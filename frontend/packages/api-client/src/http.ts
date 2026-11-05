import axios, { type AxiosAdapter, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'

import { toApiError } from './errors'

declare module 'axios' {
  interface AxiosRequestConfig {
    /** No intentar refrescar la sesión ante un 401 (p. ej. en login o en el propio refresco). */
    skipAuthRefresh?: boolean
  }
}

interface RetriableConfig extends InternalAxiosRequestConfig {
  _retried?: boolean
}

/** Lo que el cliente HTTP necesita saber de la sesión; lo implementa el store de cada app. */
export interface SessionPort {
  /** Access token actual. Vive solo en memoria, nunca en localStorage (FE-NF-08). */
  getAccessToken(): string | null
  /** Pide un access token nuevo usando la cookie HttpOnly de refresco. Devuelve false si la sesión terminó. */
  refresh(): Promise<boolean>
  /** La sesión no se pudo recuperar: limpiar estado y llevar al login. */
  onSessionExpired(): void
}

export interface ApiClientOptions {
  baseURL: string
  session: SessionPort
  timeoutMs?: number
  /** Solo para pruebas: reemplaza el transporte HTTP. */
  adapter?: AxiosAdapter
}

export function createApiClient(options: ApiClientOptions): AxiosInstance {
  const { baseURL, session, timeoutMs = 15_000, adapter } = options
  const api = axios.create({ baseURL, timeout: timeoutMs, withCredentials: true, adapter })

  // Un solo refresco compartido por todas las peticiones que reciban 401 a la vez.
  let refreshing: Promise<boolean> | null = null

  api.interceptors.request.use((config) => {
    const token = session.getAccessToken()
    if (token) config.headers.set('Authorization', `Bearer ${token}`)
    config.headers.set('X-Correlation-Id', crypto.randomUUID())
    return config
  })

  api.interceptors.response.use(undefined, async (error: unknown) => {
    const original = axios.isAxiosError(error) ? (error.config as RetriableConfig | undefined) : undefined
    const unauthorized = axios.isAxiosError(error) && error.response?.status === 401

    if (unauthorized && original && !original._retried && !original.skipAuthRefresh) {
      original._retried = true
      refreshing ??= session.refresh().finally(() => {
        refreshing = null
      })
      if (await refreshing) return api(original)
      session.onSessionExpired()
    }

    throw toApiError(error)
  })

  return api
}
