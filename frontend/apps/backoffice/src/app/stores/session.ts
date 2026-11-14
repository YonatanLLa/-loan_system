import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import {
  ApiError,
  type AuthTokensResponse,
  type LoginResponse,
  type LoginStatus,
  type LogoutRequest,
  type Me,
  type MfaEnrollConfirmResponse,
  type MfaEnrollResponse,
  toApiError,
} from '@prestamos/api-client'

import { useApi } from '../api'
import { ALL_PERMISSIONS } from '../permissions'

export interface SessionUser {
  id: string
  userId?: string
  fullName: string
  email: string
  role: string
  mfaEnabled?: boolean
}

export type MfaMode = 'verify' | 'enroll'

export interface MfaChallengeState {
  mode: MfaMode
  mfaToken: string
  expiresInSeconds: number
  expiresAt: number
  email: string
  enrollment?: MfaEnrollResponse
}

export type SessionTerminationReason = 'reused' | 'expired' | null

const DEMO_ENABLED = import.meta.env.DEV && import.meta.env.VITE_DEMO_LOGIN === 'true'
const REFRESH_TOKEN_STORAGE_KEY = 'prestamos.backoffice.refreshToken'
const MFA_CHALLENGE_STORAGE_KEY = 'prestamos.backoffice.mfaChallenge'

function getSessionStorage(): Storage | null {
  try {
    return typeof sessionStorage !== 'undefined' ? sessionStorage : null
  } catch {
    return null
  }
}

function readStorageItem(key: string): string | null {
  try {
    return getSessionStorage()?.getItem(key) ?? null
  } catch {
    return null
  }
}

function writeStorageItem(key: string, value: string | null): void {
  try {
    const storage = getSessionStorage()
    if (!storage) return
    if (value === null) {
      storage.removeItem(key)
    } else {
      storage.setItem(key, value)
    }
  } catch {
    // Ignorar errores de almacenamiento (modo privado / cuota excedida).
  }
}

function readMfaChallenge(): MfaChallengeState | null {
  const raw = readStorageItem(MFA_CHALLENGE_STORAGE_KEY)
  if (!raw) return null
  try {
    const parsed = JSON.parse(raw) as Partial<MfaChallengeState>
    if (
      !parsed.mfaToken ||
      (parsed.mode !== 'verify' && parsed.mode !== 'enroll') ||
      typeof parsed.expiresAt !== 'number' ||
      parsed.expiresAt <= Date.now()
    ) {
      writeStorageItem(MFA_CHALLENGE_STORAGE_KEY, null)
      return null
    }
    return {
      mode: parsed.mode,
      mfaToken: parsed.mfaToken,
      expiresInSeconds: typeof parsed.expiresInSeconds === 'number' ? parsed.expiresInSeconds : 300,
      expiresAt: parsed.expiresAt,
      email: typeof parsed.email === 'string' ? parsed.email : '',
      enrollment: parsed.enrollment,
    }
  } catch {
    writeStorageItem(MFA_CHALLENGE_STORAGE_KEY, null)
    return null
  }
}

function createClientPortalOnlyError(): ApiError {
  return new ApiError({
    status: 403,
    title: 'Acceso restringido al Backoffice',
    detail: 'Las cuentas de cliente deben ingresar desde el Portal de Clientes.',
    code: 'Security.ClientPortalOnly',
  })
}

export function getUserInitials(fullName?: string | null): string {
  return (fullName ?? '')
    .split(' ')
    .filter(Boolean)
    .map((part) => part.charAt(0))
    .join('')
    .slice(0, 2)
    .toUpperCase()
}

/**
 * Sesión del backoffice alineada con `API_SECURITY.md`.
 * - El access token vive solo en memoria; el refresh token rota por pestaña en `sessionStorage`.
 * - Refresh single-flight: comparte la promesa entre interceptor HTTP y router guards (`ensure`).
 */
export const useSessionStore = defineStore('session', () => {
  const accessToken = ref<string | null>(null)
  const refreshToken = ref<string | null>(readStorageItem(REFRESH_TOKEN_STORAGE_KEY))
  const user = ref<SessionUser | null>(null)
  const permissions = ref<readonly string[]>([])
  const isDemo = ref(false)
  const mfaChallenge = ref<MfaChallengeState | null>(readMfaChallenge())
  const sessionTerminationReason = ref<SessionTerminationReason>(null)

  const isAuthenticated = computed(() => user.value !== null)
  const initials = computed(() => getUserInitials(user.value?.fullName))

  let refreshInFlight: Promise<boolean> | null = null
  let restoring: Promise<boolean> | null = null

  function can(permission: string): boolean {
    return permissions.value.includes(permission)
  }

  function setRefreshToken(token: string | null) {
    refreshToken.value = token
    writeStorageItem(REFRESH_TOKEN_STORAGE_KEY, token)
  }

  function setMfaChallenge(challenge: MfaChallengeState | null) {
    mfaChallenge.value = challenge
    writeStorageItem(
      MFA_CHALLENGE_STORAGE_KEY,
      challenge ? JSON.stringify(challenge) : null,
    )
  }

  function clearMfaChallenge() {
    setMfaChallenge(null)
  }

  function clearTerminationReason() {
    sessionTerminationReason.value = null
  }

  function applyTokens(nextAccessToken: string, nextRefreshToken?: string | null) {
    accessToken.value = nextAccessToken
    if (nextRefreshToken !== undefined) {
      setRefreshToken(nextRefreshToken)
    }
    sessionTerminationReason.value = null
  }

  function clear() {
    accessToken.value = null
    setRefreshToken(null)
    user.value = null
    permissions.value = []
    isDemo.value = false
    clearMfaChallenge()
    clearTerminationReason()
  }

  function getValidMfaChallenge(): MfaChallengeState | null {
    const current = mfaChallenge.value ?? readMfaChallenge()
    if (!current || current.expiresAt <= Date.now()) {
      clearMfaChallenge()
      return null
    }
    mfaChallenge.value = current
    return current
  }

  async function revokeStrayClientTokens(tokens: Pick<AuthTokensResponse, 'accessToken' | 'refreshToken'>) {
    try {
      await useApi().post(
        '/auth/logout',
        { refreshToken: tokens.refreshToken },
        {
          skipAuthRefresh: true,
          headers: tokens.accessToken ? { Authorization: `Bearer ${tokens.accessToken}` } : undefined,
        },
      )
    } catch {
      // No bloquear el rechazo si el logout falla.
    }
  }

  async function completeTokenAuthentication(tokens: AuthTokensResponse): Promise<void> {
    if (tokens.role === 'ROLE_CLIENT') {
      await revokeStrayClientTokens(tokens)
      clear()
      throw createClientPortalOnlyError()
    }

    applyTokens(tokens.accessToken, tokens.refreshToken)
    clearMfaChallenge()
    await loadProfile()

    if (user.value?.role === 'ROLE_CLIENT') {
      await logout()
      throw createClientPortalOnlyError()
    }
  }

  async function loadProfile() {
    const { data } = await useApi().get<Me>('/me')
    user.value = {
      id: data.id,
      userId: data.userId ?? data.id,
      fullName: data.fullName,
      email: data.email,
      role: data.role,
      mfaEnabled: data.mfaEnabled,
    }
    permissions.value = data.permissions
  }

  /** Re-consulta `GET /api/me` bajo demanda; en modo demo conserva los datos locales. */
  async function refreshProfile(): Promise<void> {
    if (isDemo.value) return
    await loadProfile()
  }

  /**
   * Renueva el access token enviando el refresh token rotatorio en el body.
   * Garantiza single-flight para evitar reutilización concurrente del mismo token.
   */
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
        applyTokens(data.accessToken, data.refreshToken)
        return true
      } catch (error) {
        const apiError = toApiError(error)
        const reason: SessionTerminationReason =
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

  /** Recupera la sesión al recargar la página (una sola vez aunque varias rutas la pidan). */
  async function ensure(): Promise<boolean> {
    if (isAuthenticated.value) return true
    const currentRefreshToken = refreshToken.value ?? readStorageItem(REFRESH_TOKEN_STORAGE_KEY)
    if (!currentRefreshToken) return false

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

  async function login(email: string, password: string): Promise<LoginStatus> {
    const normalizedEmail = email.trim()
    const { data } = await useApi().post<LoginResponse>(
      '/auth/login',
      { email: normalizedEmail, password },
      { skipAuthRefresh: true },
    )

    if (data.status === 'AUTHENTICATED') {
      if (!data.accessToken || !data.refreshToken) {
        throw new Error('Respuesta de autenticación incompleta.')
      }
      await completeTokenAuthentication({
        accessToken: data.accessToken,
        refreshToken: data.refreshToken,
        expiresInSeconds: data.expiresInSeconds ?? 900,
        role: data.role ?? '',
      })
      return 'AUTHENTICATED'
    }

    clear()
    const expiresInSeconds = data.mfaExpiresInSeconds ?? 300
    setMfaChallenge({
      mode: data.status === 'MFA_ENROLLMENT_REQUIRED' ? 'enroll' : 'verify',
      mfaToken: data.mfaToken ?? '',
      expiresInSeconds,
      expiresAt: Date.now() + expiresInSeconds * 1000,
      email: normalizedEmail,
    })
    return data.status
  }

  async function verifyMfa(payload: { code?: string; recoveryCode?: string }): Promise<void> {
    const challenge = getValidMfaChallenge()
    if (!challenge || challenge.mode !== 'verify') {
      throw new ApiError({
        status: 401,
        title: 'Desafío MFA expirado',
        detail: 'Tu sesión de verificación expiró. Inicia sesión nuevamente.',
        code: 'Security.MfaChallengeExpired',
      })
    }

    const { data } = await useApi().post<AuthTokensResponse>(
      '/auth/mfa/verify',
      {
        mfaToken: challenge.mfaToken,
        ...(payload.code?.trim() ? { code: payload.code.trim() } : {}),
        ...(payload.recoveryCode?.trim() ? { recoveryCode: payload.recoveryCode.trim() } : {}),
      },
      { skipAuthRefresh: true },
    )

    await completeTokenAuthentication(data)
  }

  async function startMfaEnroll(): Promise<MfaEnrollResponse> {
    const challenge = getValidMfaChallenge()
    if (!challenge || challenge.mode !== 'enroll') {
      throw new ApiError({
        status: 401,
        title: 'Registro MFA expirado',
        detail: 'Tu sesión de configuración MFA expiró. Inicia sesión nuevamente.',
        code: 'Security.MfaChallengeExpired',
      })
    }

    // BeginMfaEnrollmentUseCase genera un secreto nuevo por cada llamada:
    // si ya obtuvimos uno para este mfaToken, lo reutilizamos al recargar la página.
    if (challenge.enrollment) {
      return challenge.enrollment
    }

    const { data } = await useApi().post<MfaEnrollResponse>(
      '/auth/mfa/enroll',
      { mfaToken: challenge.mfaToken },
      { skipAuthRefresh: true },
    )

    setMfaChallenge({
      ...challenge,
      enrollment: data,
    })

    return data
  }

  async function confirmMfaEnroll(code: string): Promise<string[]> {
    const challenge = getValidMfaChallenge()
    if (!challenge || challenge.mode !== 'enroll') {
      throw new ApiError({
        status: 401,
        title: 'Registro MFA expirado',
        detail: 'Tu sesión de configuración MFA expiró. Inicia sesión nuevamente.',
        code: 'Security.MfaChallengeExpired',
      })
    }

    const { data } = await useApi().post<MfaEnrollConfirmResponse>(
      '/auth/mfa/enroll/confirm',
      { mfaToken: challenge.mfaToken, code: code.trim() },
      { skipAuthRefresh: true },
    )

    await completeTokenAuthentication(data.tokens)
    return data.recoveryCodes
  }

  /** Ingreso ficticio para recorrer las pantallas sin backend (solo desarrollo). */
  function loginDemo() {
    if (!DEMO_ENABLED) throw new Error('El ingreso demo solo existe en desarrollo.')
    isDemo.value = true
    accessToken.value = 'demo'
    setRefreshToken(null)
    clearMfaChallenge()
    sessionTerminationReason.value = null
    user.value = {
      id: 'demo',
      userId: 'demo',
      fullName: 'Usuario Demo',
      email: 'demo@prestamos.local',
      role: 'ROLE_ADMIN',
      mfaEnabled: true,
    }
    permissions.value = ALL_PERMISSIONS
  }

  async function performLogout(payload: LogoutRequest, shouldNotifyBackend: boolean): Promise<void> {
    try {
      if (!isDemo.value && shouldNotifyBackend) {
        await useApi().post('/auth/logout', payload, { skipAuthRefresh: true })
      }
    } catch {
      // La sesión local se limpia igual aunque el backend no responda.
    } finally {
      clear()
    }
  }

  async function logout(): Promise<void> {
    const currentRefreshToken = refreshToken.value ?? readStorageItem(REFRESH_TOKEN_STORAGE_KEY)
    await performLogout(
      { refreshToken: currentRefreshToken ?? null },
      Boolean(currentRefreshToken || accessToken.value),
    )
  }

  /** Revoca todas las sesiones activas del usuario autenticado (`POST /api/auth/logout` con `{}`). */
  async function logoutAll(): Promise<void> {
    await performLogout({}, true)
  }

  return {
    accessToken,
    refreshToken,
    user,
    permissions,
    isDemo,
    isAuthenticated,
    initials,
    mfaChallenge,
    sessionTerminationReason,
    demoEnabled: DEMO_ENABLED,
    can,
    clear,
    clearMfaChallenge,
    clearTerminationReason,
    getValidMfaChallenge,
    ensure,
    login,
    verifyMfa,
    startMfaEnroll,
    confirmMfaEnroll,
    loginDemo,
    logout,
    logoutAll,
    loadProfile,
    refreshProfile,
    refreshAccessToken,
  }
})
