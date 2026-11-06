import { describe, expect, it } from 'vitest'

import { formatBusinessDate, formatInstant, formatMoney, maskTail } from '../format'

// Intl usa espacios no separables; se normalizan para comparar.
const plain = (text: string) => text.replace(/\s/g, ' ')

describe('formatMoney', () => {
  it('formatea soles con separador de miles y 2 decimales', () => {
    expect(plain(formatMoney('1250.5'))).toBe('S/ 1,250.50')
  })

  it('formatea dólares cuando se indica la moneda', () => {
    expect(plain(formatMoney('99.9', 'USD'))).toContain('99.90')
  })

  it('rechaza montos que no son números', () => {
    expect(() => formatMoney('abc')).toThrow(TypeError)
  })
})

describe('formatBusinessDate', () => {
  it('no corre la fecha un día hacia atrás (no pasa por UTC)', () => {
    expect(formatBusinessDate('2026-11-05')).toBe('05/11/2026')
  })

  it('rechaza formatos distintos de AAAA-MM-DD', () => {
    expect(() => formatBusinessDate('05/11/2026')).toThrow(TypeError)
  })
})

describe('formatInstant', () => {
  it('muestra el instante en hora de Lima (UTC-5)', () => {
    // 2026-11-05 03:00 UTC = 2026-11-04 22:00 en Lima
    expect(plain(formatInstant('2026-11-05T03:00:00Z'))).toMatch(/^4\/11\/26/)
  })
})

describe('maskTail', () => {
  it('deja visibles solo los últimos 4 dígitos', () => {
    expect(maskTail('00219312345678901234')).toBe('**** 1234')
  })

  it('no enmascara valores cortos', () => {
    expect(maskTail('123')).toBe('123')
  })
})
