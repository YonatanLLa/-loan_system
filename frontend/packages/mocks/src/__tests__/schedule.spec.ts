import { describe, expect, it } from 'vitest'
import type { AmortizationMethod, PaymentFrequency } from '@prestamos/api-client'

import { fromCents, sum, toCents } from '../money'
import { buildSchedule, computeTcea } from '../schedule'

const base = {
  principal: toCents('1000.00'),
  annualRate: 0.3,
  term: 3,
  startDate: '2026-01-05',
  periodicFee: 0,
  holidays: new Set<string>(),
}

describe('buildSchedule', () => {
  it.each(['FRANCES', 'FLAT', 'ALEMAN', 'AMERICANO'] as AmortizationMethod[])(
    '%s: la suma del capital es exactamente el capital (cuadre de centavos)',
    (method) => {
      const rows = buildSchedule({ ...base, frequency: 'MENSUAL', method })
      expect(sum(rows.map((row) => row.principal))).toBe(base.principal)
      expect(rows.at(-1)?.balance).toBe(0)
    },
  )

  it('FRANCES: las dos primeras cuotas son iguales', () => {
    const rows = buildSchedule({ ...base, frequency: 'MENSUAL', method: 'FRANCES' })
    expect(rows[0]?.totalDue).toBe(rows[1]?.totalDue)
  })

  it('ALEMAN: capital constante y cuota decreciente', () => {
    const rows = buildSchedule({ ...base, term: 4, frequency: 'MENSUAL', method: 'ALEMAN' })
    expect(rows[0]?.principal).toBe(rows[1]?.principal)
    expect((rows[0]?.totalDue ?? 0) > (rows[3]?.totalDue ?? 0)).toBe(true)
  })

  it('AMERICANO: solo intereses hasta la última cuota', () => {
    const rows = buildSchedule({ ...base, frequency: 'MENSUAL', method: 'AMERICANO' })
    expect(rows.slice(0, -1).every((row) => row.principal === 0)).toBe(true)
    expect(rows.at(-1)?.principal).toBe(base.principal)
  })

  it('desplaza los vencimientos a día hábil (fin de semana y feriados)', () => {
    // 2026-02-07 es sábado y 2026-02-09 se declara feriado: el vencimiento pasa al martes 10.
    const rows = buildSchedule({
      ...base,
      startDate: '2026-01-07',
      frequency: 'MENSUAL',
      method: 'FRANCES',
      holidays: new Set(['2026-02-09']),
    })
    expect(rows[0]?.dueDate).toBe('2026-02-10')
  })

  it.each(['SEMANAL', 'QUINCENAL', 'DIARIA'] as PaymentFrequency[])(
    '%s: genera todas las cuotas y cuadra el capital',
    (frequency) => {
      const rows = buildSchedule({ ...base, term: 6, frequency, method: 'FRANCES' })
      expect(rows).toHaveLength(6)
      expect(sum(rows.map((row) => row.principal))).toBe(base.principal)
    },
  )
})

describe('dinero en centavos', () => {
  it('convierte sin errores de coma flotante', () => {
    expect(toCents('0.10') + toCents('0.20')).toBe(30)
    expect(fromCents(toCents('1250.5'))).toBe('1250.50')
  })

  it('la TCEA supera a la TEA cuando hay comisiones', () => {
    const rows = buildSchedule({ ...base, periodicFee: 250, frequency: 'MENSUAL', method: 'FRANCES' })
    expect(computeTcea(base.principal - 1000, rows, 'MENSUAL')).toBeGreaterThan(0.3)
  })
})
