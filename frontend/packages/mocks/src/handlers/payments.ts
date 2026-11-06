import { http, HttpResponse } from 'msw'
import type {
  ClientPaymentRequest,
  PaymentIntent,
  RegisterPaymentRequest,
  ReversePaymentRequest,
} from '@prestamos/api-client'

import { db, nextId, nowIso, type PaymentRec } from '../db'
import { applyPaymentCascade, reversePayment, toLoanDto, toPaymentDto } from '../domain'
import {
  idempotent,
  matches,
  notFound,
  paginate,
  pdfResponse,
  problem,
  readJson,
  secured,
} from '../http-utils'
import { toCents } from '../money'

const MONEY = /^\d+(\.\d{1,2})?$/
/** Cuánto tarda el "webhook" de la pasarela en confirmar un pago con QR o tarjeta. */
const WEBHOOK_DELAY_MS = 5_000

const newPayment = (partial: Partial<PaymentRec> & Pick<PaymentRec, 'loanId' | 'amount' | 'method'>): PaymentRec => ({
  id: nextId('pay'),
  kind: 'NORMAL',
  status: 'PENDING_CONFIRMATION',
  paidAt: nowIso(),
  createdAt: nowIso(),
  allocations: [],
  creditToCustomer: 0,
  ...partial,
})

export const paymentHandlers = [
  http.get('*/api/payments', secured('payments.read', ({ url }) => {
    const status = url.searchParams.get('status')
    const loanId = url.searchParams.get('loanId')
    const q = url.searchParams.get('q')
    const items = db.payments
      .map(toPaymentDto)
      .filter((p) => !status || status.split(',').includes(p.status))
      .filter((p) => !loanId || p.loanId === loanId)
      .filter((p) => matches(`${p.customerName} ${p.receiptNumber ?? ''} ${p.externalReference ?? ''}`, q))
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    return HttpResponse.json(paginate(items, url))
  })),

  http.get('*/api/payments/:id', secured<{ id: string }>('payments.read', ({ params }) => {
    const payment = db.payments.find((p) => p.id === params.id)
    return payment ? HttpResponse.json(toPaymentDto(payment)) : notFound('El pago')
  })),

  // Pago identificado en el extracto (RF-16.2): se aplica de inmediato con la prelación estricta.
  http.post('*/api/loans/:id/payments', secured<{ id: string }>('payments.read', async ({ request, params }) => {
    const body = await readJson<RegisterPaymentRequest>(request)
    return idempotent(request, () => {
      const loan = db.loans.find((l) => l.id === params.id)
      if (!loan) return notFound('El préstamo')
      if (!body || !MONEY.test(body.amount ?? '') || toCents(body.amount) <= 0) {
        return problem(400, 'Datos inválidos', 'Monto inválido.', { Amount: ['Ingresa un monto mayor a cero'] })
      }
      if (loan.status !== 'VIGENTE' && loan.status !== 'EN_MORA') {
        return problem(422, 'Préstamo sin deuda exigible', 'Solo se registran pagos de préstamos vigentes o en mora.')
      }
      if (!body.externalReference?.trim()) {
        return problem(422, 'Falta la referencia', 'Indica el código de operación del banco o la pasarela.', { ExternalReference: ['La referencia es obligatoria'] })
      }
      if (db.payments.some((p) => p.externalReference === body.externalReference && p.status !== 'REVERSED')) {
        return problem(409, 'Operación duplicada', 'Ya existe un pago con esa referencia.')
      }
      const payment = newPayment({
        loanId: loan.id,
        amount: toCents(body.amount),
        method: body.method,
        externalReference: body.externalReference,
        companyBankAccountId: body.companyBankAccountId,
        paidAt: body.paidAt ?? nowIso(),
      })
      db.payments.unshift(payment)
      applyPaymentCascade(payment)
      return { status: 201, body: toPaymentDto(payment) }
    })
  })),

  // Confirmar un voucher tras verlo en el extracto bancario (RF-16.2.1).
  http.post('*/api/payments/:id/confirm', secured<{ id: string }>('payments.read', ({ params }) => {
    const payment = db.payments.find((p) => p.id === params.id)
    if (!payment) return notFound('El pago')
    if (payment.status !== 'PENDING_CONFIRMATION') {
      return problem(409, 'Estado inválido', 'El pago no está pendiente de confirmación.')
    }
    applyPaymentCascade(payment)
    return HttpResponse.json(toPaymentDto(payment))
  })),

  http.post('*/api/payments/:id/reverse', secured<{ id: string }>('payments.reverse', async ({ request, params }) => {
    const payment = db.payments.find((p) => p.id === params.id)
    if (!payment) return notFound('El pago')
    const body = await readJson<ReversePaymentRequest>(request)
    if (!body?.reason?.trim()) {
      return problem(422, 'Falta el motivo', 'El motivo del reverso es obligatorio.', { Reason: ['El motivo es obligatorio'] })
    }
    if (payment.status === 'REVERSED' || payment.kind === 'REVERSAL') {
      return problem(409, 'Pago ya revertido', 'El pago ya se encuentra revertido.')
    }
    if (payment.status !== 'APPLIED') {
      return problem(422, 'Pago no aplicado', 'Solo se revierten pagos aplicados.')
    }
    return HttpResponse.json(toPaymentDto(reversePayment(payment, body.reason)), { status: 201 })
  })),

  // ── Portal del cliente (US-29) ─────────────────────────────────────────────
  http.post('*/api/client/payments', secured('client.portal', async ({ request, user }) => {
    const body = await readJson<ClientPaymentRequest>(request)
    return idempotent(request, () => {
      const loan = db.loans.find((l) => l.id === body?.loanId && l.customerId === user.customerId)
      if (!loan) return notFound('El préstamo')
      if (!body || !MONEY.test(body.amount ?? '') || toCents(body.amount) <= 0) {
        return problem(400, 'Datos inválidos', 'Monto inválido.', { Amount: ['Ingresa un monto mayor a cero'] })
      }
      if (loan.status !== 'VIGENTE' && loan.status !== 'EN_MORA') {
        return problem(422, 'Préstamo sin deuda exigible', 'Este préstamo no tiene cuotas por pagar.')
      }
      const method = db.paymentMethods.find((m) => m.methodType === body.method)
      if (!method?.isActive) return problem(422, 'Canal no disponible', 'Ese medio de pago no está habilitado.')

      const viaWebhook = method.webhookEnabled
      const payment = newPayment({
        loanId: loan.id,
        amount: toCents(body.amount),
        method: body.method,
        externalReference: body.voucherFileName ? `VOUCHER-${body.voucherFileName}` : undefined,
        confirmAt: viaWebhook ? Date.now() + WEBHOOK_DELAY_MS : undefined,
        companyBankAccountId: 'bca-001',
      })
      db.payments.unshift(payment)

      const intent: PaymentIntent = {
        paymentId: payment.id,
        loanId: loan.id,
        amount: body.amount,
        method: body.method,
        status: 'PENDING_CONFIRMATION',
        qrPayload:
          body.method === 'YAPE' || body.method === 'PLIN'
            ? `00020101021226${body.method}|${payment.id}|${body.amount}|PEN`
            : undefined,
        checkoutUrl:
          body.method === 'TARJETA_DEBITO' || body.method === 'TARJETA_CREDITO' || body.method === 'PASARELA_PAGO'
            ? `https://checkout.pasarela.test/pay/${payment.id}`
            : undefined,
        expiresAt: new Date(Date.now() + 30 * 60_000).toISOString(),
      }
      return { status: 201, body: intent }
    })
  })),

  http.get('*/api/client/payments', secured('client.portal', ({ user }) => {
    const mine = new Set(db.loans.filter((l) => l.customerId === user.customerId).map((l) => l.id))
    return HttpResponse.json(
      db.payments.filter((p) => mine.has(p.loanId)).map(toPaymentDto),
    )
  })),

  // El portal consulta este endpoint cada pocos segundos hasta que el pago se confirma.
  http.get('*/api/client/payments/:id', secured<{ id: string }>('client.portal', ({ params, user }) => {
    const payment = db.payments.find((p) => p.id === params.id)
    const loan = db.loans.find((l) => l.id === payment?.loanId)
    if (!payment || !loan || loan.customerId !== user.customerId) return notFound('El pago')
    return HttpResponse.json({ ...toPaymentDto(payment), loan: toLoanDto(loan) })
  })),

  http.get('*/api/client/payments/:id/receipt.pdf', secured<{ id: string }>('client.portal', ({ params, user }) => {
    const payment = db.payments.find((p) => p.id === params.id)
    const loan = db.loans.find((l) => l.id === payment?.loanId)
    if (!payment || !loan || loan.customerId !== user.customerId) return notFound('El pago')
    if (payment.status !== 'APPLIED') {
      return problem(409, 'Pago sin confirmar', 'El recibo se emite cuando el pago esté confirmado.')
    }
    return pdfResponse(`Recibo ${payment.receiptNumber ?? payment.id}`)
  })),
]
