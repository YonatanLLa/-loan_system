import { describe, expect, it, vi } from 'vitest'
import { AxiosError, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios'

import { ApiError, toApiError, toFormErrors } from '../errors'
import { createIdempotentAction, withIdempotency } from '../idempotency'

const config = { headers: {} } as InternalAxiosRequestConfig

function axiosError(status: number, data: unknown) {
  const response = { data, status, statusText: '', headers: {}, config } as AxiosResponse
  return new AxiosError(`HTTP ${status}`, String(status), config, null, response)
}

describe('toApiError', () => {
  it('lee el ProblemDetails del backend', () => {
    const error = toApiError(
      axiosError(422, {
        title: 'Cliente con KYC pendiente',
        detail: 'El cliente debe tener el KYC verificado para desembolsar.',
        traceId: 'abc',
        errors: { Amount: ['El monto excede el máximo del producto'] },
      }),
    )
    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(422)
    expect(error.detail).toBe('El cliente debe tener el KYC verificado para desembolsar.')
    expect(error.traceId).toBe('abc')
  })

  it('preserva code y errorType de ProblemDetails de Security y detecta respuestas con solo code', () => {
    const error = toApiError(
      axiosError(401, {
        title: 'Security.RefreshTokenReused',
        detail: 'The refresh token is invalid, expired or revoked.',
        code: 'Security.RefreshTokenReused',
        errorType: 'Unauthorized',
      }),
    )
    expect(error.status).toBe(401)
    expect(error.code).toBe('Security.RefreshTokenReused')
    expect(error.errorType).toBe('Unauthorized')
    expect(error.detail).toBe('The refresh token is invalid, expired or revoked.')

    const codeOnly = toApiError(
      axiosError(403, {
        code: 'Approvals.InvalidMfaCode',
        errorType: 'Forbidden',
      }),
    )
    expect(codeOnly.status).toBe(403)
    expect(codeOnly.code).toBe('Approvals.InvalidMfaCode')
    expect(codeOnly.errorType).toBe('Forbidden')
    expect(codeOnly.title).toBe('Sin permiso')
  })

  it('da un título por defecto cuando la respuesta no es ProblemDetails', () => {
    const error = toApiError(axiosError(409, 'texto plano'))
    expect(error.title).toBe('Conflicto')
  })

  it('distingue la falta de conexión', () => {
    const error = toApiError(new AxiosError('Network Error', 'ERR_NETWORK', config, null, undefined))
    expect(error.isNetworkError).toBe(true)
    expect(error.title).toBe('Sin conexión')
  })
})

describe('toFormErrors', () => {
  it('pasa los nombres de campo del backend al formato del formulario', () => {
    const error = new ApiError({
      status: 400,
      title: 'Datos inválidos',
      detail: 'Revisa los campos',
      fieldErrors: { Amount: ['Monto inválido'], 'Customer.DocNumber': ['DNI duplicado'] },
    })
    expect(toFormErrors(error)).toEqual({
      amount: 'Monto inválido',
      'customer.docNumber': 'DNI duplicado',
    })
  })
})

describe('idempotencia', () => {
  it('agrega la cabecera Idempotency-Key', () => {
    expect(withIdempotency('k-1').headers).toMatchObject({ 'Idempotency-Key': 'k-1' })
  })

  it('reutiliza la misma clave en los reintentos y la renueva tras el éxito', async () => {
    const keys: string[] = []
    let attempts = 0
    const action = createIdempotentAction(async (key: string) => {
      keys.push(key)
      attempts += 1
      if (attempts === 1) throw new Error('caída de red')
      return 'ok'
    })

    await expect(action.execute()).rejects.toThrow('caída de red')
    await expect(action.execute()).resolves.toBe('ok')
    await action.execute()

    expect(keys[0]).toBe(keys[1])
    expect(keys[2]).not.toBe(keys[1])
  })

  it('reset descarta la clave pendiente', async () => {
    const run = vi.fn<() => Promise<never>>(async () => {
      throw new Error('falló')
    })
    const action = createIdempotentAction(run)
    await expect(action.execute()).rejects.toThrow('falló')
    expect(action.pendingKey).not.toBeNull()
    action.reset()
    expect(action.pendingKey).toBeNull()
  })
})
