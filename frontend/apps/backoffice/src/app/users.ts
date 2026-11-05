import { createDemoUsersApi, createUsersApi, type UsersApi } from '@prestamos/api-client'

import { useApi } from './api'
import { useSessionStore } from './stores/session'

/** Módulo de usuarios de api-client: datos demo en modo demo, API .NET en los demás casos. */
export function useUsersApi(): UsersApi {
  const session = useSessionStore()
  return session.isDemo && session.user
    ? createDemoUsersApi(session.user)
    : createUsersApi(useApi())
}
