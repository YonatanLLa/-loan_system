import { http, HttpResponse } from 'msw'
import type { CustomerBankAccount, SimulateRequest } from '@prestamos/api-client'

import { addDays, today } from '../dates'
import { db, nextId, nowIso, recordAudit } from '../db'
import {
  activeInstallments,
  quotePrepayment,
  simulateLoan,
  toInstallmentDto,
  toLoanDto,
  toPaymentDto,
} from '../domain'
import { idempotent, notFound, pdfResponse, problem, readJson, secured } from '../http-utils'
import { toCents } from '../money'
import { statement } from './collections-reports'

/** Préstamos del cliente autenticado; un préstamo ajeno responde 404 (RF-26.5). */
const ownLoan = (loanId: string, customerId?: string) =>
  db.loans.find((l) => l.id === loanId && l.customerId === customerId)

interface MockNotification {
  id: string
  customerId: string
  title: string
  message: string
  type: 'PAYMENT_DUE' | 'PAYMENT_CONFIRMED' | 'DISBURSEMENT_READY' | 'APPLICATION_UPDATE'
  isRead: boolean
  createdAt: string
  actionUrl?: string
}

interface MockComplaint {
  id: string
  customerId: string
  filingNumber: string
  type: 'QUEJA' | 'RECLAMO'
  detail: string
  requestedSolution: string
  status: 'RECEIVED' | 'IN_REVIEW' | 'RESOLVED'
  createdAt: string
}

const clientNotifications: MockNotification[] = [
  {
    id: 'not-001',
    customerId: 'cus-001',
    title: 'Próximo vencimiento de cuota',
    message: 'Tu próxima cuota del Crédito Personal Digital vence pronto. Paga a tiempo con Yape o Plin.',
    type: 'PAYMENT_DUE',
    isRead: false,
    createdAt: `${today()}T08:00:00Z`,
    actionUrl: '/(app)/loans/loan-001',
  },
  {
    id: 'not-002',
    customerId: 'cus-001',
    title: 'Solicitud en evaluación',
    message: 'Tu solicitud APP-008 por S/ 2,500.00 se encuentra en evaluación crediticia.',
    type: 'APPLICATION_UPDATE',
    isRead: false,
    createdAt: `${today()}T08:20:00Z`,
    actionUrl: '/(app)/applications',
  },
  {
    id: 'not-003',
    customerId: 'cus-001',
    title: 'Pago confirmado',
    message: 'Hemos aplicado exitosamente el pago de tu cuota #2. Tu recibo PDF está disponible.',
    type: 'PAYMENT_CONFIRMED',
    isRead: true,
    createdAt: `${addDays(today(), -15)}T15:05:00Z`,
  },
]

const clientComplaints: MockComplaint[] = []

export const clientHandlers = [
  http.get(
    '*/api/client/loans',
    secured('client.portal', ({ user }) =>
      HttpResponse.json(db.loans.filter((l) => l.customerId === user.customerId).map(toLoanDto)),
    ),
  ),

  http.get(
    '*/api/client/loans/:id',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      return loan ? HttpResponse.json(toLoanDto(loan)) : notFound('El préstamo')
    }),
  ),

  http.get(
    '*/api/client/loans/:id/schedule',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      if (!loan) return notFound('El préstamo')
      return HttpResponse.json({
        loanId: loan.id,
        version: loan.scheduleVersion,
        installments: activeInstallments(loan).map(toInstallmentDto),
      })
    }),
  ),

  http.get(
    '*/api/client/loans/:id/payments',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      if (!loan) return notFound('El préstamo')
      return HttpResponse.json(
        db.payments.filter((p) => p.loanId === loan.id).map(toPaymentDto),
      )
    }),
  ),

  http.get(
    '*/api/client/loans/:id/statement',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      return loan ? HttpResponse.json(statement(loan.id)) : notFound('El préstamo')
    }),
  ),

  http.get(
    '*/api/client/loans/:id/statement.pdf',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      return loan ? pdfResponse(`Estado de cuenta ${loan.id}`) : notFound('El préstamo')
    }),
  ),

  http.post(
    '*/api/client/loans/:id/prepayment/quote',
    secured<{ id: string }>('client.portal', async ({ request, params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      if (!loan) return notFound('El préstamo')
      const body = await readJson<{
        type?: 'TOTAL' | 'PARTIAL'
        amount?: string
        reductionOption?: 'REDUCE_TERM' | 'REDUCE_INSTALLMENT'
        asOfDate?: string
      }>(request)
      const quote = quotePrepayment(loan, body?.asOfDate ?? today())
      return HttpResponse.json({
        ...quote,
        type: body?.type ?? 'TOTAL',
        reductionOption: body?.reductionOption,
        interestToDate: quote.accruedInterest,
        penaltyFee: quote.prepaymentPenalty,
        totalPayable: quote.totalToPay,
        expiresAt: `${quote.validUntil}T23:59:59Z`,
      })
    }),
  ),

  http.get(
    '*/api/client/loans/:id/contract',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      if (!loan) return notFound('El préstamo')
      return HttpResponse.json({
        loanId: loan.id,
        contractNumber: `CTR-${loan.id.toUpperCase()}`,
        pdfUrl: `/api/client/loans/${loan.id}/documents/contract.pdf`,
        summarySheetPdfUrl: `/api/client/loans/${loan.id}/documents/summary.pdf`,
        isSigned: loan.status !== 'PENDIENTE_DESEMBOLSO',
        signedAt: loan.status !== 'PENDIENTE_DESEMBOLSO' ? loan.createdAt : undefined,
      })
    }),
  ),

  http.get(
    '*/api/client/loans/:id/documents/contract.pdf',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      return loan ? pdfResponse(`Contrato ${loan.id}`) : notFound('El préstamo')
    }),
  ),

  http.post(
    '*/api/client/loans/:id/request-sign-otp',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      if (!loan) return notFound('El préstamo')
      return HttpResponse.json({ sent: true })
    }),
  ),

  http.post(
    '*/api/client/loans/:id/sign',
    secured<{ id: string }>('client.portal', async ({ request, params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      if (!loan) return notFound('El préstamo')
      const body = await readJson<{ otpCode?: string }>(request)
      if (!body?.otpCode || !/^\d{6}$/.test(body.otpCode)) {
        return problem(400, 'Código OTP inválido', 'Ingresa el código de 6 dígitos.')
      }
      const signedAt = nowIso()
      recordAudit({
        entityName: 'loans',
        entityId: loan.id,
        action: 'SIGN_CONTRACT',
        after: { signedAt },
        userName: user.fullName,
      })
      return HttpResponse.json({ success: true, signedAt })
    }),
  ),

  http.post(
    '*/api/client/loan-applications/simulate',
    secured('client.portal', async ({ request }) => {
      const body = await readJson<
        SimulateRequest & { termMonths?: number }
      >(request)
      const product =
        db.products.find((p) => p.id === body?.productId) ??
        db.products.find((p) => p.status === 'ACTIVO') ??
        db.products[0]!
      const term = Number(body?.term ?? body?.termMonths ?? 12)
      const amount = body?.amount ?? '1000.00'
      const result = simulateLoan(
        product,
        toCents(amount),
        term,
        body?.frequency ?? product.allowedFrequencies[0] ?? 'MENSUAL',
        body?.firstDueDate,
      )
      return HttpResponse.json({
        ...result,
        monthlyInstallment: result.summary.installmentAmount,
        tea: `${(Number(result.summary.tea) * 100).toFixed(2)}%`,
        tcea: `${(Number(result.summary.tcea) * 100).toFixed(2)}%`,
        totalCost: result.summary.totalCost,
        installmentsCount: result.installments.length,
      })
    }),
  ),

  http.get(
    '*/api/client/loan-applications/:id',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const app = db.applications.find(
        (a) => a.id === params.id && a.customerId === user.customerId,
      )
      return app ? HttpResponse.json(app) : notFound('La solicitud')
    }),
  ),

  http.get(
    '*/api/client/profile',
    secured('client.portal', ({ user }) => {
      const customer = db.customers.find((c) => c.id === user.customerId) ?? db.customers[0]
      return customer ? HttpResponse.json(customer) : notFound('El perfil del cliente')
    }),
  ),

  http.put(
    '*/api/client/profile',
    secured('client.portal', async ({ request, user }) => {
      const customer = db.customers.find((c) => c.id === user.customerId)
      if (!customer) return notFound('El perfil del cliente')
      const body = await readJson<{ phone?: string; address?: string; district?: string; email?: string }>(request)
      if (body?.phone) customer.phone = body.phone
      if (body?.address) customer.address = body.address
      if (body?.district) customer.district = body.district
      if (body?.email) customer.email = body.email
      return HttpResponse.json(customer)
    }),
  ),

  http.post(
    '*/api/client/profile/refund-credit-balance',
    secured('client.portal', async ({ request, user }) =>
      idempotent(request, () => {
        const customer = db.customers.find((c) => c.id === user.customerId)
        if (!customer) return notFound('El cliente')
        customer.creditBalance = '0.00'
        return {
          status: 200,
          body: { refundId: nextId('ref'), status: 'PROCESSING' },
        }
      }),
    ),
  ),

  http.post(
    '*/api/client/documents',
    secured('client.portal', () =>
      HttpResponse.json({ documentId: nextId('doc') }, { status: 201 }),
    ),
  ),

  http.post(
    '*/api/client/consents',
    secured('client.portal', () =>
      HttpResponse.json({ accepted: true, recordedAt: nowIso() }),
    ),
  ),

  http.get(
    '*/api/client/bank-accounts',
    secured('client.portal', ({ user }) =>
      HttpResponse.json(db.customerBankAccounts.filter((a) => a.customerId === user.customerId)),
    ),
  ),

  http.post(
    '*/api/client/bank-accounts',
    secured('client.portal', async ({ request, user }) => {
      const body = await readJson<{
        bankName?: string
        accountType?: CustomerBankAccount['accountType']
        cci?: string
        phoneWallet?: string
      }>(request)
      if (!body?.bankName) {
        return problem(400, 'Datos inválidos', 'Indica el nombre del banco o billetera.')
      }
      const account: CustomerBankAccount = {
        id: nextId('cba'),
        customerId: user.customerId ?? 'cus-001',
        bankName: body.bankName,
        accountType: body.accountType ?? 'CUENTA_AHORRO',
        accountNumberMasked: `**** ${(body.cci ?? body.phoneWallet ?? '0000').slice(-4)}`,
        cci: body.cci,
        phoneWallet: body.phoneWallet,
        isVerified: true,
        isPrimary: false,
        status: 'ACTIVE',
      }
      db.customerBankAccounts.push(account)
      return HttpResponse.json(account, { status: 201 })
    }),
  ),

  http.put(
    '*/api/client/bank-accounts/:id/verify',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const account = db.customerBankAccounts.find(
        (a) => a.id === params.id && a.customerId === user.customerId,
      )
      if (!account) return notFound('La cuenta bancaria')
      account.isVerified = true
      account.status = 'ACTIVE'
      return HttpResponse.json(account)
    }),
  ),

  http.post(
    '*/api/client/bank-accounts/:id/request-change-otp',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const account = db.customerBankAccounts.find(
        (a) => a.id === params.id && a.customerId === user.customerId,
      )
      if (!account) return notFound('La cuenta bancaria')
      return HttpResponse.json({
        coolingPeriodHours: 24,
        effectiveAt: `${addDays(today(), 1)}T12:00:00Z`,
      })
    }),
  ),

  http.put(
    '*/api/client/bank-accounts/:id/set-primary',
    secured<{ id: string }>('client.portal', async ({ request, params, user }) =>
      idempotent(request, () => {
        const account = db.customerBankAccounts.find(
          (a) => a.id === params.id && a.customerId === user.customerId,
        )
        if (!account) return notFound('La cuenta bancaria')
        db.customerBankAccounts
          .filter((a) => a.customerId === user.customerId)
          .forEach((a) => {
            a.isPrimary = a.id === account.id
          })
        return { status: 200, body: account }
      }),
    ),
  ),

  http.get(
    '*/api/client/notifications',
    secured('client.portal', ({ user }) =>
      HttpResponse.json(
        clientNotifications.filter((n) => n.customerId === (user.customerId ?? 'cus-001')),
      ),
    ),
  ),

  http.put(
    '*/api/client/notifications/:id/read',
    secured<{ id: string }>('client.portal', ({ params }) => {
      const item = clientNotifications.find((n) => n.id === params.id)
      if (item) item.isRead = true
      return new HttpResponse(null, { status: 204 })
    }),
  ),

  http.post(
    '*/api/client/notifications/push-token',
    secured('client.portal', () => HttpResponse.json({ registered: true })),
  ),

  http.put(
    '*/api/client/notifications/preferences',
    secured('client.portal', async ({ request }) => {
      const body = await readJson<Record<string, boolean>>(request)
      return HttpResponse.json(body ?? {})
    }),
  ),

  http.get(
    '*/api/client/complaints',
    secured('client.portal', ({ user }) =>
      HttpResponse.json(
        clientComplaints.filter((c) => c.customerId === (user.customerId ?? 'cus-001')),
      ),
    ),
  ),

  http.post(
    '*/api/client/complaints',
    secured('client.portal', async ({ request, user }) => {
      const body = await readJson<{
        type?: 'QUEJA' | 'RECLAMO'
        detail?: string
        requestedSolution?: string
      }>(request)
      const count = clientComplaints.length + 1
      const prefix = body?.type === 'QUEJA' ? 'QUE' : 'REC'
      const created: MockComplaint = {
        id: `cmp-${String(count).padStart(3, '0')}`,
        customerId: user.customerId ?? 'cus-001',
        filingNumber: `${prefix}-2026-${String(count).padStart(4, '0')}`,
        type: body?.type ?? 'RECLAMO',
        detail: body?.detail ?? '',
        requestedSolution: body?.requestedSolution ?? '',
        status: 'RECEIVED',
        createdAt: nowIso(),
      }
      clientComplaints.unshift(created)
      return HttpResponse.json(created, { status: 201 })
    }),
  ),
]
