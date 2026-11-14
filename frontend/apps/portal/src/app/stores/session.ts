import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import { type AuthTokensResponse, type Me, toApiError } from '@prestamos/api-client'

import { useApi } from '../api'

export interface ClientUser {
  id: string
  userId?: string
  fullName: string
  docNumber: string
  email: string
  role?: string
  customerId?: string
  kycStatus: string
}

export type PortalSessionTerminationReason = 'reused' | 'expired' | null

const INACTIVITY_TIMEOUT_MS = 15 * 60 * 1000 // 15 minutos de inactividad (FE-CP-02 / RNF-19)
const REFRESH_TOKEN_STORAGE_KEY = 'prestamos.portal.refreshToken'
const DEMO_ENABLED = import.meta.env.DEV && import.meta.env.VITE_DEMO_LOGIN === 'true'

function readStorageItem(key: string): string | null {
  try {
    return typeof sessionStorage !== 'undefined' ? sessionStorage.getItem(key) : null
  } catch {
    return null
  }
}

function writeStorageItem(key: string, value: string | null): void {
  try {
    if (typeof sessionStorage === 'undefined') return
    if (value === null) {
      sessionStorage.removeItem(key)
    } else {
      sessionStorage.setItem(key, value)
    }
  } catch {
    // Ignorar errores de almacenamiento.
  }
}

export const useSessionStore = defineStore('session', () => {
  const accessToken = ref<string | null>(null)
  const refreshToken = ref<string | null>(readStorageItem(REFRESH_TOKEN_STORAGE_KEY))
  const user = ref<ClientUser | null>(null)
  const isDemo = ref(false)
  const sessionTerminationReason = ref<PortalSessionTerminationReason>(null)

  const isAuthenticated = computed(() => user.value !== null)

  let lastActivity = Date.now()
  let inactivityInterval: ReturnType<typeof setInterval> | null = null
  let refreshInFlight: Promise<boolean> | null = null
  let restoring: Promise<boolean> | null = null

  function markActivity() {
    lastActivity = Date.now()
  }

  function startInactivityWatch(onExpired?: () => void) {
    if (inactivityInterval) clearInterval(inactivityInterval)
    markActivity()

    if (typeof window !== 'undefined') {
      window.removeEventListener('click', markActivity)
      window.removeEventListener('keydown', markActivity)
      window.removeEventListener('touchstart', markActivity)
      window.addEventListener('click', markActivity, { passive: true })
      window.addEventListener('keydown', markActivity, { passive: true })
      window.addEventListener('touchstart', markActivity, { passive: true })
    }

    inactivityInterval = setInterval(() => {
      if (isAuthenticated.value && Date.now() - lastActivity > INACTIVITY_TIMEOUT_MS) {
        clear()
        if (onExpired) onExpired()
      }
    }, 15000)
  }

  function stopInactivityWatch() {
    if (inactivityInterval) {
      clearInterval(inactivityInterval)
      inactivityInterval = null
    }
    if (typeof window !== 'undefined') {
      window.removeEventListener('click', markActivity)
      window.removeEventListener('keydown', markActivity)
      window.removeEventListener('touchstart', markActivity)
    }
  }

  function setRefreshToken(token: string | null) {
    refreshToken.value = token
    writeStorageItem(REFRESH_TOKEN_STORAGE_KEY, token)
  }

  function clearTerminationReason() {
    sessionTerminationReason.value = null
  }

  function setSession(token: string, nextRefreshToken?: string | null) {
    accessToken.value = token
    if (nextRefreshToken !== undefined) {
      setRefreshToken(nextRefreshToken)
    }
    sessionTerminationReason.value = null
    markActivity()
  }

  function clear() {
    accessToken.value = null
    setRefreshToken(null)
    user.value = null
    isDemo.value = false
    sessionTerminationReason.value = null
    stopInactivityWatch()
  }

  async function loadProfile(): Promise<ClientUser | null> {
    try {
      const { data } = await useApi().get<Me>('/me')
      user.value = {
        id: data.id,
        userId: data.userId ?? data.id,
        fullName: data.fullName,
        docNumber: '',
        email: data.email,
        role: data.role,
        customerId: data.customerId,
        // GET /api/me del módulo Security actual no devuelve kycStatus; evitamos atrapar al cliente en /verificacion.
        kycStatus: data.kycStatus ?? 'VERIFIED',
      }
      return user.value
    } catch {
      return null
    }
  }

  function updateKycStatus(status: string) {
    if (user.value) {
      user.value.kycStatus = status
    }
  }

  async function refreshAccessToken(): Promise<boolean> {
    if (isDemo.value) return true
    if (refreshInFlight) return refreshInFlight

    refreshInFlight = (async () => {
      const currentRefreshToken = refreshToken.value ?? readStorageItem(REFRESH_TOKEN_STORAGE_KEY)
      if (!currentRefreshToken) {
        clear()
        sessionTerminationReason.value = 'expired'
        return false
      }

      try {
        const { data } = await useApi().post<AuthTokensResponse>(
          '/auth/refresh',
          { refreshToken: currentRefreshToken },
          { skipAuthRefresh: true },
        )
        setSession(data.accessToken, data.refreshToken)
        return true
      } catch (error) {
        const apiError = toApiError(error)
        const reason: PortalSessionTerminationReason =
          apiError.code === 'Security.RefreshTokenReused' ? 'reused' : 'expired'
        clear()
        sessionTerminationReason.value = reason
        return false
      }
    })().finally(() => {
      refreshInFlight = null
    })

    return refreshInFlight
  }

  /** Restaura la sesión en recarga de pestaña usando el refresh token en `sessionStorage`. */
  async function ensure(): Promise<boolean> {
    if (isAuthenticated.value) return true
    const currentRefreshToken = refreshToken.value ?? readStorageItem(REFRESH_TOKEN_STORAGE_KEY)
    if (!currentRefreshToken) return false

    restoring ??= (async () => {
      if (!(await refreshAccessToken())) return false
      const profile = await loadProfile()
      if (!profile || (profile.role && profile.role !== 'ROLE_CLIENT')) {
        clear()
        return false
      }
      return true
    })().finally(() => {
      restoring = null
    })

    return restoring
  }

  async function logout() {
    const currentRefreshToken = refreshToken.value ?? readStorageItem(REFRESH_TOKEN_STORAGE_KEY)
    try {
      if (!isDemo.value && (currentRefreshToken || accessToken.value)) {
        await useApi().post(
          '/auth/logout',
          { refreshToken: currentRefreshToken ?? null },
          { skipAuthRefresh: true },
        )
      }
    } catch {
      // Limpiar localmente aunque falle el backend.
    } finally {
      clear()
    }
  }

  return {
    accessToken,
    refreshToken,
    user,
    isDemo,
    isAuthenticated,
    demoEnabled: DEMO_ENABLED,
    sessionTerminationReason,
    setSession,
    clear,
    clearTerminationReason,
    loadProfile,
    updateKycStatus,
    refreshAccessToken,
    ensure,
    logout,
    startInactivityWatch,
    stopInactivityWatch,
    markActivity,
  }
})
