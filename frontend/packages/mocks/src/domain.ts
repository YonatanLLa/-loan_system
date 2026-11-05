import type {
  AgingBucket,
  AgingRow,
  ApplicationSummary,
  Installment,
  Loan,
  LoanApplication,
  LoanProduct,
  LoanSummary,
  Payment,
  PaymentFrequency,
  PrepaymentQuote,
  Simulation,
} from '@prestamos/api-client'

import { ageOn, addDays, diffDays, today } from './dates'
import {
  db,
  nextId,
  nowIso,
  recordAudit,
  type InstallmentRec,
  type LoanRec,
  type PaymentRec,
} from './db'
import { fromCents, roundHalfUp, sum, toCents, type Cents } from './money'
import { buildSchedule, computeTcea, PERIODS_PER_YEAR, periodRate } from './schedule'

export const holidaySet = (): Set<string> => new Set(db.holidays.map((holiday) => holiday.date))

export const customerName = (customerId: string): string =>
  db.customers.find((customer) => customer.id === customerId)?.fullName ?? 'Cliente'

// ── Simulación y evaluación ──────────────────────────────────────────────────

export function simulateLoan(
  product: LoanProduct,
  amount: Cents,
  term: number,
  frequency: PaymentFrequency,
  firstDueDate?: string,
): Simulation {
  const tea = Number(product.annualRate)
  const periodicFee = toCents(product.periodicFee)
  const rows = buildSchedule({
    principal: amount,
    annualRate: tea,
    term,
    frequency,
    method: product.amortizationMethod,
    startDate: today(),
    firstDueDate,
    periodicFee,
    holidays: holidaySet(),
  })
  const openingFee = roundHalfUp(amount * Number(product.openingFeeRate))
  const net = product.openingFeeMode === 'DEDUCTIVA' ? amount - openingFee : amount
  const totalInterest = sum(rows.map((row) => row.interest))
  const totalFees = sum(rows.map((row) => row.fee)) + openingFee

  return {
    productId: product.id,
    summary: {
      tea: tea.toFixed(6),
      tem: periodRate(tea, 'MENSUAL').toFixed(6),
      tcea: computeTcea(net, rows, frequency).toFixed(6),
      totalPrincipal: fromCents(amount),
      totalInterest: fromCents(totalInterest),
      totalFees: fromCents(totalFees),
      totalCost: fromCents(amount + totalInterest + totalFees),
      installmentAmount: fromCents(rows[0]?.totalDue ?? 0),
    },
    installments: rows.map((row) => ({
      number: row.number,
      dueDate: row.dueDate,
      principal: fromCents(row.principal),
      interest: fromCents(row.interest),
      fee: fromCents(row.fee),
      totalDue: fromCents(row.totalDue),
      balance: fromCents(row.balance),
    })),
  }
}

/** Validaciones de admisión (RF-10.2). Devuelve errores por campo en formato ProblemDetails. */
export function validateAdmission(
  customerId: string | undefined,
  product: LoanProduct,
  amount: Cents,
  term: number,
  frequency: PaymentFrequency,
): Record<string, string[]> {
  const errors: Record<string, string[]> = {}
  if (amount < toCents(product.minAmount) || amount > toCents(product.maxAmount)) {
    errors.Amount = [
      `El monto debe estar entre ${product.minAmount} y ${product.maxAmount} ${product.currency}`,
    ]
  }
  if (term < product.minTerm || term > product.maxTerm) {
    errors.Term = [`El plazo debe estar entre ${product.minTerm} y ${product.maxTerm} cuotas`]
  }
  if (!product.allowedFrequencies.includes(frequency)) {
    errors.Frequency = ['El producto no admite esa frecuencia de pago']
  }
  const customer = customerId ? db.customers.find((c) => c.id === customerId) : undefined
  if (customer) {
    const age = ageOn(customer.birthDate, today())
    if (age < product.minAge || age > product.maxAge) {
      errors.CustomerId = [`La edad del cliente debe estar entre ${product.minAge} y ${product.maxAge} años`]
    }
  }
  return errors
}

export function customerLoans(customerId: string): LoanRec[] {
  return db.loans.filter((loan) => loan.customerId === customerId)
}

/** DTI (RF-12.1): (cuota mensual proyectada + otras cuotas activas) / ingreso neto × 100. */
export function evaluateDti(
  customerId: string,
  product: LoanProduct,
  amount: Cents,
  term: number,
  frequency: PaymentFrequency,
): { dti: string; recommendation: LoanApplication['systemRecommendation'] } {
  const customer = db.customers.find((c) => c.id === customerId)
  const income = toCents(customer?.monthlyNetIncome ?? '0')
  const simulation = simulateLoan(product, amount, term, frequency)
  const monthlyFactor = PERIODS_PER_YEAR[frequency] / 12
  const projected = toCents(simulation.summary.installmentAmount) * monthlyFactor

  const others = customerLoans(customerId)
    .filter((loan) => loan.status === 'VIGENTE' || loan.status === 'EN_MORA')
    .map((loan) => {
      const next = activeInstallments(loan).find((inst) => inst.status !== 'PAID')
      return next ? next.totalDue * (PERIODS_PER_YEAR[loan.frequency] / 12) : 0
    })

  const dti = income === 0 ? 100 : ((projected + sum(others)) / income) * 100
  const hasArrears = customerLoans(customerId).some((loan) => loan.status === 'EN_MORA')
  const recommendation =
    hasArrears || dti > Number(product.maxDtiManual) * 100
      ? 'RECHAZO_SUGERIDO'
      : dti > Number(product.maxDtiAuto) * 100
        ? 'REVISION_MANUAL'
        : 'APROBABLE'
  return { dti: dti.toFixed(4), recommendation }
}

// ── Cronograma, mora y estados ───────────────────────────────────────────────

export function activeInstallments(loan: LoanRec): InstallmentRec[] {
  return db.installments
    .filter((inst) => inst.loanId === loan.id && inst.scheduleVersion === loan.scheduleVersion)
    .sort((a, b) => a.number - b.number)
}

const isSettled = (inst: InstallmentRec): boolean =>
  inst.paidPrincipal >= inst.principal &&
  inst.paidInterest >= inst.interest &&
  inst.paidFee >= inst.fee &&
  inst.paidLateFee >= inst.lateFee

export function installmentStatus(inst: InstallmentRec, asOf: string): InstallmentRec['status'] {
  if (inst.status === 'CANCELLED_BY_PREPAYMENT') return inst.status
  if (isSettled(inst)) return 'PAID'
  if (inst.paidPrincipal + inst.paidInterest + inst.paidFee + inst.paidLateFee > 0) return 'PARTIAL'
  return inst.dueDate < asOf ? 'OVERDUE' : 'PENDING'
}

/** Mora diaria (US-19): idempotente; recalcula mora y estados a la fecha indicada. */
export function refreshLoan(loan: LoanRec, asOf: string = today()): void {
  if (loan.status === 'PENDIENTE_DESEMBOLSO' || loan.status === 'CASTIGADO') return
  for (const inst of activeInstallments(loan)) {
    if (inst.status !== 'CANCELLED_BY_PREPAYMENT' && !isSettled(inst) && inst.dueDate < asOf) {
      const daysLate = diffDays(inst.dueDate, asOf)
      if (daysLate > loan.graceDays) {
        const base =
          loan.moraBase === 'SALDO_CAPITAL_VENCIDO'
            ? inst.principal - inst.paidPrincipal
            : inst.totalDue - inst.paidPrincipal - inst.paidInterest - inst.paidFee
        inst.lateFee = Math.max(inst.lateFee, roundHalfUp(base * loan.moraRatePerDay * daysLate))
      }
    }
    inst.status = installmentStatus(inst, asOf)
  }
  updateLoanStatus(loan, asOf)
}

function updateLoanStatus(loan: LoanRec, asOf: string): void {
  if (loan.status === 'PAGADO' && !activeInstallments(loan).every(isSettledOrCancelled)) {
    loan.status = 'VIGENTE'
  }
  if (loan.status === 'CANCELADO' || loan.status === 'CASTIGADO') return
  const installments = activeInstallments(loan)
  if (installments.length > 0 && installments.every(isSettledOrCancelled)) {
    loan.status = 'PAGADO'
    return
  }
  loan.status = installments.some(
    (inst) => !isSettledOrCancelled(inst) && inst.dueDate < asOf && diffDays(inst.dueDate, asOf) > loan.graceDays,
  )
    ? 'EN_MORA'
    : 'VIGENTE'
}

export const isSettledOrCancelled = (inst: InstallmentRec): boolean =>
  inst.status === 'CANCELLED_BY_PREPAYMENT' || isSettled(inst)

/** Genera el cronograma inmutable al confirmarse el desembolso (RF-13.4.1). */
export function activateLoan(loan: LoanRec, startDate: string): void {
  const rows = buildSchedule({
    principal: loan.principal,
    annualRate: loan.annualRate,
    term: loan.term,
    frequency: loan.frequency,
    method: loan.amortizationMethod,
    startDate,
    periodicFee: loan.periodicFee,
    holidays: holidaySet(),
  })
  db.installments = db.installments.filter(
    (inst) => !(inst.loanId === loan.id && inst.scheduleVersion === loan.scheduleVersion),
  )
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
  loan.disbursementDate = startDate
  loan.firstDueDate = rows[0]?.dueDate
  loan.status = 'VIGENTE'
  const application = db.applications.find((app) => app.id === loan.applicationId)
  if (application) application.status = 'DESEMBOLSADO'
  recordAudit({
    entityName: 'loans',
    entityId: loan.id,
    action: 'DISBURSE',
    before: { status: 'PENDIENTE_DESEMBOLSO' },
    after: { status: 'VIGENTE', disbursementDate: startDate },
  })
}

// ── Pagos ────────────────────────────────────────────────────────────────────

/** Prelación estricta por cuota más antigua: mora → interés → comisiones → capital (RF-16.4). */
export function applyPaymentCascade(payment: PaymentRec, asOf: string = today()): void {
  const loan = db.loans.find((l) => l.id === payment.loanId)
  if (!loan) return
  refreshLoan(loan, asOf)

  let remaining = payment.amount
  payment.allocations = []
  for (const inst of activeInstallments(loan)) {
    if (remaining === 0) break
    if (isSettledOrCancelled(inst)) continue
    const toMora = Math.min(remaining, inst.lateFee - inst.paidLateFee)
    remaining -= toMora
    const toInterest = Math.min(remaining, inst.interest - inst.paidInterest)
    remaining -= toInterest
    const toFee = Math.min(remaining, inst.fee - inst.paidFee)
    remaining -= toFee
    const toPrincipal = Math.min(remaining, inst.principal - inst.paidPrincipal)
    remaining -= toPrincipal
    if (toMora + toInterest + toFee + toPrincipal === 0) continue

    inst.paidLateFee += toMora
    inst.paidInterest += toInterest
    inst.paidFee += toFee
    inst.paidPrincipal += toPrincipal
    inst.status = installmentStatus(inst, asOf)
    payment.allocations.push({
      installmentId: inst.id,
      installmentNumber: inst.number,
      toMora,
      toInterest,
      toFee,
      toPrincipal,
    })
  }

  payment.creditToCustomer = remaining
  if (remaining > 0) {
    const customer = db.customers.find((c) => c.id === loan.customerId)
    if (customer) customer.creditBalance = fromCents(toCents(customer.creditBalance) + remaining)
  }
  payment.status = 'APPLIED'
  payment.receiptNumber ??= nextReceipt(asOf)
  updateLoanStatus(loan, asOf)
  recordAudit({
    entityName: 'payments',
    entityId: payment.id,
    action: 'CREATE',
    after: { amount: fromCents(payment.amount), status: 'APPLIED', loanId: payment.loanId },
  })
}

export function nextReceipt(date: string = today()): string {
  const n = (db.counters.get('receipt') ?? 0) + 1
  db.counters.set('receipt', n)
  return `REC-${date.replaceAll('-', '')}-${String(n).padStart(6, '0')}`
}

/** Reverso con contra-asiento (US-18): nunca se edita ni borra el pago original. */
export function reversePayment(original: PaymentRec, reason: string): PaymentRec {
  const loan = db.loans.find((l) => l.id === original.loanId)
  for (const alloc of original.allocations) {
    const inst = db.installments.find((i) => i.id === alloc.installmentId)
    if (!inst) continue
    inst.paidLateFee -= alloc.toMora
    inst.paidInterest -= alloc.toInterest
    inst.paidFee -= alloc.toFee
    inst.paidPrincipal -= alloc.toPrincipal
    inst.status = installmentStatus(inst, today())
  }
  if (original.creditToCustomer > 0 && loan) {
    const customer = db.customers.find((c) => c.id === loan.customerId)
    if (customer) {
      customer.creditBalance = fromCents(
        Math.max(0, toCents(customer.creditBalance) - original.creditToCustomer),
      )
    }
  }
  original.status = 'REVERSED'
  if (loan) updateLoanStatus(loan, today())

  const reversal: PaymentRec = {
    id: nextId('pay'),
    loanId: original.loanId,
    kind: 'REVERSAL',
    amount: original.amount,
    method: original.method,
    status: 'APPLIED',
    reversalOfId: original.id,
    reversalReason: reason,
    paidAt: nowIso(),
    createdAt: nowIso(),
    allocations: [],
    creditToCustomer: 0,
  }
  db.payments.unshift(reversal)
  recordAudit({
    entityName: 'payments',
    entityId: original.id,
    action: 'REVERSE',
    before: { status: 'APPLIED' },
    after: { status: 'REVERSED', reason },
  })
  return reversal
}

// ── Procesos que avanzan con el tiempo (simulan banco y webhooks) ────────────

const STAGE_PROCESSING_MS = 2_000
const STAGE_DONE_MS = 6_000

export function tickDisbursements(): void {
  for (const d of db.disbursements) {
    if (d.status !== 'PENDING' && d.status !== 'PROCESSING') continue
    const loan = db.loans.find((l) => l.id === d.loanId)
    const age = Date.now() - d.createdMs
    if (age >= STAGE_DONE_MS && loan) {
      const account = db.customerBankAccounts.find((a) => a.id === d.customerBankAccountId)
      if (account?.cci?.endsWith('0000')) {
        d.status = 'FAILED'
        d.failedAt = nowIso()
        d.bankResponseCode = '14'
        d.bankResponseMessage = 'Cuenta destino inválida o inexistente'
        recordAudit({ entityName: 'disbursements', entityId: d.id, action: 'FAIL', after: { status: 'FAILED' } })
      } else {
        d.status = 'COMPLETED'
        d.confirmedAt = nowIso()
        d.operationNumber = `OP-${Math.floor(100000 + Math.random() * 899999)}`
        activateLoan(loan, today())
      }
    } else if (age >= STAGE_PROCESSING_MS && d.status === 'PENDING') {
      d.status = 'PROCESSING'
    }
  }
}

/** Pagos con webhook (QR, tarjeta): se confirman solos al llegar `confirmAt`. */
export function tickPayments(): void {
  for (const payment of db.payments) {
    if (payment.status === 'PENDING_CONFIRMATION' && payment.confirmAt && payment.confirmAt <= Date.now()) {
      payment.gatewayReference ??= `GW-${Math.floor(100000 + Math.random() * 899999)}`
      applyPaymentCascade(payment)
    }
  }
}

export function tickAll(): void {
  tickDisbursements()
  tickPayments()
  for (const loan of db.loans) refreshLoan(loan)
}

// ── Mapeo a DTO ──────────────────────────────────────────────────────────────

export function outstandingPrincipal(loan: LoanRec): Cents {
  const installments = activeInstallments(loan)
  if (installments.length === 0) return loan.status === 'PENDIENTE_DESEMBOLSO' ? loan.principal : 0
  return sum(
    installments
      .filter((inst) => inst.status !== 'CANCELLED_BY_PREPAYMENT')
      .map((inst) => inst.principal - inst.paidPrincipal),
  )
}

export function nextDueDate(loan: LoanRec): string | undefined {
  return activeInstallments(loan).find((inst) => !isSettledOrCancelled(inst))?.dueDate
}

export function maxDaysLate(loan: LoanRec, asOf: string = today()): number {
  return activeInstallments(loan)
    .filter((inst) => !isSettledOrCancelled(inst) && inst.dueDate < asOf)
    .reduce((max, inst) => Math.max(max, diffDays(inst.dueDate, asOf)), 0)
}

export function toLoanDto(loan: LoanRec): Loan {
  const product = db.products.find((p) => p.id === loan.productId)
  const paid = sum(
    db.payments
      .filter((p) => p.loanId === loan.id && p.kind === 'NORMAL' && p.status === 'APPLIED')
      .map((p) => p.amount),
  )
  return {
    id: loan.id,
    applicationId: loan.applicationId,
    customerId: loan.customerId,
    customerName: customerName(loan.customerId),
    productId: loan.productId,
    productName: product?.name ?? '',
    principal: fromCents(loan.principal),
    disbursedAmount: fromCents(loan.disbursed),
    annualRate: loan.annualRate.toFixed(6),
    amortizationMethod: loan.amortizationMethod,
    frequency: loan.frequency,
    term: loan.term,
    graceDays: loan.graceDays,
    moraRatePerDay: loan.moraRatePerDay.toFixed(6),
    disbursementDate: loan.disbursementDate,
    firstDueDate: loan.firstDueDate,
    status: loan.status,
    outstandingPrincipal: fromCents(outstandingPrincipal(loan)),
    totalPaid: fromCents(paid),
    scheduleVersion: loan.scheduleVersion,
    disbursementId: loan.disbursementId,
    createdAt: loan.createdAt,
    assignedTo: loan.assignedTo,
  }
}

export function toLoanSummary(loan: LoanRec): LoanSummary {
  const product = db.products.find((p) => p.id === loan.productId)
  return {
    id: loan.id,
    productName: product?.name ?? '',
    principal: fromCents(loan.principal),
    outstandingPrincipal: fromCents(outstandingPrincipal(loan)),
    status: loan.status,
    nextDueDate: nextDueDate(loan),
    daysLate: maxDaysLate(loan),
  }
}

export function toInstallmentDto(inst: InstallmentRec): Installment {
  const loan = db.loans.find((l) => l.id === inst.loanId)
  // Saldo de capital después de esta cuota (vale también para cronogramas reestructurados).
  const balance = loan
    ? sum(activeInstallments(loan).filter((other) => other.number > inst.number).map((other) => other.principal))
    : 0
  const daysLate =
    inst.status === 'PAID' || inst.status === 'CANCELLED_BY_PREPAYMENT' || inst.dueDate >= today()
      ? 0
      : diffDays(inst.dueDate, today())
  return {
    number: inst.number,
    dueDate: inst.dueDate,
    principal: fromCents(inst.principal),
    interest: fromCents(inst.interest),
    fee: fromCents(inst.fee),
    totalDue: fromCents(inst.totalDue),
    balance: fromCents(balance),
    paidPrincipal: fromCents(inst.paidPrincipal),
    paidInterest: fromCents(inst.paidInterest),
    paidFee: fromCents(inst.paidFee),
    lateFee: fromCents(inst.lateFee),
    paidLateFee: fromCents(inst.paidLateFee),
    status: inst.status,
    daysLate,
  }
}

export function toPaymentDto(payment: PaymentRec): Payment {
  const loan = db.loans.find((l) => l.id === payment.loanId)
  return {
    id: payment.id,
    loanId: payment.loanId,
    customerName: loan ? customerName(loan.customerId) : '',
    kind: payment.kind,
    amount: fromCents(payment.amount),
    method: payment.method,
    status: payment.status,
    gatewayReference: payment.gatewayReference,
    externalReference: payment.externalReference,
    receiptNumber: payment.receiptNumber,
    reversalOfId: payment.reversalOfId,
    reversalReason: payment.reversalReason,
    paidAt: payment.paidAt,
    allocations: payment.allocations.map((alloc) => ({
      installmentId: alloc.installmentId,
      installmentNumber: alloc.installmentNumber,
      toMora: fromCents(alloc.toMora),
      toInterest: fromCents(alloc.toInterest),
      toFee: fromCents(alloc.toFee),
      toPrincipal: fromCents(alloc.toPrincipal),
    })),
    createdAt: payment.createdAt,
  }
}

export function toApplicationSummary(app: LoanApplication): ApplicationSummary {
  return {
    id: app.id,
    productName: app.productName,
    requestedAmount: app.requestedAmount,
    status: app.status,
    createdAt: app.createdAt,
  }
}

// ── Cobranza y prepago ───────────────────────────────────────────────────────

/** Marca como incumplidas las promesas cuya fecha ya pasó y siguen activas. */
export function refreshBrokenPromises(asOf: string = today()): void {
  for (const action of db.actions) {
    if (
      action.actionType === 'PROMESA_DE_PAGO' &&
      action.status === 'ACTIVA' &&
      action.promiseDate &&
      action.promiseDate < asOf
    ) {
      action.status = 'PROMESA_INCUMPLIDA'
    }
  }
}

export function loanHasBrokenPromise(loanId: string): boolean {
  return db.actions.some(
    (action) =>
      action.loanId === loanId &&
      action.actionType === 'PROMESA_DE_PAGO' &&
      action.status === 'PROMESA_INCUMPLIDA',
  )
}

export function agingBucket(daysLate: number): AgingBucket {
  if (daysLate <= 0) return 'PREVENTIVO'
  if (daysLate <= 30) return 'TRAMO_1'
  if (daysLate <= 60) return 'TRAMO_2'
  if (daysLate <= 90) return 'TRAMO_3'
  return 'TRAMO_4'
}

export function buildAging(): AgingRow[] {
  refreshBrokenPromises()
  const rows: AgingRow[] = []
  for (const loan of db.loans) {
    if (loan.status !== 'VIGENTE' && loan.status !== 'EN_MORA') continue
    const overdue = activeInstallments(loan).filter(
      (inst) => !isSettledOrCancelled(inst) && inst.dueDate < today(),
    )
    const customer = db.customers.find((c) => c.id === loan.customerId)
    if (overdue.length > 0) {
      const daysLate = maxDaysLate(loan)
      rows.push({
        loanId: loan.id,
        customerId: loan.customerId,
        customerName: customer?.fullName ?? '',
        phone: customer?.phone ?? '',
        productName: db.products.find((p) => p.id === loan.productId)?.name ?? '',
        bucket: agingBucket(daysLate),
        daysLate,
        overdueInstallments: overdue.length,
        overduePrincipal: fromCents(sum(overdue.map((i) => i.principal - i.paidPrincipal))),
        totalDue: fromCents(
          sum(
            overdue.map(
              (i) => i.totalDue + i.lateFee - i.paidPrincipal - i.paidInterest - i.paidFee - i.paidLateFee,
            ),
          ),
        ),
        assignedTo: loan.assignedTo,
        hasBrokenPromise: loanHasBrokenPromise(loan.id),
      })
    } else {
      const next = activeInstallments(loan).find((inst) => !isSettledOrCancelled(inst))
      if (next && diffDays(today(), next.dueDate) <= 5) {
        rows.push({
          loanId: loan.id,
          customerId: loan.customerId,
          customerName: customer?.fullName ?? '',
          phone: customer?.phone ?? '',
          productName: db.products.find((p) => p.id === loan.productId)?.name ?? '',
          bucket: 'PREVENTIVO',
          daysLate: 0,
          overdueInstallments: 0,
          overduePrincipal: '0.00',
          totalDue: fromCents(next.totalDue - next.paidPrincipal - next.paidInterest - next.paidFee),
          assignedTo: loan.assignedTo,
          hasBrokenPromise: loanHasBrokenPromise(loan.id),
        })
      }
    }
  }
  return rows.sort((a, b) => toCents(b.totalDue) - toCents(a.totalDue))
}

export function quotePrepayment(loan: LoanRec, asOf: string): PrepaymentQuote {
  refreshLoan(loan)
  const installments = activeInstallments(loan)
  const unpaid = installments.filter((inst) => !isSettledOrCancelled(inst))
  const principal = outstandingPrincipal(loan)
  const lastDue = [...installments].reverse().find((inst) => inst.dueDate <= asOf)
  const accruedDays = lastDue ? Math.max(0, diffDays(lastDue.dueDate, asOf)) : 0
  const dailyRate = periodRate(loan.annualRate, 'DIARIA')
  const overdueInterest = sum(
    unpaid.filter((inst) => inst.dueDate <= asOf).map((inst) => inst.interest - inst.paidInterest),
  )
  const accrued = overdueInterest + roundHalfUp(principal * dailyRate * accruedDays)
  const mora = sum(unpaid.map((inst) => inst.lateFee - inst.paidLateFee))
  const penalty = roundHalfUp(principal * loan.prepaymentPenaltyRate)
  return {
    loanId: loan.id,
    asOfDate: asOf,
    outstandingPrincipal: fromCents(principal),
    accruedInterest: fromCents(accrued),
    accruedMora: fromCents(mora),
    prepaymentPenalty: fromCents(penalty),
    totalToPay: fromCents(principal + accrued + mora + penalty),
    validUntil: addDays(asOf, 0),
  }
}
