import { ref } from 'vue'
import { toApiError } from '@prestamos/api-client'
import type { LoanContractDocument, SignContractResponse } from '@prestamos/api-client'
import { toast } from 'vue-sonner'

import { contractService } from '../services/contract.service'

/**
 * Composable para la revisión y firma electrónica de contratos con código OTP (FE-CP-09).
 */
export function useContractSigning() {
  const contract = ref<LoanContractDocument | null>(null)
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  const isOtpModalOpen = ref(false)
  const otpCode = ref('')
  const otpCountdown = ref(0)
  const isSigning = ref(false)
  const isDownloading = ref(false)
  const signedSuccess = ref(false)
  const signatureResult = ref<SignContractResponse | null>(null)

  let timerHandle: ReturnType<typeof setInterval> | null = null

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

  async function loadContract(applicationId?: string): Promise<void> {
    isLoading.value = true
    error.value = null
    try {
      contract.value = await contractService.fetchContract(applicationId)
      signedSuccess.value = contract.value.isSigned
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail || 'No se pudo cargar el contrato digital.'
      contract.value = null
    } finally {
      isLoading.value = false
    }
  }

  async function requestOtp(phone = '987000001'): Promise<boolean> {
    error.value = null
    try {
      const res = await contractService.requestSigningOtp(phone)
      isOtpModalOpen.value = true
      startCountdown(res.expiresInSeconds || 60)
      toast.info('Código de firma enviado', {
        description: res.demoCode
          ? `Código de prueba: ${res.demoCode}`
          : res.message,
      })
      return true
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail
      toast.error('Error al enviar código OTP', { description: apiErr.detail })
      return false
    }
  }

  async function signWithOtp(): Promise<boolean> {
    if (!contract.value?.applicationId || !otpCode.value.trim() || otpCode.value.trim().length < 6) {
      error.value = 'Ingresa el código OTP completo de 6 dígitos.'
      return false
    }

    isSigning.value = true
    error.value = null

    try {
      const res = await contractService.signContract({
        applicationId: contract.value.applicationId,
        otpCode: otpCode.value.trim(),
      })

      signatureResult.value = res
      signedSuccess.value = true
      if (contract.value) {
        contract.value.isSigned = true
        contract.value.loanId = res.loanId
        contract.value.signatureEvidence = {
          signedAt: res.signedAt,
          signedIp: '190.235.12.84',
          otpCode: otpCode.value.trim(),
          verified: true,
        }
      }

      isOtpModalOpen.value = false
      toast.success('¡Contrato firmado electrónicamente!', {
        description: 'Tu dinero ha sido transferido a tu cuenta bancaria registrada.',
      })
      return true
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail || 'Error al validar el código OTP.'
      toast.error('No se pudo firmar el contrato', { description: error.value })
      return false
    } finally {
      isSigning.value = false
    }
  }

  async function downloadPdf(): Promise<void> {
    if (!contract.value) return
    isDownloading.value = true
    try {
      await contractService.downloadContractPdf(`contrato_${contract.value.applicationId}`)
      toast.success('Descarga iniciada', {
        description: 'Documento PDF con constancia de firma electrónica y QR generado.',
      })
    } catch {
      toast.error('No se pudo descargar el documento.')
    } finally {
      isDownloading.value = false
    }
  }

  return {
    contract,
    isLoading,
    error,
    isOtpModalOpen,
    otpCode,
    otpCountdown,
    isSigning,
    isDownloading,
    signedSuccess,
    signatureResult,
    loadContract,
    requestOtp,
    signWithOtp,
    downloadPdf,
  }
}
