/**
 * Formatos de presentación para Perú (es-PE).
 *
 * Regla del proyecto: el frontend NO calcula dinero. Los montos llegan de la API
 * como string decimal ("1250.50") y aquí solo se formatean para mostrarlos.
 */

const LIMA_TZ = 'America/Lima'

const moneyFormatters = new Map<string, Intl.NumberFormat>()

function moneyFormatter(currency: string): Intl.NumberFormat {
  let formatter = moneyFormatters.get(currency)
  if (!formatter) {
    formatter = new Intl.NumberFormat('es-PE', { style: 'currency', currency })
    moneyFormatters.set(currency, formatter)
  }
  return formatter
}

/** "1250.5" → "S/ 1,250.50". Solo para mostrar; nunca para operar. */
export function formatMoney(amount: string, currency = 'PEN'): string {
  const value = Number(amount)
  if (!Number.isFinite(value)) {
    throw new TypeError(`Monto inválido: "${amount}"`)
  }
  return moneyFormatter(currency).format(value)
}

const BUSINESS_DATE = /^(\d{4})-(\d{2})-(\d{2})$/

/**
 * Fecha de negocio ("2026-11-05", vencimientos, feriados) → "05/11/2026".
 *
 * No usar `new Date('2026-11-05')`: se interpreta como medianoche UTC
 * y en Lima (UTC-5) se muestra el día anterior.
 */
export function formatBusinessDate(isoDate: string): string {
  const match = BUSINESS_DATE.exec(isoDate)
  if (!match) {
    throw new TypeError(`Fecha de negocio inválida: "${isoDate}"`)
  }
  const [, year, month, day] = match
  return `${day}/${month}/${year}`
}

const instantFormatter = new Intl.DateTimeFormat('es-PE', {
  dateStyle: 'short',
  timeStyle: 'short',
  timeZone: LIMA_TZ,
})

/** Instante TIMESTAMPTZ en UTC ("2026-11-05T15:30:00Z") → fecha y hora de Lima. */
export function formatInstant(isoUtc: string): string {
  const date = new Date(isoUtc)
  if (Number.isNaN(date.getTime())) {
    throw new TypeError(`Instante inválido: "${isoUtc}"`)
  }
  return instantFormatter.format(date)
}

/** Enmascara cuentas, CCI o documentos dejando visibles los últimos dígitos: "**** 4821". */
export function maskTail(value: string, visible = 4): string {
  const clean = value.replace(/\s+/g, '')
  if (clean.length <= visible) return clean
  return `**** ${clean.slice(-visible)}`
}
