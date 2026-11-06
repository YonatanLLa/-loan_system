import { http, HttpResponse } from 'msw'
import type {
  CreateApplicationRequest,
  DecisionRequest,
  LoanApplication,
  SimulateRequest,
} from '@prestamos/api-client'

import { db, nextId, nowIso, recordAudit, type UserRec } from '../db'
import { evaluateDti, simulateLoan, validateAdmission } from '../domain'
import { matches, notFound, paginate, problem, readJson, secured } from '../http-utils'
import { toCents } from '../money'

function simulate(body: SimulateRequest | null, customerId?: string): Response {
  if (!body) return problem(400, 'Solicitud inválida', 'Cuerpo inválido.')
  const product = db.products.find((p) => p.id === body.productId)
  if (!product) return notFound('El producto')
  if (!/^\d+(\.\d{1,2})?$/.test(body.amount ?? '')) {
    return problem(400, 'Datos inválidos', 'Monto inválido.', { Amount: ['Ingresa un monto con hasta 2 decimales'] })
  }
  const frequency = body.frequency ?? product.allowedFrequencies[0]!
  const errors = validateAdmission(customerId, product, toCents(body.amount), body.term, frequency)
  if (Object.keys(errors).length > 0) return problem(400, 'Datos fuera de rango', 'La simulación no cumple las reglas del producto.', errors)
  // Cálculo puro en memoria: no crea ni modifica nada (RF-11.4).
  return HttpResponse.json(
    simulateLoan(product, toCents(body.amount), body.term, frequency, body.firstDueDate),
  )
}

function createApplication(
  body: CreateApplicationRequest | null,
  customerId: string | undefined,
  origin: LoanApplication['originatedFrom'],
  user: UserRec,
): Response {
  if (!body) return problem(400, 'Solicitud inválida', 'Cuerpo inválido.')
  const customer = db.customers.find((c) => c.id === customerId)
  if (!customer) return problem(400, 'Datos inválidos', 'Indica el cliente.', { CustomerId: ['El cliente es obligatorio'] })
  const product = db.products.find((p) => p.id === body.productId)
  if (!product || product.status !== 'ACTIVO') return problem(422, 'Producto no disponible', 'El producto no está activo.')
  const amount = toCents(body.amount)
  const errors = validateAdmission(customer.id, product, amount, body.term, body.frequency)
  if (Object.keys(errors).length > 0) return problem(400, 'Datos fuera de rango', 'La solicitud no cumple las reglas del producto.', errors)

  const evaluation = evaluateDti(customer.id, product, amount, body.term, body.frequency)
  const application: LoanApplication = {
    id: nextId('app'),
    customerId: customer.id,
    customerName: customer.fullName,
    productId: product.id,
    productName: product.name,
    requestedAmount: body.amount,
    term: body.term,
    frequency: body.frequency,
    destination: body.destination ?? 'CONSUMO',
    status: 'SOLICITADO',
    dtiRatio: evaluation.dti,
    systemRecommendation: evaluation.recommendation,
    originatedFrom: origin,
    createdAt: nowIso(),
  }
  db.applications.unshift(application)
  recordAudit({ entityName: 'loan_applications', entityId: application.id, action: 'CREATE', after: { status: 'SOLICITADO' }, userName: user.fullName })
  return HttpResponse.json(application, { status: 201 })
}

export const originationHandlers = [
  http.post('*/api/loan-applications/simulate', secured(null, async ({ request }) =>
    simulate(await readJson<SimulateRequest>(request)),
  )),

  http.post('*/api/loan-applications/:id/simulate', secured<{ id: string }>(null, async ({ request, params }) => {
    const application = db.applications.find((a) => a.id === params.id)
    if (!application) return notFound('La solicitud')
    const body = await readJson<Partial<SimulateRequest>>(request)
    return simulate(
      {
        productId: application.productId,
        amount: application.requestedAmount,
        term: application.term,
        frequency: application.frequency,
        ...body,
      },
      application.customerId,
    )
  })),

  http.get('*/api/loan-applications', secured('applications.read', ({ url }) => {
    const status = url.searchParams.get('status')
    const q = url.searchParams.get('q')
    const items = db.applications
      .filter((a) => !status || status.split(',').includes(a.status))
      .filter((a) => matches(`${a.customerName} ${a.productName} ${a.id}`, q))
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    return HttpResponse.json(paginate(items, url))
  })),

  http.get('*/api/loan-applications/:id', secured<{ id: string }>('applications.read', ({ params }) => {
    const application = db.applications.find((a) => a.id === params.id)
    return application ? HttpResponse.json(application) : notFound('La solicitud')
  })),

  http.post('*/api/loan-applications', secured('applications.read', async ({ request, user }) => {
    const body = await readJson<CreateApplicationRequest>(request)
    return createApplication(body, body?.customerId, 'BACKOFFICE', user)
  })),

  // El analista toma la solicitud para evaluarla (SOLICITADO → EN_EVALUACION).
  http.post('*/api/loan-applications/:id/start-review', secured<{ id: string }>('applications.read', ({ params, user }) => {
    const application = db.applications.find((a) => a.id === params.id)
    if (!application) return notFound('La solicitud')
    if (application.status !== 'SOLICITADO') {
      return problem(409, 'Estado inválido', 'Solo se puede evaluar una solicitud en estado SOLICITADO.')
    }
    application.status = 'EN_EVALUACION'
    recordAudit({ entityName: 'loan_applications', entityId: application.id, action: 'UPDATE', before: { status: 'SOLICITADO' }, after: { status: 'EN_EVALUACION' }, userName: user.fullName })
    return HttpResponse.json(application)
  })),

  http.post('*/api/loan-applications/:id/decision', secured<{ id: string }>('applications.read', async ({ request, params, user }) => {
    const application = db.applications.find((a) => a.id === params.id)
    if (!application) return notFound('La solicitud')
    const body = await readJson<DecisionRequest>(request)
    if (!body?.decision) return problem(400, 'Solicitud inválida', 'Indica la decisión.')
    if (!body.notes?.trim()) {
      return problem(422, 'Falta la justificación', 'Debes indicar el motivo de la decisión.', { Notes: ['La justificación es obligatoria'] })
    }
    if (['APROBADO', 'APROBADO_CON_CONDICIONES', 'RECHAZADO', 'DESEMBOLSADO', 'ANULADO'].includes(application.status)) {
      return problem(409, 'Solicitud ya dictaminada', 'No se puede cambiar el estado sin una reapertura autorizada.')
    }
    if (body.decision === 'APROBADO_CON_CONDICIONES') {
      if (!body.approvedAmount) return problem(422, 'Falta el monto', 'Indica el monto aprobado.', { ApprovedAmount: ['El monto aprobado es obligatorio'] })
      if (toCents(body.approvedAmount) >= toCents(application.requestedAmount)) {
        return problem(422, 'Monto inválido', 'El monto aprobado debe ser menor al solicitado.')
      }
    }
    const before = { status: application.status }
    application.status = body.decision
    application.approvedAmount =
      body.decision === 'RECHAZADO' ? undefined : (body.approvedAmount ?? application.requestedAmount)
    application.decisionNotes = body.notes
    application.decidedBy = user.fullName
    application.decidedAt = nowIso()
    recordAudit({ entityName: 'loan_applications', entityId: application.id, action: 'APPROVE', before, after: { status: body.decision }, userName: user.fullName })
    return HttpResponse.json(application)
  })),

  // Portal del cliente (US-27): el cliente solo opera sobre sus propios registros.
  http.post('*/api/client/loan-applications', secured('client.portal', async ({ request, user }) => {
    const customer = db.customers.find((c) => c.id === user.customerId)
    if (customer?.kycStatus !== 'VERIFIED') {
      return problem(422, 'KYC pendiente', 'Verifica tu identidad para solicitar un préstamo.')
    }
    return createApplication(await readJson<CreateApplicationRequest>(request), user.customerId, 'PORTAL', user)
  })),

  http.get('*/api/client/loan-applications', secured('client.portal', ({ user }) =>
    HttpResponse.json(
      db.applications
        .filter((a) => a.customerId === user.customerId)
        .sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
    ),
  )),
]
