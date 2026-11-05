/** Fechas de negocio AAAA-MM-DD calculadas en UTC (sin saltos por zona horaria). */

const LIMA_OFFSET_MS = -5 * 60 * 60 * 1000

let todayOverride: string | null = null

/** Fecha de hoy en Lima. Las pruebas pueden fijarla con `setToday`. */
export function today(): string {
  if (todayOverride) return todayOverride
  return new Date(Date.now() + LIMA_OFFSET_MS).toISOString().slice(0, 10)
}

export function setToday(date: string | null): void {
  todayOverride = date
}

const toUtc = (date: string): Date => new Date(`${date}T00:00:00Z`)
const format = (date: Date): string => date.toISOString().slice(0, 10)

export function addDays(date: string, days: number): string {
  const result = toUtc(date)
  result.setUTCDate(result.getUTCDate() + days)
  return format(result)
}

export function addMonths(date: string, months: number): string {
  const base = toUtc(date)
  const day = base.getUTCDate()
  const result = new Date(Date.UTC(base.getUTCFullYear(), base.getUTCMonth() + months, 1))
  const lastDay = new Date(
    Date.UTC(result.getUTCFullYear(), result.getUTCMonth() + 1, 0),
  ).getUTCDate()
  result.setUTCDate(Math.min(day, lastDay))
  return format(result)
}

export function diffDays(from: string, to: string): number {
  return Math.round((toUtc(to).getTime() - toUtc(from).getTime()) / 86_400_000)
}

export function isWeekend(date: string): boolean {
  const day = toUtc(date).getUTCDay()
  return day === 0 || day === 6
}

/** Desplaza al día hábil siguiente si cae en fin de semana o feriado (RF-14.2). */
export function nextBusinessDay(date: string, holidays: ReadonlySet<string>): string {
  let current = date
  while (isWeekend(current) || holidays.has(current)) current = addDays(current, 1)
  return current
}

export function ageOn(birthDate: string, on: string): number {
  const birth = toUtc(birthDate)
  const ref = toUtc(on)
  let age = ref.getUTCFullYear() - birth.getUTCFullYear()
  const beforeBirthday =
    ref.getUTCMonth() < birth.getUTCMonth() ||
    (ref.getUTCMonth() === birth.getUTCMonth() && ref.getUTCDate() < birth.getUTCDate())
  if (beforeBirthday) age -= 1
  return age
}
