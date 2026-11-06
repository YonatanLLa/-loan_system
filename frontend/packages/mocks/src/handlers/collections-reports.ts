import { http, HttpResponse } from 'msw'
import type {
  AccountStatement,
  AgingBucket,
  CashflowReport,
  CollectionAction,
  DashboardKpis,
  ReceivableItem,
} from '@prestamos/api-client'

import { addDays, addMonths, diffDays, today } from '../dates'
import { db, nextId, nowIso, recordAudit } from '../db'
import {
  activeInstallments,
  buildAging,
  customerName,
  isSettledOrCancelled,
  nextDueDate,
  outstandingPrincipal,
  refreshBrokenPromises,
  toLoanDto,
} from '../domain'
import { matches, notFound, paginate, problem, readJson, secured } from '../http-utils'
import { fromCents, sum, toCents } from '../money'

const BUCKETS: AgingBucket[] = ['PREVENTIVO', 'TRAMO_1', 'TRAMO_2', 'TRAMO_3', 'TRAMO_4']

function portfolio() {
  const live = db.loans.filter((l) => l.status === 'VIGENTE' || l.status === 'EN_MORA' || l.status === 'CASTIGADO')
  const by = (status: string) => sum(live.filter((l) => l.status === status).map(outstandingPrincipal))
  return { vigente: by('VIGENTE'), enMora: by('EN_MORA'), castigada: by('CASTIGADO') }
}

function dashboard(): DashboardKpis {
  const { vigente, enMora, castigada } = portfolio()
  const total = vigente + enMora + castigada
  const t = today()
  const collectedToday = sum(
    db.payments
      .filter((p) => p.kind === 'NORMAL' && p.status === 'APPLIED' && p.paidAt.startsWith(t))
      .map((p) => p.amount),
  )
  const expectedToday = sum(
    db.loans.flatMap((l) => activeInstallments(l)).filter((i) => i.dueDate === t).map((i) => i.totalDue),
  )
  const todays = db.disbursements.filter((d) => d.status === 'COMPLETED' && d.confirmedAt?.startsWith(t))
  const decided = db.applications.filter((a) => a.status !== 'SOLICITADO' && a.status !== 'EN_EVALUACION' && a.status !== 'ANULADO')
  const approved = decided.filter((a) => a.status !== 'RECHAZADO')

  const aging = buildAging().filter((r) => r.bucket !== 'PREVENTIVO')
  const months = Array.from({ length: 6 }, (_, index) => addMonths(t, index - 5).slice(0, 7))

  return {
    portfolio: {
      total: fromCents(total),
      vigente: fromCents(vigente),
      enMora: fromCents(enMora),
      castigada: fromCents(castigada),
    },
    delinquencyRate: total === 0 ? '0.00' : ((enMora / total) * 100).toFixed(2),
    collections: { expectedToday: fromCents(expectedToday), collectedToday: fromCents(collectedToday) },
    disbursementsToday: { amount: fromCents(sum(todays.map((d) => toCents(d.amount)))), count: todays.length },
    bankBalances: {
      total: fromCents(sum(db.statements.map((s) => toCents(s.amount))) + 25_000_00),
      inTransit: fromCents(
        sum(db.disbursements.filter((d) => d.status === 'PENDING' || d.status === 'PROCESSING').map((d) => toCents(d.amount))),
      ),
    },
    approvalRate: decided.length === 0 ? '0.00' : ((approved.length / decided.length) * 100).toFixed(2),
    agingChart: BUCKETS.filter((b) => b !== 'PREVENTIVO').map((bucket) => ({
      bucket,
      amount: fromCents(sum(aging.filter((r) => r.bucket === bucket).map((r) => toCents(r.overduePrincipal)))),
    })),
    monthly: months.map((month) => ({
      month,
      disbursed: fromCents(
        sum(db.disbursements.filter((d) => d.status === 'COMPLETED' && (d.confirmedAt ?? '').startsWith(month)).map((d) => toCents(d.amount))),
      ),
      collected: fromCents(
        sum(db.payments.filter((p) => p.kind === 'NORMAL' && p.status === 'APPLIED' && p.paidAt.startsWith(month)).map((p) => p.amount)),
      ),
    })),
  }
}

export const collectionsReportHandlers = [
  // Auditoría (FE-BO-05).
  http.get('*/api/audit', secured('audit.read', ({ url }) => {
    const entity = url.searchParams.get('entity')
    const action = url.searchParams.get('action')
    const user = url.searchParams.get('user')
    const from = url.searchParams.get('from')
    const to = url.searchParams.get('to')
    const items = db.audit
      .filter((e) => !entity || e.entityName === entity)
      .filter((e) => !action || e.action === action)
      .filter((e) => matches(e.userName, user))
      .filter((e) => !from || e.createdAt.slice(0, 10) >= from)
      .filter((e) => !to || e.createdAt.slice(0, 10) <= to)
    return HttpResponse.json(paginate(items, url))
  })),

  http.get('*/api/audit/entities', secured('audit.read', () => {
    const entities = [...new Set(db.audit.map((e) => e.entityName))].sort()
    return HttpResponse.json(entities)
  })),

  http.get('*/api/audit/actions', secured('audit.read', () => {
    const actions = [...new Set(db.audit.map((e) => e.action))].sort()
    return HttpResponse.json(actions)
  })),

  // Cobranza (US-20, US-21, US-22).
  http.get('*/api/collections/aging', secured('collections.read', ({ url }) => {
    const bucket = url.searchParams.get('bucket')
    const q = url.searchParams.get('q')
    const assignedTo = url.searchParams.get('assignedTo')
    const productName = url.searchParams.get('productName')
    const minAmount = url.searchParams.get('minAmount')
    const maxAmount = url.searchParams.get('maxAmount')

    const items = buildAging()
      .filter((row) => !bucket || row.bucket === bucket)
      .filter((row) => !assignedTo || row.assignedTo === assignedTo)
      .filter((row) => !productName || row.productName === productName)
      .filter((row) => !minAmount || Number(row.totalDue) >= Number(minAmount))
      .filter((row) => !maxAmount || Number(row.totalDue) <= Number(maxAmount))
      .filter((row) => matches(`${row.customerName} ${row.phone} ${row.loanId}`, q))
    return HttpResponse.json(paginate(items, url))
  })),

  http.get('*/api/collections/collectors', secured('collections.read', () => {
    const staff = db.users
      .filter((u) => u.role === 'ROLE_COLLECTOR' || u.role === 'ROLE_OPERATOR' || u.role === 'ROLE_ADMIN')
      .map((u) => ({ id: u.id, fullName: u.fullName, email: u.email, role: u.role }))
    return HttpResponse.json(staff)
  })),

  http.post('*/api/loans/:id/assign-collector', secured('collections.read', async ({ params, request, user }) => {
    const loan = db.loans.find((l) => l.id === params.id)
    if (!loan) return notFound('El préstamo')
    const body = await readJson<{ collectorName: string }>(request)
    if (!body?.collectorName) return problem(400, 'Datos incompletos', 'Indica el nombre del gestor asignado.')
    loan.assignedTo = body.collectorName
    recordAudit({
      entityName: 'loans',
      entityId: loan.id,
      action: 'UPDATE',
      after: { assignedTo: body.collectorName },
      userName: user.fullName,
    })
    return HttpResponse.json({ success: true, assignedTo: body.collectorName })
  })),

  http.get('*/api/collections/receivables', secured('collections.read', ({ url }) => {
    const q = url.searchParams.get('q')
    const type = url.searchParams.get('type') // 'VENCIDA' | 'PENDIENTE'
    const t = today()
    const rows: ReceivableItem[] = []
    for (const loan of db.loans) {
      if (loan.status !== 'VIGENTE' && loan.status !== 'EN_MORA') continue
      const customer = db.customers.find((c) => c.id === loan.customerId)
      const product = db.products.find((p) => p.id === loan.productId)
      const insts = activeInstallments(loan).filter((i) => !isSettledOrCancelled(i))
      for (const inst of insts) {
        const isOverdue = inst.dueDate < t
        const daysLate = isOverdue ? diffDays(inst.dueDate, t) : 0
        const itemStatus: 'PENDIENTE' | 'VENCIDA' = isOverdue ? 'VENCIDA' : 'PENDIENTE'
        if (type && itemStatus !== type) continue
        const pendingAmount =
          inst.totalDue + inst.lateFee - inst.paidPrincipal - inst.paidInterest - inst.paidFee - inst.paidLateFee
        const principalDue = inst.principal - inst.paidPrincipal
        rows.push({
          loanId: loan.id,
          customerId: loan.customerId,
          customerName: customer?.fullName ?? '',
          phone: customer?.phone ?? '',
          productName: product?.name ?? '',
          installmentNumber: inst.number,
          dueDate: inst.dueDate,
          daysLate,
          amount: fromCents(pendingAmount),
          principalDue: fromCents(principalDue),
          status: itemStatus,
          outstandingPrincipal: fromCents(outstandingPrincipal(loan)),
        })
      }
    }
    rows.sort((a, b) => (b.daysLate !== a.daysLate ? b.daysLate - a.daysLate : a.dueDate.localeCompare(b.dueDate)))
    const filtered = rows.filter((r) =>
      matches(`${r.customerName} ${r.loanId} ${r.phone} ${r.productName}`, q),
    )
    return HttpResponse.json(paginate(filtered, url))
  })),

  http.get('*/api/loans/:id/collection-actions', secured<{ id: string }>('collections.read', ({ params }) => {
    refreshBrokenPromises()
    return HttpResponse.json(
      db.actions.filter((a) => a.loanId === params.id).sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
    )
  })),

  http.post('*/api/collections/actions', secured('collections.read', async ({ request, user }) => {
    const body = await readJson<Omit<CollectionAction, 'id' | 'status' | 'createdAt' | 'createdBy'>>(request)
    if (!body?.loanId || !body.actionType) return problem(400, 'Solicitud inválida', 'Indica el préstamo y el tipo de gestión.')
    if (!db.loans.some((l) => l.id === body.loanId)) return notFound('El préstamo')
    if (body.actionType === 'PROMESA_DE_PAGO' && (!body.promiseAmount || !body.promiseDate)) {
      return problem(422, 'Faltan datos de la promesa', 'Indica el monto y la fecha prometidos.', {
        PromiseAmount: ['El monto es obligatorio'],
        PromiseDate: ['La fecha es obligatoria'],
      })
    }
    const action: CollectionAction = {
      ...body,
      id: nextId('act'),
      status: 'ACTIVA',
      createdBy: user.fullName,
      createdAt: nowIso(),
    }
    db.actions.unshift(action)
    recordAudit({ entityName: 'collection_actions', entityId: action.id, action: 'CREATE', after: { type: action.actionType }, userName: user.fullName })
    return HttpResponse.json(action, { status: 201 })
  })),

  // Reportes (US-33 a US-35).
  http.get('*/api/reports/dashboard-kpis', secured('dashboard.view', () => HttpResponse.json(dashboard()))),

  http.get('*/api/reports/portfolio', secured('reports.read', ({ url }) => {
    const items = db.loans
      .map(toLoanDto)
      .filter((l) => l.status !== 'PENDIENTE_DESEMBOLSO')
      .filter((l) => matches(`${l.customerName} ${l.productName}`, url.searchParams.get('q')))
    return HttpResponse.json(paginate(items, url))
  })),

  http.get('*/api/reports/aging', secured('reports.read', () => {
    const rows = buildAging()
    return HttpResponse.json(
      BUCKETS.map((bucket) => {
        const inBucket = rows.filter((r) => r.bucket === bucket)
        return {
          bucket,
          loans: inBucket.length,
          overduePrincipal: fromCents(sum(inBucket.map((r) => toCents(r.overduePrincipal)))),
          totalDue: fromCents(sum(inBucket.map((r) => toCents(r.totalDue)))),
        }
      }),
    )
  })),

  http.get('*/api/reports/cashflow', secured('reports.read', ({ url }) => {
    const to = url.searchParams.get('to') ?? today()
    const from = url.searchParams.get('from') ?? addDays(to, -30)
    const inRange = (iso: string) => iso.slice(0, 10) >= from && iso.slice(0, 10) <= to
    const allocations = db.payments
      .filter((p) => p.kind === 'NORMAL' && p.status === 'APPLIED' && inRange(p.paidAt))
      .flatMap((p) => p.allocations)
    const principal = sum(allocations.map((a) => a.toPrincipal))
    const interest = sum(allocations.map((a) => a.toInterest))
    const mora = sum(allocations.map((a) => a.toMora))
    const fees = sum(allocations.map((a) => a.toFee))
    const disbursements = sum(
      db.disbursements.filter((d) => d.status === 'COMPLETED' && inRange(d.confirmedAt ?? '')).map((d) => toCents(d.amount)),
    )
    const report: CashflowReport = {
      from,
      to,
      inflows: {
        principal: fromCents(principal),
        interest: fromCents(interest),
        mora: fromCents(mora),
        fees: fromCents(fees),
      },
      outflows: { disbursements: fromCents(disbursements), bankFees: '0.00' },
      net: fromCents(principal + interest + mora + fees - disbursements),
    }
    return HttpResponse.json(report)
  })),

  http.get('*/api/reports/account-statement/:loanId', secured<{ loanId: string }>('reports.read', ({ params }) => {
    const loan = db.loans.find((l) => l.id === params.loanId)
    return loan ? HttpResponse.json(statement(loan.id)) : notFound('El préstamo')
  })),
]

export function statement(loanId: string): AccountStatement {
  const loan = db.loans.find((l) => l.id === loanId)!
  const dto = toLoanDto(loan)
  const disbursement = db.disbursements.find((d) => d.id === loan.disbursementId && d.status === 'COMPLETED')
  const movements: AccountStatement['movements'] = [
    ...(disbursement
      ? [{ date: disbursement.confirmedAt ?? disbursement.initiatedAt, type: 'DESEMBOLSO' as const, amount: disbursement.amount }]
      : []),
    ...db.payments
      .filter((p) => p.loanId === loanId && p.status !== 'PENDING_CONFIRMATION')
      .map((p) => ({
        date: p.paidAt,
        type: p.kind === 'REVERSAL' ? ('REVERSO' as const) : ('PAGO' as const),
        amount: fromCents(p.amount),
        receiptNumber: p.receiptNumber,
      })),
  ].sort((a, b) => a.date.localeCompare(b.date))
  return {
    loanId,
    customerName: customerName(loan.customerId),
    principal: dto.principal,
    disbursementDate: dto.disbursementDate,
    annualRate: dto.annualRate,
    term: dto.term,
    totalPaid: dto.totalPaid,
    outstandingPrincipal: dto.outstandingPrincipal,
    nextDueDate: nextDueDate(loan),
    movements,
  }
}

