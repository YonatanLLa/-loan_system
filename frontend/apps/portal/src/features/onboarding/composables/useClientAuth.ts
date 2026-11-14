import { getCurrentInstance, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { toApiError } from '@prestamos/api-client'
import type { ClientRegisterRequest } from '@prestamos/api-client'
import { toast } from 'vue-sonner'

import { useSessionStore } from '@/app/stores/session'
import { onboardingService } from '../services/onboarding.service'

/**
 * Composable para la autenticación y registro con OTP del cliente (FE-CP-01 y FE-CP-02).
 */
export function useClientAuth() {
  const router = getCurrentInstance() ? useRouter() : null
  const session = useSessionStore()

  // ── Login ──────────────────────────────────────────────────────────────────
  const loginIdentifier = ref('')
  const loginPassword = ref('')
  const loginLoading = ref(false)
  const loginError = ref<string | null>(null)
  const mfaChallengeToken = ref<string | null>(null)
  const mfaCode = ref('')

  async function completeClientLogin(
    accessToken: string,
    refreshToken: string | null,
    role?: string | null,
  ): Promise<boolean> {
    if (role && role !== 'ROLE_CLIENT') {
      await onboardingService.logout(refreshToken).catch(() => undefined)
      session.clear()
      loginError.value =
        'Esta cuenta pertenece al equipo interno. Ingresa desde el Backoffice.'
      toast.error('Acceso restringido', { description: loginError.value })
      return false
    }

    session.setSession(accessToken, refreshToken)
    const user = await session.loadProfile()

    if (!user || (user.role && user.role !== 'ROLE_CLIENT')) {
      await onboardingService.logout(refreshToken).catch(() => undefined)
      session.clear()
      loginError.value =
        user && user.role !== 'ROLE_CLIENT'
          ? 'Esta cuenta pertenece al equipo interno. Ingresa desde el Backoffice.'
          : 'No se pudo cargar tu perfil de cliente. Intenta nuevamente.'
      toast.error('Error de acceso', { description: loginError.value })
      return false
    }

    mfaChallengeToken.value = null
    mfaCode.value = ''

    // Inicia el vigilante de inactividad de 15 minutos (FE-CP-02)
    session.startInactivityWatch(() => {
      toast.info('Sesión cerrada por inactividad', {
        description: 'Tu sesión se cerró automáticamente tras 15 minutos sin interacción.',
      })
      void router?.push('/login')
    })

    toast.success('¡Bienvenido de vuelta!', {
      description: user.fullName || 'Sesión iniciada con éxito.',
    })

    // Si el KYC aún no está verificado, redirige al onboarding
    if (user.kycStatus === 'PENDING') {
      await router?.push('/verificacion')
    } else {
      await router?.push('/inicio')
    }
    return true
  }

  async function login(): Promise<boolean> {
    if (!loginIdentifier.value.trim() || !loginPassword.value) {
      loginError.value = 'Ingresa tu correo o celular y contraseña.'
      return false
    }

    loginLoading.value = true
    loginError.value = null
    try {
      const data = await onboardingService.loginClient(
        loginIdentifier.value,
        loginPassword.value,
      )

      if (data.status === 'MFA_REQUIRED') {
        mfaChallengeToken.value = data.mfaToken
        return false
      }

      if (data.status === 'MFA_ENROLLMENT_REQUIRED') {
        loginError.value =
          'Tu cuenta requiere configuración de autenticación en dos pasos antes de ingresar.'
        toast.error('Configuración requerida', { description: loginError.value })
        return false
      }

      if (!data.accessToken) {
        loginError.value = 'Respuesta de autenticación incompleta.'
        return false
      }

      return await completeClientLogin(data.accessToken, data.refreshToken, data.role)
    } catch (err) {
      const apiErr = toApiError(err)
      loginError.value =
        apiErr.status === 423
          ? 'Tu cuenta ha sido bloqueada temporalmente por exceso de intentos.'
          : apiErr.detail || 'Credenciales incorrectas. Verifica tus datos.'
      toast.error('Error de acceso', { description: loginError.value })
      return false
    } finally {
      loginLoading.value = false
    }
  }

  async function verifyMfaLogin(): Promise<boolean> {
    if (!mfaChallengeToken.value) return false
    const cleaned = mfaCode.value.trim()
    if (!/^\d{6}$/.test(cleaned)) {
      loginError.value = 'Ingresa un código de verificación de 6 dígitos.'
      return false
    }

    loginLoading.value = true
    loginError.value = null
    try {
      const tokens = await onboardingService.verifyClientMfa({
        mfaToken: mfaChallengeToken.value,
        code: cleaned,
      })
      return await completeClientLogin(tokens.accessToken, tokens.refreshToken, tokens.role)
    } catch (err) {
      const apiErr = toApiError(err)
      loginError.value = apiErr.detail || 'Código inválido o expirado.'
      toast.error(apiErr.title, { description: loginError.value })
      return false
    } finally {
      loginLoading.value = false
    }
  }

  function cancelMfaChallenge() {
    mfaChallengeToken.value = null
    mfaCode.value = ''
    loginError.value = null
  }

  // ── Registro y Verificación OTP ───────────────────────────────────────────
  const registerForm = ref<ClientRegisterRequest>({
    docType: 'DNI',
    docNumber: '',
    fullName: '',
    email: '',
    phone: '',
    password: '',
  })

  const isOtpModalOpen = ref(false)
  const otpCode = ref('')
  const otpCountdown = ref(0)
  const isOtpBusy = ref(false)
  const registerError = ref<string | null>(null)

  let timerHandle: ReturnType<typeof setInterval> | null = null

  if (getCurrentInstance()) {
    onUnmounted(() => {
      if (timerHandle) clearInterval(timerHandle)
    })
  }

  function startCountdown(seconds = 60) {
    if (timerHandle) clearInterval(timerHandle)
    otpCountdown.value = seconds
    timerHandle = setInterval(() => {
      if (otpCountdown.value > 0) {
        otpCountdown.value--
      } else if (timerHandle) {
        clearInterval(timerHandle)
      }
    }, 1000)
  }

  async function requestOtp(): Promise<boolean> {
    const { docNumber, phone, fullName, email, password } = registerForm.value
    if (!docNumber || !phone || !fullName.trim() || !email.trim() || !password) {
      registerError.value = 'Completa todos los campos obligatorios.'
      return false
    }

    if (registerForm.value.docType === 'DNI' && !/^\d{8}$/.test(docNumber)) {
      registerError.value = 'El DNI debe contener exactamente 8 dígitos numéricos.'
      return false
    }

    if (!/^9\d{8}$/.test(phone)) {
      registerError.value = 'El celular debe tener 9 dígitos y empezar con 9.'
      return false
    }

    isOtpBusy.value = true
    registerError.value = null
    try {
      const res = await onboardingService.sendOtp(phone, 'REGISTER')
      isOtpModalOpen.value = true
      startCountdown(res.expiresInSeconds || 60)
      toast.info('Código OTP enviado', {
        description: res.demoCode
          ? `Código generado: ${res.demoCode} (modo prueba).`
          : res.message,
      })
      return true
    } catch (err) {
      const apiErr = toApiError(err)
      registerError.value = apiErr.detail
      toast.error('No se pudo enviar el código OTP', { description: apiErr.detail })
      return false
    } finally {
      isOtpBusy.value = false
    }
  }

  async function verifyOtpAndRegister(): Promise<boolean> {
    if (!otpCode.value.trim() || otpCode.value.trim().length < 6) {
      registerError.value = 'Ingresa el código OTP completo de 6 dígitos.'
      return false
    }

    isOtpBusy.value = true
    registerError.value = null
    try {
      // 1. Valida el código OTP
      await onboardingService.verifyOtp(registerForm.value.phone, otpCode.value, 'REGISTER')

      // 2. Crea la cuenta del cliente
      const reg = await onboardingService.registerClient(registerForm.value)

      if (reg.accessToken) {
        session.setSession(reg.accessToken)
        await session.loadProfile()
      }

      toast.success('¡Registro exitoso!', {
        description: 'Tu cuenta ha sido creada. Ahora validaremos tu identidad.',
      })

      isOtpModalOpen.value = false
      await router?.push('/verificacion')
      return true
    } catch (err) {
      const apiErr = toApiError(err)
      registerError.value =
        apiErr.status === 409
          ? 'Ya existe una cuenta registrada con este documento o correo.'
          : apiErr.detail
      toast.error('Error al verificar código', { description: registerError.value })
      return false
    } finally {
      isOtpBusy.value = false
    }
  }

  async function logout(): Promise<void> {
    const currentRefreshToken = session.refreshToken
    try {
      await onboardingService.logout(currentRefreshToken)
    } catch {
      // Si la llamada falla, igualmente limpiamos el estado local
    } finally {
      session.clear()
      await router?.push('/login')
      toast.info('Sesión cerrada correctamente.')
    }
  }

  return {
    // Login
    loginIdentifier,
    loginPassword,
    loginLoading,
    loginError,
    mfaChallengeToken,
    mfaCode,
    login,
    verifyMfaLogin,
    cancelMfaChallenge,
    logout,
    // Registro y OTP
    registerForm,
    isOtpModalOpen,
    otpCode,
    otpCountdown,
    isOtpBusy,
    registerError,
    requestOtp,
    verifyOtpAndRegister,
  }
}
