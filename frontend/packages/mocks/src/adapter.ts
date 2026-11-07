import './polyfills'
import { getResponse } from 'msw'
import { AxiosError, type AxiosAdapter, type AxiosResponse } from '@prestamos/api-client'

import { db } from './db'
import { handlers } from './handlers'
import { restoreSession } from './handlers/auth'
import { resetDb } from './seed'

let initialized = false

function ensureSeeded(): void {
  if (!initialized || db.users.length === 0) {
    resetDb()
    restoreSession()
    initialized = true
  }
}

function serializeBody(data: unknown, headers: Headers): string | undefined {
  if (data === undefined || data === null) return undefined
  if (typeof data === 'string') return data

  if (typeof FormData !== 'undefined' && data instanceof FormData) {
    const obj: Record<string, unknown> = {}
    const anyForm = data as unknown as {
      _parts?: Array<[string, unknown]>
      forEach?: (cb: (value: unknown, key: string) => void) => void
    }
    if (Array.isArray(anyForm._parts)) {
      for (const [key, value] of anyForm._parts) {
        obj[key] = value
      }
    } else if (typeof anyForm.forEach === 'function') {
      anyForm.forEach((value, key) => {
        obj[key] = typeof value === 'object' && value !== null && 'name' in value ? (value as { name: string }).name : value
      })
    }
    headers.set('content-type', 'application/json')
    return JSON.stringify(obj)
  }

  if (typeof data === 'object') {
    headers.set('content-type', 'application/json')
    return JSON.stringify(data)
  }

  return String(data)
}

/**
 * Adaptador de Axios en memoria respaldado por los handlers MSW de `@prestamos/mocks`.
 * Funciona tanto en Expo Web como en React Native (iOS / Android) sin requerir Service Worker.
 */
export const mockAxiosAdapter: AxiosAdapter = async (config) => {
  ensureSeeded()

  const base = (config.baseURL ?? 'http://localhost:5000').replace(/\/+$/, '')
  const rawPath = config.url ?? '/'
  const fullUrl = /^https?:\/\//i.test(rawPath)
    ? new URL(rawPath)
    : new URL(rawPath.startsWith('/') ? `${base}${rawPath}` : `${base}/${rawPath}`)

  if (config.params && typeof config.params === 'object') {
    for (const [key, value] of Object.entries(config.params as Record<string, unknown>)) {
      if (value !== undefined && value !== null) {
        fullUrl.searchParams.set(key, String(value))
      }
    }
  }

  const headers = new Headers()
  if (config.headers) {
    const rawHeaders =
      typeof config.headers.toJSON === 'function' ? config.headers.toJSON() : config.headers
    for (const [key, value] of Object.entries(rawHeaders as Record<string, unknown>)) {
      if (value !== undefined && value !== null) {
        headers.set(key, String(value))
      }
    }
  }

  const method = (config.method ?? 'GET').toUpperCase()
  const body = method !== 'GET' && method !== 'HEAD' ? serializeBody(config.data, headers) : undefined

  const request = new Request(fullUrl.toString(), {
    method,
    headers,
    body,
  })

  const res = await getResponse(handlers, request)

  if (!res) {
    throw new AxiosError(
      `No mock handler matched ${method} ${fullUrl.pathname}`,
      'ERR_BAD_REQUEST',
      config,
      request,
    )
  }

  const contentType = res.headers.get('content-type') ?? ''
  let data: unknown = null
  if (res.status !== 204) {
    if (contentType.includes('json')) {
      data = await res.json().catch(() => null)
    } else {
      data = await res.text()
    }
  }

  const responseHeaders: Record<string, string> = {}
  res.headers.forEach((value, key) => {
    responseHeaders[key] = value
  })

  const axiosResponse: AxiosResponse = {
    data,
    status: res.status,
    statusText: res.statusText || String(res.status),
    headers: responseHeaders,
    config,
    request,
  }

  if (res.status >= 200 && res.status < 300) {
    return axiosResponse
  }

  throw new AxiosError(
    `Request failed with status code ${res.status}`,
    res.status >= 500 ? 'ERR_BAD_RESPONSE' : 'ERR_BAD_REQUEST',
    config,
    request,
    axiosResponse,
  )
}
