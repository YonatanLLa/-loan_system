import { createApiClient, type ApiClientOptions, type AxiosInstance } from '@prestamos/api-client'

let instance: AxiosInstance | null = null

/** Crea la instancia única de Axios. Se llama una vez en main.ts. */
export function setupApi(options: ApiClientOptions): AxiosInstance {
  instance = createApiClient(options)
  return instance
}

/** Instancia de Axios con sesión, idempotencia y errores normalizados (ARQUITECTURA_FRONTEND §6). */
export function useApi(): AxiosInstance {
  if (!instance) {
    throw new Error('El cliente de API no está inicializado: llama a setupApi() en main.ts.')
  }
  return instance
}
