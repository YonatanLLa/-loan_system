import type { AmortizationMethod, PaymentFrequency } from '@prestamos/api-client'

import { addDays, addMonths, nextBusinessDay } from './dates'
import { roundHalfUp, sum, type Cents } from './money'

export interface ScheduleInput {
  principal: Cents
  /** TEA como decimal (0.30 = 30 %). */
  annualRate: number
  term: number
  frequency: PaymentFrequency
  method: AmortizationMethod
  /** Fecha desde la que corren los intereses. */
  startDate: string
  firstDueDate?: string
  periodicFee: Cents
  holidays: ReadonlySet<string>
}

export interface PlannedInstallment {
  number: number
  dueDate: string
  principal: Cents
  interest: Cents
  fee: Cents
  totalDue: Cents
  /** Saldo de capital después de la cuota. */
  balance: Cents
}

export const PERIODS_PER_YEAR: Record<PaymentFrequency, number> = {
  MENSUAL: 12,
  QUINCENAL: 24,
  SEMANAL: 52,
  DIARIA: 365,
}

/** Tasa efectiva del periodo a partir de la TEA (RF-08.2). */
export function periodRate(annualRate: number, frequency: PaymentFrequency): number {
  return Math.pow(1 + annualRate, 1 / PERIODS_PER_YEAR[frequency]) - 1
}

function periodDate(start: string, frequency: PaymentFrequency, n: number): string {
  switch (frequency) {
    case 'MENSUAL':
      return addMonths(start, n)
    case 'QUINCENAL':
      return addDays(start, 15 * n)
    case 'SEMANAL':
      return addDays(start, 7 * n)
    case 'DIARIA':
      return addDays(start, n)
  }
}

/**
 * Genera el cronograma. Reglas: cada componente se redondea a 2 decimales (Half-Up) y el
 * residuo de centavos va a la última cuota, de modo que Σ capital = capital exacto (RF-14.3).
 */
export function buildSchedule(input: ScheduleInput): PlannedInstallment[] {
  const { principal, annualRate, term, frequency, method, startDate, periodicFee, holidays } = input
  const i = periodRate(annualRate, frequency)

  const dueDates = Array.from({ length: term }, (_, index) => {
    const raw =
      index === 0 && input.firstDueDate
        ? input.firstDueDate
        : periodDate(input.firstDueDate ?? startDate, frequency, input.firstDueDate ? index : index + 1)
    return nextBusinessDay(raw, holidays)
  })

  const level =
    i === 0 ? principal / term : (principal * i) / (1 - Math.pow(1 + i, -term))
  const flatInterestPerPeriod = roundHalfUp(principal * i)

  const rows: PlannedInstallment[] = []
  let balance = principal

  for (let index = 0; index < term; index += 1) {
    const last = index === term - 1
    let interest: Cents
    let capital: Cents

    switch (method) {
      case 'FRANCES':
        interest = roundHalfUp(balance * i)
        capital = last ? balance : roundHalfUp(level) - interest
        break
      case 'FLAT':
        interest = flatInterestPerPeriod
        capital = last ? balance : roundHalfUp(principal / term)
        break
      case 'ALEMAN':
        interest = roundHalfUp(balance * i)
        capital = last ? balance : roundHalfUp(principal / term)
        break
      case 'AMERICANO':
        interest = roundHalfUp(balance * i)
        capital = last ? balance : 0
        break
    }

    capital = Math.min(Math.max(capital, 0), balance)
    balance -= capital
    rows.push({
      number: index + 1,
      dueDate: dueDates[index] as string,
      principal: capital,
      interest,
      fee: periodicFee,
      totalDue: capital + interest + periodicFee,
      balance,
    })
  }
  return rows
}

/** TCEA: tasa interna de retorno del flujo (neto recibido vs. cuotas), anualizada. */
export function computeTcea(
  netDisbursed: Cents,
  rows: PlannedInstallment[],
  frequency: PaymentFrequency,
): number {
  const payments = rows.map((row) => row.totalDue)
  if (sum(payments) <= netDisbursed) return 0
  const npv = (rate: number) =>
    payments.reduce((total, payment, index) => total + payment / Math.pow(1 + rate, index + 1), 0) -
    netDisbursed
  let low = 0
  let high = 1
  while (npv(high) > 0 && high < 1e6) high *= 2
  for (let step = 0; step < 100; step += 1) {
    const mid = (low + high) / 2
    if (npv(mid) > 0) low = mid
    else high = mid
  }
  return Math.pow(1 + (low + high) / 2, PERIODS_PER_YEAR[frequency]) - 1
}
