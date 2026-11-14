import { computed, getCurrentInstance, ref } from 'vue'
import { useRouter } from 'vue-router'
import { toApiError } from '@prestamos/api-client'
import { toast } from 'vue-sonner'

import { useSessionStore } from '@/app/stores/session'
import { onboardingService } from '../services/onboarding.service'

export type OnboardingStep = 1 | 2 | 3 | 4

/**
 * Composable para el flujo de Onboarding KYC, Consentimientos y Cuenta de Desembolso (FE-CP-03 a FE-CP-05).
 */
export function useKycOnboarding() {
  const router = getCurrentInstance() ? useRouter() : null
  const session = useSessionStore()

  const currentStep = ref<OnboardingStep>(1)
  const isBusy = ref(false)
  const errorMessage = ref<string | null>(null)

  // ── Paso 1: DNI Anverso y Reverso (FE-CP-03) ──────────────────────────────
  const dniFront = ref<string | null>(null)
  const dniBack = ref<string | null>(null)

  function simulateUploadDniFront() {
    dniFront.value =
      'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=='
  }

  function simulateUploadDniBack() {
    dniBack.value =
      'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=='
  }

  // ── Paso 2: Selfie con Prueba de Vida (FE-CP-03) ──────────────────────────
  const selfie = ref<string | null>(null)

  function simulateCaptureSelfie() {
    selfie.value =
      'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=='
  }

  // ── Paso 3: Consentimientos Legales (FE-CP-04) ────────────────────────────
  const consentDataProtection = ref(true)
  const consentCreditBureau = ref(true)
  const consentMarketing = ref(false)

  // ── Paso 4: Cuenta de Desembolso (FE-CP-05) ───────────────────────────────
  const payoutType = ref<'BANK' | 'WALLET'>('BANK')
  const bankName = ref('Banco de Crédito BCP')
  const cci = ref('')
  const phoneWallet = ref('')

  // Validación por paso
  const canProceed = computed(() => {
    switch (currentStep.value) {
      case 1:
        return !!dniFront.value && !!dniBack.value
      case 2:
        return !!selfie.value
      case 3:
        return consentDataProtection.value && consentCreditBureau.value
      case 4:
        if (payoutType.value === 'BANK') {
          return !!bankName.value && /^\d{20}$/.test(cci.value)
        }
        return /^9\d{8}$/.test(phoneWallet.value)
      default:
        return false
    }
  })

  function scrollToTop() {
    try {
      window.scrollTo?.({ top: 0, behavior: 'smooth' })
    } catch {
      // Ignorado en entornos de prueba sin scrollTo implementado
    }
  }

  function nextStep() {
    if (!canProceed.value) return
    if (currentStep.value < 4) {
      currentStep.value = (currentStep.value + 1) as OnboardingStep
      scrollToTop()
    }
  }

  function prevStep() {
    if (currentStep.value > 1) {
      currentStep.value = (currentStep.value - 1) as OnboardingStep
      scrollToTop()
    }
  }

  async function completeOnboarding(): Promise<boolean> {
    if (!canProceed.value) return false
    isBusy.value = true
    errorMessage.value = null
    const customerId = session.user?.customerId

    try {
      // 1. Envía expediente KYC (FE-CP-03 / BE-CUS-03..04)
      await onboardingService.submitKyc(
        {
          dniFrontUrl: dniFront.value || 'dni-front.png',
          dniBackUrl: dniBack.value || 'dni-back.png',
          selfieUrl: selfie.value || 'selfie.png',
        },
        customerId,
      )

      // 2. Envía consentimientos legales (FE-CP-04 / BE-CUS-06)
      await onboardingService.submitConsents(
        {
          dataProtection: consentDataProtection.value,
          creditBureau: consentCreditBureau.value,
          marketing: consentMarketing.value,
        },
        customerId,
      )

      // 3. Registra cuenta de desembolso (FE-CP-05 / BE-CUS-09)
      await onboardingService.registerBankAccount(
        {
          bankName: payoutType.value === 'BANK' ? bankName.value : 'Billetera Digital',
          accountType: payoutType.value === 'BANK' ? 'CUENTA_AHORRO' : 'BILLETERA_DIGITAL',
          cci: payoutType.value === 'BANK' ? cci.value : undefined,
          phoneWallet: payoutType.value === 'WALLET' ? phoneWallet.value : undefined,
        },
        customerId,
      )

      session.updateKycStatus('VERIFIED')

      toast.success('¡Verificación completada con éxito!', {
        description:
          'Tu identidad y cuenta de desembolso han sido validadas. Ya puedes solicitar préstamos.',
      })

      await router?.push('/inicio')
      return true
    } catch (err) {
      const apiErr = toApiError(err)
      errorMessage.value = apiErr.detail || 'Ocurrió un error al procesar tu expediente.'
      toast.error('Error en la verificación', { description: errorMessage.value })
      return false
    } finally {
      isBusy.value = false
    }
  }

  const userKycStatus = computed(() => session.user?.kycStatus || 'NOT_SUBMITTED')

  function retryKyc() {
    dniFront.value = null
    dniBack.value = null
    selfie.value = null
    currentStep.value = 1
    session.updateKycStatus('NOT_SUBMITTED')
  }

  return {
    userKycStatus,
    retryKyc,
    currentStep,
    isBusy,
    errorMessage,
    canProceed,
    nextStep,
    prevStep,
    completeOnboarding,
    // Paso 1
    dniFront,
    dniBack,
    simulateUploadDniFront,
    simulateUploadDniBack,
    // Paso 2
    selfie,
    simulateCaptureSelfie,
    // Paso 3
    consentDataProtection,
    consentCreditBureau,
    consentMarketing,
    // Paso 4
    payoutType,
    bankName,
    cci,
    phoneWallet,
  }
}
