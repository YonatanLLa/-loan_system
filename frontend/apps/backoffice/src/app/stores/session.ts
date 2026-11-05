import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import { useApi } from '../api'
import { ALL_PERMISSIONS } from '../permissions'

export interface SessionUser {
  id: string
  fullName: string
  email: string
  role: string
}

interface TokenResponse {
  accessToken: string
}

interface MeResponse extends SessionUser {
  permissions: string[]
}

const DEMO_ENABLED = import.meta.env.DEV && import.meta.env.VITE_DEMO_LOGIN === 'true'

/**
 * Sesión del backoffice (ARQUITECTURA_FRONTEND §5.1).
 * - El access token vive solo en memoria; el refresh token es una cookie HttpOnly del backend.
 * - Contrato provisional: POST /auth/login, POST /auth/refresh, POST /auth/logout, GET /me.
 */
export const useSessionStore = defineStore('session', () => {
  const accessToken = ref<string | null>(null)
  const user = ref<SessionUser | null>(null)
  const permissions = ref<readonly string[]>([])
  const isDemo = ref(false)

  const isAuthenticated = computed(() => user.value !== null)

  let restoring: Promise<boolean> | null = null

  function can(permission: string): boolean {
    return permissions.value.includes(permission)
  }

  function clear() {
    accessToken.value = null
    user.value = null
    permissions.value = []
    isDemo.value = false
  }

  async function loadProfile() {
    const { data } = await useApi().get<MeResponse>('/me')
    const { permissions: granted, ...profile } = data
    user.value = profile
    permissions.value = granted
  }

  /** Pide un access token nuevo con la cookie de refresco. */
  async function refreshAccessToken(): Promise<boolean> {
    if (isDemo.value) return true
    try {
      const { data } = await useApi().post<TokenResponse>('/auth/refresh', null, {
        skipAuthRefresh: true,
      })
      accessToken.value = data.accessToken
      return true
    } catch {
      clear()
      return false
    }
  }

  /** Recupera la sesión al recargar la página (una sola vez aunque varias rutas la pidan). */
  async function ensure(): Promise<boolean> {
    if (isAuthenticated.value) return true
    restoring ??= (async () => {
      if (!(await refreshAccessToken())) return false
      try {
        await loadProfile()
        return true
      } catch {
        clear()
        return false
      }
    })().finally(() => {
      restoring = null
    })
    return restoring
  }

  async function login(email: string, password: string) {
    const { data } = await useApi().post<TokenResponse>(
      '/auth/login',
      { email, password },
      { skipAuthRefresh: true },
    )
    accessToken.value = data.accessToken
    await loadProfile()
  }

  /** Ingreso ficticio para recorrer las pantallas sin backend (solo desarrollo). */
  function loginDemo() {
    if (!DEMO_ENABLED) throw new Error('El ingreso demo solo existe en desarrollo.')
    isDemo.value = true
    accessToken.value = 'demo'
    user.value = {
      id: 'demo',
      fullName: 'Usuario Demo',
      email: 'demo@prestamos.local',
      role: 'ROLE_ADMIN',
    }
    permissions.value = ALL_PERMISSIONS
  }

  async function logout() {
    if (!isDemo.value) {
      try {
        await useApi().post('/auth/logout', null, { skipAuthRefresh: true })
      } catch {
        // La sesión local se limpia igual aunque el backend no responda.
      }
    }
    clear()
  }

  return {
    accessToken,
    user,
    permissions,
    isDemo,
    isAuthenticated,
    demoEnabled: DEMO_ENABLED,
    can,
    clear,
    ensure,
    login,
    loginDemo,
    logout,
    refreshAccessToken,
  }
})
