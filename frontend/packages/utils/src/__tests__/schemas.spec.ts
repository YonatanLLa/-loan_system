import { describe, expect, it } from 'vitest'

import { cci, celularPe, correoOCelular, dni, monto } from '../schemas'

describe('schemas', () => {
  it('valida DNI de 8 dígitos', () => {
    expect(dni.safeParse('12345678').success).toBe(true)
    expect(dni.safeParse('1234567').success).toBe(false)
  })

  it('valida CCI de 20 dígitos', () => {
    expect(cci.safeParse('00219312345678901234').success).toBe(true)
    expect(cci.safeParse('0021931234567890123').success).toBe(false)
  })

  it('valida celular peruano', () => {
    expect(celularPe.safeParse('987654321').success).toBe(true)
    expect(celularPe.safeParse('887654321').success).toBe(false)
  })

  it('valida montos positivos con hasta 2 decimales', () => {
    expect(monto.safeParse('1250.50').success).toBe(true)
    expect(monto.safeParse('1250.505').success).toBe(false)
    expect(monto.safeParse('0').success).toBe(false)
  })

  it('acepta correo o celular para el ingreso al portal', () => {
    expect(correoOCelular.safeParse('cliente@correo.pe').success).toBe(true)
    expect(correoOCelular.safeParse('987654321').success).toBe(true)
    expect(correoOCelular.safeParse('hola').success).toBe(false)
  })
})
