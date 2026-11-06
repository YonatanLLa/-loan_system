/** Aritmética de dinero en CENTAVOS enteros (sin errores de coma flotante). Solo para la API simulada. */
export type Cents = number

const DECIMAL = /^(-?)(\d+)(?:\.(\d{1,2}))?$/

export function toCents(amount: string): Cents {
  const match = DECIMAL.exec(amount.trim())
  if (!match) throw new TypeError(`Monto inválido: "${amount}"`)
  const [, sign, whole, fraction = ''] = match
  const cents = Number(whole) * 100 + Number(fraction.padEnd(2, '0'))
  return sign ? -cents : cents
}

export function fromCents(cents: Cents): string {
  const sign = cents < 0 ? '-' : ''
  const abs = Math.abs(Math.round(cents))
  return `${sign}${Math.trunc(abs / 100)}.${String(abs % 100).padStart(2, '0')}`
}

/** Redondeo Half-Up de un valor positivo (en centavos). */
export const roundHalfUp = (value: number): Cents => Math.round(value + Number.EPSILON)

export const sum = (values: Cents[]): Cents => values.reduce((total, value) => total + value, 0)
