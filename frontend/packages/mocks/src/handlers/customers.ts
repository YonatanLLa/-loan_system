import { http, HttpResponse } from 'msw'
import type {
  CreateCustomerRequest,
  Customer,
  Customer360,
  CustomerBankAccount,
  KycStatus,
  RefundRequest,
} from '@prestamos/api-client'

import { db, nextId, nowIso, recordAudit, type PaymentRec } from '../db'
import {
  activeInstallments,
  applyPaymentCascade,
  customerLoans,
  maxDaysLate,
  toApplicationSummary,
  toLoanSummary,
  toPaymentDto,
} from '../domain'
import { matches, notFound, paginate, problem, readJson, secured } from '../http-utils'
import { fromCents, sum, toCents } from '../money'

const DOC_PATTERNS: Record<Customer['docType'], RegExp> = {
  DNI: /^\d{8}$/,
  CE: /^\d{9}$/,
  RUC: /^\d{11}$/,
  PASAPORTE: /^[A-Z0-9]{6,12}$/,
}

function totalDebt(customerId: string): number {
  return sum(
    customerLoans(customerId)
      .filter((loan) => loan.status === 'VIGENTE' || loan.status === 'EN_MORA')
      .flatMap((loan) => activeInstallments(loan))
      .filter((inst) => inst.status !== 'PAID' && inst.status !== 'CANCELLED_BY_PREPAYMENT')
      .map(
        (inst) =>
          inst.totalDue + inst.lateFee - inst.paidPrincipal - inst.paidInterest - inst.paidFee - inst.paidLateFee,
      ),
  )
}

function to360(customer: Customer): Customer360 {
  const loans = customerLoans(customer.id)
  const days = Math.max(0, ...loans.map((loan) => maxDaysLate(loan)))
  return {
    ...customer,
    bankAccounts: db.customerBankAccounts.filter((a) => a.customerId === customer.id),
    loans: loans.map(toLoanSummary),
    applications: db.applications
      .filter((app) => app.customerId === customer.id)
      .map(toApplicationSummary),
    totalDebt: fromCents(totalDebt(customer.id)),
    maxDaysLate: days,
    hasActiveArrears: loans.some((loan) => loan.status === 'EN_MORA'),
  }
}

export const customerHandlers = [
  http.get('*/api/customers', secured('customers.read', ({ url }) => {
    const q = url.searchParams.get('q')
    const kyc = url.searchParams.get('kycStatus')
    const items = db.customers
      .filter((c) => matches(`${c.fullName} ${c.docNumber} ${c.email}`, q))
      .filter((c) => !kyc || c.kycStatus === kyc)
    return HttpResponse.json(paginate(items, url))
  })),

  http.get('*/api/customers/:id', secured<{ id: string }>('customers.read', ({ params }) => {
    const customer = db.customers.find((c) => c.id === params.id)
    return customer ? HttpResponse.json(to360(customer)) : notFound('El cliente')
  })),

  http.post('*/api/customers', secured('customers.read', async ({ request, user }) => {
    const body = await readJson<CreateCustomerRequest>(request)
    if (!body) return problem(400, 'Solicitud inválida', 'Cuerpo inválido.')
    const errors: Record<string, string[]> = {}
    if (!DOC_PATTERNS[body.docType]?.test(body.docNumber)) {
      errors.DocNumber = [`Formato inválido para ${body.docType}`]
    }
    if (!body.fullName?.trim()) errors.FullName = ['El nombre es obligatorio']
    if (!/^9\d{8}$/.test(body.phone ?? '')) errors.Phone = ['El celular tiene 9 dígitos y empieza con 9']
    if (Object.keys(errors).length > 0) return problem(400, 'Datos inválidos', 'Revisa los campos.', errors)
    if (db.customers.some((c) => c.docType === body.docType && c.docNumber === body.docNumber)) {
      return problem(409, 'Documento duplicado', `Ya existe un cliente con ${body.docType} ${body.docNumber}.`)
    }
    const customer: Customer = {
      id: nextId('cus'),
      personType: body.personType ?? 'NATURAL',
      docType: body.docType,
      docNumber: body.docNumber,
      fullName: body.fullName,
      email: body.email,
      phone: body.phone,
      address: body.address ?? '',
      district: body.district ?? '',
      birthDate: body.birthDate,
      monthlyNetIncome: body.monthlyNetIncome,
      occupation: body.occupation ?? '',
      employerName: body.employerName ?? '',
      workSeniorityMonths: body.workSeniorityMonths ?? 0,
      kycStatus: 'PENDING',
      creditBalance: '0.00',
      createdAt: nowIso(),
    }
    db.customers.unshift(customer)
    recordAudit({ entityName: 'customers', entityId: customer.id, action: 'CREATE', after: { docNumber: customer.docNumber }, userName: user.fullName })
    return HttpResponse.json(customer, { status: 201 })
  })),

  http.put('*/api/customers/:id', secured<{ id: string }>('customers.read', async ({ request, params, user }) => {
    const customer = db.customers.find((c) => c.id === params.id)
    if (!customer) return notFound('El cliente')
    const body = await readJson<Partial<CreateCustomerRequest>>(request)
    if (!body) return problem(400, 'Solicitud inválida', 'Cuerpo inválido.')
    const before = { ...customer }
    if (body.fullName !== undefined) customer.fullName = body.fullName
    if (body.email !== undefined) customer.email = body.email
    if (body.phone !== undefined) customer.phone = body.phone
    if (body.address !== undefined) customer.address = body.address
    if (body.district !== undefined) customer.district = body.district
    if (body.monthlyNetIncome !== undefined) customer.monthlyNetIncome = body.monthlyNetIncome
    if (body.occupation !== undefined) customer.occupation = body.occupation
    if (body.employerName !== undefined) customer.employerName = body.employerName
    if (body.workSeniorityMonths !== undefined) customer.workSeniorityMonths = body.workSeniorityMonths
    recordAudit({ entityName: 'customers', entityId: customer.id, action: 'UPDATE', before, after: { id: customer.id }, userName: user.fullName })
    return HttpResponse.json(customer)
  })),

  // Aprobar o rechazar el KYC (US-05). Endpoint propuesto: no figura aún en las historias.
  http.patch('*/api/customers/:id/kyc', secured<{ id: string }>('customers.read', async ({ request, params, user }) => {
    const customer = db.customers.find((c) => c.id === params.id)
    if (!customer) return notFound('El cliente')
    const body = await readJson<{ status: KycStatus; notes?: string }>(request)
    if (!body?.status) return problem(400, 'Solicitud inválida', 'Indica el estado del KYC.')
    if (body.status === 'REJECTED' && !body.notes?.trim()) {
      return problem(422, 'Falta la justificación', 'Para rechazar el KYC indica el motivo.')
    }
    const before = { kycStatus: customer.kycStatus }
    customer.kycStatus = body.status
    customer.kycNotes = body.notes
    customer.kycVerifiedAt = body.status === 'VERIFIED' ? nowIso() : undefined
    db.customerBankAccounts
      .filter((a) => a.customerId === customer.id && a.isPrimary)
      .forEach((a) => {
        a.isVerified = body.status === 'VERIFIED'
        a.status = body.status === 'VERIFIED' ? 'ACTIVE' : 'PENDING_VERIFICATION'
      })
    recordAudit({ entityName: 'customers', entityId: customer.id, action: 'APPROVE', before, after: { kycStatus: body.status }, userName: user.fullName })
    return HttpResponse.json(customer)
  })),

  http.get('*/api/customers/:id/bank-accounts', secured<{ id: string }>('customers.read', ({ params }) =>
    HttpResponse.json(db.customerBankAccounts.filter((a) => a.customerId === params.id)),
  )),

  http.post('*/api/customers/:id/bank-accounts', secured<{ id: string }>('customers.read', async ({ request, params }) => {
    const customer = db.customers.find((c) => c.id === params.id)
    if (!customer) return notFound('El cliente')
    const body = await readJson<Partial<CustomerBankAccount> & { accountNumber?: string }>(request)
    if (!body?.bankName) return problem(400, 'Datos inválidos', 'Indica el banco.', { BankName: ['El banco es obligatorio'] })
    if (body.cci && !/^\d{20}$/.test(body.cci)) {
      return problem(400, 'Datos inválidos', 'El CCI tiene 20 dígitos.', { Cci: ['El CCI tiene 20 dígitos'] })
    }
    const account: CustomerBankAccount = {
      id: nextId('cba'),
      customerId: customer.id,
      bankName: body.bankName,
      accountType: body.accountType ?? 'CUENTA_AHORRO',
      accountNumberMasked: `**** ${(body.cci ?? body.accountNumber ?? '0000').slice(-4)}`,
      cci: body.cci,
      phoneWallet: body.phoneWallet,
      isVerified: false,
      isPrimary: false,
      status: 'PENDING_VERIFICATION',
    }
    db.customerBankAccounts.push(account)
    return HttpResponse.json(account, { status: 201 })
  })),

  http.post('*/api/customer-bank-accounts/:id/verify', secured<{ id: string }>('customers.read', ({ params }) => {
    const account = db.customerBankAccounts.find((a) => a.id === params.id)
    if (!account) return notFound('La cuenta')
    account.isVerified = true
    account.status = 'ACTIVE'
    return HttpResponse.json(account)
  })),

  http.post('*/api/customer-bank-accounts/:id/make-primary', secured<{ id: string }>('customers.read', ({ params }) => {
    const account = db.customerBankAccounts.find((a) => a.id === params.id)
    if (!account) return notFound('La cuenta')
    db.customerBankAccounts
      .filter((a) => a.customerId === account.customerId)
      .forEach((a) => { a.isPrimary = a.id === account.id })
    return HttpResponse.json(account)
  })),

  // ── Portal del Cliente (FE-CP-03 a FE-CP-05, FE-CP-18) ──────────────────────
  http.get('*/api/client/profile', secured(null, ({ user }) => {
    if (!user.customerId) return problem(403, 'Acceso denegado', 'Solo clientes del portal.')
    const customer = db.customers.find((c) => c.id === user.customerId)
    if (!customer) return notFound('El cliente')
    return HttpResponse.json(customer)
  })),

  http.post('*/api/client/onboarding/kyc', secured(null, async ({ request, user }) => {
    if (!user.customerId) return problem(403, 'Acceso denegado', 'Solo clientes del portal.')
    const customer = db.customers.find((c) => c.id === user.customerId)
    if (!customer) return notFound('El cliente')
    const body = await readJson<{ dniFrontUrl?: string; dniBackUrl?: string; selfieUrl?: string }>(request)
    if (!body?.dniFrontUrl || !body?.selfieUrl) {
      return problem(400, 'Documentos incompletos', 'Se requiere foto del DNI anverso y selfie con prueba de vida.')
    }
    customer.kycStatus = 'VERIFIED'
    customer.kycVerifiedAt = nowIso()
    customer.kycNotes = 'Verificación biométrica y OCR aprobada automáticamente.'
    recordAudit({
      entityName: 'customers',
      entityId: customer.id,
      action: 'KYC_VERIFIED',
      userName: user.fullName,
    })
    return HttpResponse.json({ customer, message: 'Identidad verificada exitosamente.' })
  })),

  http.post('*/api/client/onboarding/consents', secured(null, async ({ request, user }) => {
    const body = await readJson<{ dataProtection: boolean; creditBureau: boolean; marketing: boolean }>(request)
    if (!body?.dataProtection || !body?.creditBureau) {
      return problem(400, 'Consentimientos requeridos', 'Debes aceptar las cláusulas obligatorias de tratamiento de datos y consulta a centrales.')
    }
    recordAudit({
      entityName: 'customers',
      entityId: user.customerId || user.id,
      action: 'CONSENTS_ACCEPTED',
      userName: user.fullName,
    })
    return HttpResponse.json({ success: true, message: 'Consentimientos registrados.' })
  })),

  http.get('*/api/client/bank-accounts', secured(null, ({ user }) => {
    if (!user.customerId) return HttpResponse.json([])
    return HttpResponse.json(db.customerBankAccounts.filter((a) => a.customerId === user.customerId))
  })),

  http.post('*/api/client/bank-accounts', secured(null, async ({ request, user }) => {
    if (!user.customerId) return problem(403, 'Acceso denegado', 'Solo clientes del portal.')
    const body = await readJson<{ bankName: string; accountType?: string; cci?: string; phoneWallet?: string }>(request)
    if (!body?.bankName) {
      return problem(400, 'Datos inválidos', 'Indica el nombre de la entidad bancaria.')
    }
    if (body.cci && !/^\d{20}$/.test(body.cci)) {
      return problem(400, 'CCI inválido', 'El Código de Cuenta Interbancario (CCI) debe contener 20 dígitos.')
    }
    const existingAccounts = db.customerBankAccounts.filter((a) => a.customerId === user.customerId)
    const isFirst = existingAccounts.length === 0
    const account: CustomerBankAccount = {
      id: nextId('cba'),
      customerId: user.customerId,
      bankName: body.bankName,
      accountType: (body.accountType as CustomerBankAccount['accountType']) || (body.phoneWallet ? 'BILLETERA_DIGITAL' : 'CUENTA_AHORRO'),
      accountNumberMasked: body.phoneWallet ? `Billetera: ${body.phoneWallet}` : `**** ${(body.cci || '0000').slice(-4)}`,
      cci: body.cci,
      phoneWallet: body.phoneWallet,
      isVerified: true,
      isPrimary: isFirst,
      status: 'ACTIVE',
    }
    db.customerBankAccounts.push(account)
    recordAudit({
      entityName: 'customer_bank_accounts',
      entityId: account.id,
      action: 'CREATE',
      userName: user.fullName,
    })
    return HttpResponse.json(account, { status: 201 })
  })),

  // ── Gestión de Saldo a Favor en Backoffice (FE-BO-24 / US-17 / RF-17.3) ──────
  http.post('*/api/customers/:id/credit-balance/apply', secured<{ id: string }>('payments.read', async ({ request, params, user }) => {
    const customer = db.customers.find((c) => c.id === params.id)
    if (!customer) return notFound('El cliente')
    const body = await readJson<{ loanId: string; amount: string }>(request)
    if (!body?.loanId || !body?.amount) {
      return problem(400, 'Datos incompletos', 'Selecciona el préstamo de destino y el monto a aplicar.')
    }
    const loan = db.loans.find((l) => l.id === body.loanId && l.customerId === customer.id)
    if (!loan) return notFound('El préstamo')
    if (loan.status !== 'VIGENTE' && loan.status !== 'EN_MORA') {
      return problem(422, 'Préstamo sin deuda exigible', 'Solo se aplica saldo a favor a préstamos vigentes o en mora.')
    }
    const creditCents = toCents(customer.creditBalance)
    const applyCents = toCents(body.amount)
    if (applyCents <= 0) {
      return problem(400, 'Monto inválido', 'El monto debe ser mayor a cero.')
    }
    if (applyCents > creditCents) {
      return problem(422, 'Saldo insuficiente', `El monto supera el saldo a favor disponible (S/ ${customer.creditBalance}).`)
    }
    customer.creditBalance = fromCents(creditCents - applyCents)
    const payment: PaymentRec = {
      id: nextId('pay'),
      loanId: loan.id,
      amount: applyCents,
      method: 'TRANSFERENCIA_BANCARIA',
      kind: 'NORMAL',
      status: 'PENDING_CONFIRMATION',
      externalReference: `SAF-${customer.id}-${Date.now()}`,
      paidAt: nowIso(),
      createdAt: nowIso(),
      allocations: [],
      creditToCustomer: 0,
    }
    db.payments.unshift(payment)
    applyPaymentCascade(payment)
    recordAudit({
      entityName: 'customers',
      entityId: customer.id,
      action: 'CREDIT_BALANCE_APPLIED',
      userName: user.fullName,
      after: { loanId: loan.id, amount: body.amount, newCreditBalance: customer.creditBalance },
    })
    return HttpResponse.json({
      payment: toPaymentDto(payment),
      customer: to360(customer),
    })
  })),

  http.post('*/api/customers/:id/credit-balance/refund', secured<{ id: string }>('payments.read', async ({ request, params, user }) => {
    const customer = db.customers.find((c) => c.id === params.id)
    if (!customer) return notFound('El cliente')
    const body = await readJson<{ bankAccountId: string; amount: string }>(request)
    if (!body?.bankAccountId || !body?.amount) {
      return problem(400, 'Datos incompletos', 'Selecciona la cuenta bancaria verificada y el monto a devolver.')
    }
    const bankAccount = db.customerBankAccounts.find((a) => a.id === body.bankAccountId && a.customerId === customer.id)
    if (!bankAccount) return notFound('La cuenta bancaria')
    if (!bankAccount.isVerified) {
      return problem(422, 'Cuenta no verificada', 'Solo se procesan devoluciones a cuentas bancarias verificadas.')
    }
    const creditCents = toCents(customer.creditBalance)
    const refundCents = toCents(body.amount)
    if (refundCents <= 0) {
      return problem(400, 'Monto inválido', 'El monto debe ser mayor a cero.')
    }
    if (refundCents > creditCents) {
      return problem(422, 'Saldo insuficiente', `El monto supera el saldo a favor disponible (S/ ${customer.creditBalance}).`)
    }
    customer.creditBalance = fromCents(creditCents - refundCents)
    const refund: RefundRequest = {
      id: nextId('ref'),
      customerId: customer.id,
      amount: body.amount,
      customerBankAccountId: bankAccount.id,
      status: 'DISBURSED',
      requestedAt: nowIso(),
      bankName: bankAccount.bankName,
      accountNumberMasked: bankAccount.accountNumberMasked,
    }
    db.refundRequests.unshift(refund)
    recordAudit({
      entityName: 'refund_requests',
      entityId: refund.id,
      action: 'CREATE',
      userName: user.fullName,
      after: { amount: refund.amount, bankAccountId: bankAccount.id },
    })
    return HttpResponse.json({
      refund,
      customer: to360(customer),
    }, { status: 201 })
  })),
]
