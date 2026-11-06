import { describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { onboardingService } from '../features/onboarding/services'
import { useClientAuth, useKycOnboarding } from '../features/onboarding/composables'

describe('Onboarding Service (FE-CP-01 a FE-CP-05)', () => {
  it('expone todas las operaciones de autenticación, OTP, KYC y cuentas bancarias', () => {
    expect(typeof onboardingService.loginClient).toBe('function')
    expect(typeof onboardingService.sendOtp).toBe('function')
    expect(typeof onboardingService.verifyOtp).toBe('function')
    expect(typeof onboardingService.registerClient).toBe('function')
    expect(typeof onboardingService.logout).toBe('function')
    expect(typeof onboardingService.fetchProfile).toBe('function')
    expect(typeof onboardingService.submitKyc).toBe('function')
    expect(typeof onboardingService.submitConsents).toBe('function')
    expect(typeof onboardingService.fetchBankAccounts).toBe('function')
    expect(typeof onboardingService.registerBankAccount).toBe('function')
  })
})

describe('useClientAuth Composable (FE-CP-01 y FE-CP-02)', () => {
  it('valida formato de DNI y teléfono antes de solicitar código OTP', async () => {
    setActivePinia(createPinia())
    const auth = useClientAuth()

    auth.registerForm.value.docType = 'DNI'
    auth.registerForm.value.docNumber = '123' // Inválido (menos de 8)
    auth.registerForm.value.phone = '987654321'
    auth.registerForm.value.fullName = 'Juan Pérez'
    auth.registerForm.value.email = 'juan@correo.pe'
    auth.registerForm.value.password = 'Demo1234!'

    const ok = await auth.requestOtp()
    expect(ok).toBe(false)
    expect(auth.registerError.value).toContain('8 dígitos')

    // Corrige DNI pero celular inválido
    auth.registerForm.value.docNumber = '72345678'
    auth.registerForm.value.phone = '888000111' // No empieza con 9
    const okPhone = await auth.requestOtp()
    expect(okPhone).toBe(false)
    expect(auth.registerError.value).toContain('empezar con 9')
  })

  it('exige un código OTP de 6 dígitos para confirmar el registro', async () => {
    setActivePinia(createPinia())
    const auth = useClientAuth()

    auth.otpCode.value = '123' // Incompleto
    const ok = await auth.verifyOtpAndRegister()
    expect(ok).toBe(false)
    expect(auth.registerError.value).toContain('6 dígitos')
  })
})

describe('useKycOnboarding Composable (FE-CP-03 a FE-CP-05)', () => {
  it('controla el avance secuencial del stepper y valida requerimientos por paso', () => {
    setActivePinia(createPinia())
    const kyc = useKycOnboarding()

    // Paso 1: DNI
    expect(kyc.currentStep.value).toBe(1)
    expect(kyc.canProceed.value).toBe(false)

    kyc.simulateUploadDniFront()
    expect(kyc.canProceed.value).toBe(false) // Falta reverso

    kyc.simulateUploadDniBack()
    expect(kyc.canProceed.value).toBe(true)

    kyc.nextStep()
    expect(kyc.currentStep.value).toBe(2)

    // Paso 2: Selfie Biométrico
    expect(kyc.canProceed.value).toBe(false)
    kyc.simulateCaptureSelfie()
    expect(kyc.canProceed.value).toBe(true)

    kyc.nextStep()
    expect(kyc.currentStep.value).toBe(3)

    // Paso 3: Consentimientos Legales
    expect(kyc.canProceed.value).toBe(true) // Vienen pre-marcados los obligatorios
    kyc.consentDataProtection.value = false
    expect(kyc.canProceed.value).toBe(false) // No puede avanzar sin protección de datos

    kyc.consentDataProtection.value = true
    expect(kyc.canProceed.value).toBe(true)

    kyc.nextStep()
    expect(kyc.currentStep.value).toBe(4)

    // Paso 4: Cuenta de Desembolso
    kyc.payoutType.value = 'BANK'
    kyc.cci.value = '123' // CCI menor a 20 dígitos
    expect(kyc.canProceed.value).toBe(false)

    kyc.cci.value = '00219000000000010000' // 20 dígitos
    expect(kyc.canProceed.value).toBe(true)

    // Modo Billetera Digital
    kyc.payoutType.value = 'WALLET'
    kyc.phoneWallet.value = '987000123'
    expect(kyc.canProceed.value).toBe(true)

    // Navegación hacia atrás
    kyc.prevStep()
    expect(kyc.currentStep.value).toBe(3)
  })
})
