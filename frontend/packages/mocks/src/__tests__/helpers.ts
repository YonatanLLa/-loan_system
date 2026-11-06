import { createApiClient, type AxiosInstance } from '@prestamos/api-client'

import { DEMO_PASSWORD } from '../seed'

export const BASE_URL = 'http://api.test/api'

/** Cliente HTTP real del frontend (Axios + interceptores) apuntando a la API simulada. */
export function client(token: string | null = null): AxiosInstance {
  return createApiClient({
    baseURL: BASE_URL,
    session: {
      getAccessToken: () => token,
      refresh: async () => false,
      onSessionExpired: () => undefined,
    },
  })
}

export async function loginAs(email: string, isPortal = false): Promise<AxiosInstance> {
  const { data } = await client().post<{ accessToken: string }>(
    isPortal ? '/client/login' : '/auth/login',
    { email, password: DEMO_PASSWORD },
    { skipAuthRefresh: true },
  )
  return client(data.accessToken)
}

export const admin = () => loginAs('admin@sistema.local')
export const portalClient = () => loginAs('cliente@correo.pe', true)
