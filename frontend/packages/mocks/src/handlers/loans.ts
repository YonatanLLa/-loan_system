import { http, HttpResponse } from 'msw'
import type { CreateLoanRequest } from '@prestamos/api-client'

import { today } from '../dates'
import { db, nextId, nowIso, recordAudit } from '../db'
import {
  activeInstallments,
  customerName,
  holidaySet,
  maxDaysLate,
  outstandingPrincipal,
  quotePrepayment,
  refreshLoan,
  toInstallmentDto,
  toLoanDto,
  toPaymentDto,
} from '../domain'
import { createLoanRecord } from '../seed'
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
import { fromCents } from '../money'
import { buildSchedule } from '../schedule'

export const loanHandlers = [
  http.get('*/api/loans', secured('loans.read', ({ url }) => {
    const status = url.searchParams.get('status')
    const q = url.searchParams.get('q')
    const customerId = url.searchParams.get('customerId')
    const items = db.loans
      .map(toLoanDto)
      .filter((l) => !status || status.split(',').includes(l.status))
      .filter((l) => !customerId || l.customerId === customerId)
      .filter((l) => matches(`${l.customerName} ${l.productName} ${l.id}`, q))
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    return HttpResponse.json(paginate(items, url))
  })),

  http.get('*/api/loans/:id', secured<{ id: string }>('loans.read', ({ params }) => {
    const loan = db.loans.find((l) => l.id === params.id)
    return loan ? HttpResponse.json(toLoanDto(loan)) : notFound('El préstamo')
  })),

  http.get('*/api/loans/:id/schedule', secured<{ id: string }>('loans.read', ({ params, url }) => {
    const loan = db.loans.find((l) => l.id === params.id)
    if (!loan) return notFound('El préstamo')
    const version = Number(url.searchParams.get('version') ?? loan.scheduleVersion)
    const installments = db.installments
      .filter((i) => i.loanId === loan.id && i.scheduleVersion === version)
      .sort((a, b) => a.number - b.number)
      .map(toInstallmentDto)
    return HttpResponse.json({ loanId: loan.id, version, installments })
  })),

  http.get('*/api/loans/:id/payments', secured<{ id: string }>('payments.read', ({ params }) =>
    HttpResponse.json(db.payments.filter((p) => p.loanId === params.id).map(toPaymentDto)),
  )),

  // Desembolso digital (US-13): crea el préstamo y la transferencia PENDING; el banco la confirma después.
  http.post('*/api/loans', secured('loans.read', async ({ request, user }) => {
    const body = await readJson<CreateLoanRequest>(request)
    return idempotent(request, () => {
      if (!body?.applicationId) return problem(400, 'Solicitud inválida', 'Indica la solicitud aprobada.')
      const application = db.applications.find((a) => a.id === body.applicationId)
      if (!application) return notFound('La solicitud')
      if (!['APROBADO', 'APROBADO_CON_CONDICIONES'].includes(application.status)) {
        return problem(422, 'Solicitud no aprobada', 'La solicitud debe estar aprobada para desembolsar.')
      }
      if (db.loans.some((l) => l.applicationId === application.id)) {
        return problem(409, 'Desembolso duplicado', 'Esa solicitud ya tiene un préstamo creado.')
      }
      const customer = db.customers.find((c) => c.id === application.customerId)
      if (customer?.kycStatus !== 'VERIFIED') {
        return problem(422, 'KYC pendiente', 'Cliente con KYC pendiente.')
      }
      const customerAccount = db.customerBankAccounts.find((a) => a.id === body.customerBankAccountId)
      if (!customerAccount || customerAccount.customerId !== application.customerId || !customerAccount.isVerified) {
        return problem(422, 'Cuenta destino no verificada', 'La cuenta destino debe pertenecer al cliente y estar verificada.')
      }
      const companyAccount = db.companyAccounts.find((a) => a.id === body.companyBankAccountId)
      if (!companyAccount?.isActive || !companyAccount.isDisbursement) {
        return problem(422, 'Cuenta de origen no habilitada', 'La cuenta de origen no está habilitada para desembolsos.')
      }

      const loan = createLoanRecord(application, customerAccount.id)
      const disbursementId = nextId('dis')
      loan.disbursementId = disbursementId
      const disbursement = {
        id: disbursementId,
        loanId: loan.id,
        customerName: customerName(loan.customerId),
        amount: fromCents(loan.disbursed),
        channel: 'TRANSFERENCIA_BANCARIA' as const,
        status: 'PENDING' as const,
        companyBankAccountId: companyAccount.id,
        customerBankAccountId: customerAccount.id,
        initiatedAt: nowIso(),
        createdMs: Date.now(),
      }
      db.disbursements.unshift(disbursement)
      recordAudit({ entityName: 'loans', entityId: loan.id, action: 'DISBURSE', after: { status: 'PENDIENTE_DESEMBOLSO' }, userName: user.fullName })
      return { status: 201, body: { loan: toLoanDto(loan), disbursement } }
    })
  })),

  http.post('*/api/loans/:id/quote-prepayment', secured<{ id: string }>('loans.read', async ({ request, params }) => {
    const loan = db.loans.find((l) => l.id === params.id)
    if (!loan) return notFound('El préstamo')
    if (loan.status !== 'VIGENTE' && loan.status !== 'EN_MORA') {
      return problem(422, 'Préstamo no cancelable', 'Solo se cotiza la cancelación de préstamos vigentes o en mora.')
    }
    const body = await readJson<{ asOfDate?: string }>(request)
    return HttpResponse.json(quotePrepayment(loan, body?.asOfDate ?? today()))
  })),

  // Reestructuración (US-31): nueva versión del cronograma sobre el saldo de capital.
  http.post('*/api/loans/:id/restructure', secured<{ id: string }>('loans.read', async ({ request, params, user }) => {
    const loan = db.loans.find((l) => l.id === params.id)
    if (!loan) return notFound('El préstamo')
    const body = await readJson<{ term: number; annualRate?: string; graceDays?: number }>(request)
    if (!body?.term || body.term < 1) return problem(400, 'Datos inválidos', 'Indica el nuevo plazo.', { Term: ['El plazo es obligatorio'] })
    if (loan.status !== 'VIGENTE' && loan.status !== 'EN_MORA') {
      return problem(422, 'Préstamo no reestructurable', 'Solo se reestructuran préstamos vigentes o en mora.')
    }
    refreshLoan(loan)
    if (maxDaysLate(loan) > 60) {
      return problem(422, 'Mora excesiva', 'No se puede reestructurar con más de 60 días de mora.')
    }
    const outstanding = outstandingPrincipal(loan)
    const rate = body.annualRate ? Number(body.annualRate) : loan.annualRate
    if (typeof body.graceDays === 'number' && body.graceDays >= 0) {
      loan.graceDays = body.graceDays
    }
    const rows = buildSchedule({
      principal: outstanding,
      annualRate: rate,
      term: body.term,
      frequency: loan.frequency,
      method: loan.amortizationMethod,
      startDate: today(),
      periodicFee: loan.periodicFee,
      holidays: holidaySet(),
    })
    const before = { scheduleVersion: loan.scheduleVersion }
    for (const inst of activeInstallments(loan)) {
      if (inst.status !== 'PAID') inst.status = 'CANCELLED_BY_PREPAYMENT'
    }
    loan.scheduleVersion += 1
    loan.term = body.term
    loan.annualRate = rate
    for (const row of rows) {
      db.installments.push({
        id: `${loan.id}-v${loan.scheduleVersion}-${String(row.number).padStart(2, '0')}`,
        loanId: loan.id,
        scheduleVersion: loan.scheduleVersion,
        number: row.number,
        dueDate: row.dueDate,
        principal: row.principal,
        interest: row.interest,
        fee: row.fee,
        totalDue: row.totalDue,
        paidPrincipal: 0,
        paidInterest: 0,
        paidFee: 0,
        lateFee: 0,
        paidLateFee: 0,
        status: 'PENDING',
      })
    }
    loan.status = 'VIGENTE'
    recordAudit({ entityName: 'loans', entityId: loan.id, action: 'RESTRUCTURE', before, after: { scheduleVersion: loan.scheduleVersion, term: body.term, graceDays: loan.graceDays }, userName: user.fullName })
    return HttpResponse.json(toLoanDto(loan))
  })),

  // Refinanciamiento y consolidación de varios préstamos (US-32 / FE-BO-26).
  http.post('*/api/loans/refinance', secured('loans.read', async ({ request, user }) => {
    const body = await readJson<{
      customerId?: string
      loanIds: string[]
      term: number
      annualRate?: string
      graceDays?: number
    }>(request)
    if (!body || !Array.isArray(body.loanIds) || body.loanIds.length === 0 || !body.term || body.term < 1) {
      return problem(400, 'Datos inválidos', 'Indica los préstamos a consolidar y el nuevo plazo.')
    }
    const targetLoans = db.loans.filter(
      (l) => body.loanIds.includes(l.id) && (!body.customerId || l.customerId === body.customerId),
    )
    if (targetLoans.length !== body.loanIds.length) {
      return notFound('Alguno de los préstamos seleccionados')
    }
    if (targetLoans.some((l) => l.status !== 'VIGENTE' && l.status !== 'EN_MORA')) {
      return problem(422, 'Préstamo no refinanciable', 'Solo se refinancian préstamos vigentes o en mora.')
    }

    let consolidatedPrincipal = 0
    for (const oldLoan of targetLoans) {
      refreshLoan(oldLoan)
      consolidatedPrincipal += outstandingPrincipal(oldLoan)
      for (const inst of activeInstallments(oldLoan)) {
        if (inst.status !== 'PAID') inst.status = 'CANCELLED_BY_PREPAYMENT'
      }
      oldLoan.status = 'PAGADO'
    }

    const baseLoan = targetLoans[0]!
    const rate = body.annualRate ? Number(body.annualRate) : baseLoan.annualRate
    const newLoanId = nextId('loan')
    const newLoan = {
      id: newLoanId,
      applicationId: baseLoan.applicationId,
      customerId: baseLoan.customerId,
      productId: baseLoan.productId,
      customerBankAccountId: baseLoan.customerBankAccountId,
      principal: consolidatedPrincipal,
      disbursed: consolidatedPrincipal,
      annualRate: rate,
      amortizationMethod: baseLoan.amortizationMethod,
      frequency: baseLoan.frequency,
      term: body.term,
      graceDays: body.graceDays ?? baseLoan.graceDays,
      periodicFee: baseLoan.periodicFee,
      moraRatePerDay: baseLoan.moraRatePerDay,
      moraBase: baseLoan.moraBase,
      prepaymentPenaltyRate: baseLoan.prepaymentPenaltyRate,
      status: 'VIGENTE' as const,
      scheduleVersion: 1,
      createdAt: nowIso(),
    }
    db.loans.unshift(newLoan)

    const rows = buildSchedule({
      principal: consolidatedPrincipal,
      annualRate: rate,
      term: body.term,
      frequency: newLoan.frequency,
      method: newLoan.amortizationMethod,
      startDate: today(),
      periodicFee: newLoan.periodicFee,
      holidays: holidaySet(),
    })
    for (const row of rows) {
      db.installments.push({
        id: `${newLoan.id}-v1-${String(row.number).padStart(2, '0')}`,
        loanId: newLoan.id,
        scheduleVersion: 1,
        number: row.number,
        dueDate: row.dueDate,
        principal: row.principal,
        interest: row.interest,
        fee: row.fee,
        totalDue: row.totalDue,
        paidPrincipal: 0,
        paidInterest: 0,
        paidFee: 0,
        lateFee: 0,
        paidLateFee: 0,
        status: 'PENDING',
      })
    }

    recordAudit({
      entityName: 'loans',
      entityId: newLoan.id,
      action: 'CREATE',
      after: { refinancedFrom: body.loanIds, principal: fromCents(consolidatedPrincipal), term: body.term },
      userName: user.fullName,
    })
    return HttpResponse.json(
      {
        newLoan: toLoanDto(newLoan),
        consolidatedLoans: body.loanIds,
      },
      { status: 201 },
    )
  })),

  http.post('*/api/loans/:id/write-off', secured<{ id: string }>('collections.read', async ({ request, params, user }) => {
    const loan = db.loans.find((l) => l.id === params.id)
    if (!loan) return notFound('El préstamo')
    refreshLoan(loan)
    if (loan.status === 'CASTIGADO') {
      return problem(409, 'Préstamo ya castigado', 'Este préstamo ya se encuentra en estado CASTIGADO.')
    }
    const body = await readJson<{ reason?: string; notes?: string; minuteRef?: string; approvedBy?: string }>(request)
    const reason = (body?.reason ?? body?.notes ?? '').trim()
    const minuteRef = body?.minuteRef?.trim() ?? ''
    const approvedBy = body?.approvedBy?.trim() ?? ''
    const errors: Record<string, string[]> = {}
    if (!reason) errors.reason = ['La justificación es obligatoria']
    if (!minuteRef) errors.minuteRef = ['El número de acta de comité es obligatorio']
    if (!approvedBy) errors.approvedBy = ['El segundo usuario aprobador es obligatorio']
    if (Object.keys(errors).length > 0) {
      return problem(422, 'Datos incompletos de castigo', 'Se requiere justificación, acta de comité y un segundo aprobador.', errors)
    }
    const samePerson =
      approvedBy.toLowerCase() === user.fullName.toLowerCase() ||
      approvedBy.toLowerCase() === user.email.toLowerCase()
    if (samePerson) {
      return problem(422, 'Aprobación de cuatro ojos', 'El segundo aprobador debe ser una persona distinta de quien registra el castigo.', {
        approvedBy: ['Elige otro usuario'],
      })
    }
    if (loan.status !== 'EN_MORA' || maxDaysLate(loan) <= 180) {
      return problem(422, 'Castigo no permitido', 'Solo se castigan préstamos en mora con más de 180 días de atraso.')
    }
    loan.status = 'CASTIGADO'
    recordAudit({
      entityName: 'loans',
      entityId: loan.id,
      action: 'WRITE_OFF',
      before: { status: 'EN_MORA' },
      after: { status: 'CASTIGADO', reason, minuteRef, approvedBy },
      userName: user.fullName,
    })
    return HttpResponse.json(toLoanDto(loan))
  })),

  http.get('*/api/loans/:id/documents/contract.pdf', secured<{ id: string }>('loans.read', ({ params }) =>
    db.loans.some((l) => l.id === params.id) ? pdfResponse(`Contrato ${params.id}`) : notFound('El préstamo'),
  )),

  // Reintento de un desembolso rechazado (tras corregir la cuenta destino).
  http.post('*/api/disbursements/:id/retry', secured<{ id: string }>('treasury.read', async ({ request, params }) => {
    const disbursement = db.disbursements.find((d) => d.id === params.id)
    if (!disbursement) return notFound('El desembolso')
    if (disbursement.status !== 'FAILED') {
      return problem(409, 'Estado inválido', 'Solo se reintentan desembolsos fallidos.')
    }
    const body = await readJson<{ customerBankAccountId?: string }>(request)
    if (body?.customerBankAccountId) {
      const account = db.customerBankAccounts.find((a) => a.id === body.customerBankAccountId)
      if (!account?.isVerified || account.customerId !== db.loans.find((l) => l.id === disbursement.loanId)?.customerId) {
        return problem(422, 'Cuenta destino no verificada', 'La cuenta debe pertenecer al cliente y estar verificada.')
      }
      disbursement.customerBankAccountId = account.id
    }
    disbursement.status = 'PENDING'
    disbursement.failedAt = undefined
    disbursement.bankResponseCode = undefined
    disbursement.bankResponseMessage = undefined
    disbursement.initiatedAt = nowIso()
    disbursement.createdMs = Date.now()
    return HttpResponse.json(disbursement)
  })),
]
