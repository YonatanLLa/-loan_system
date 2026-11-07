import { getCurrentInstance, ref } from 'vue'
import { useRouter } from 'vue-router'
import { toApiError } from '@prestamos/api-client'
import type { PaymentFrequency } from '@prestamos/api-client'
import { toast } from 'vue-sonner'

import { applicationsService } from '../services/applications.service'

export interface SubmitApplicationData {
  productId: string
  amount: number | string
  term: number
  frequency: PaymentFrequency
}

/**
 * Composable para la Solicitud de Préstamo con datos laborales y sustento de ingresos (FE-CP-07).
 */
export function useLoanApplication() {
  const router = getCurrentInstance() ? useRouter() : null

  const destination = ref('CONSUMO')
  const employerName = ref('')
  const occupation = ref('')
  const monthlyNetIncome = ref('3500.00')
  const incomeProof = ref<string | null>(null)

  const isSubmitting = ref(false)
  const error = ref<string | null>(null)

  function simulateUploadIncomeProof() {
    incomeProof.value = 'boleta_pago_sustento.pdf'
  }

  async function submit(data: SubmitApplicationData): Promise<boolean> {
    if (!data.productId || !data.amount || !data.term) {
      error.value = 'Configura los parámetros del préstamo en el simulador.'
      return false
    }

    if (!employerName.value.trim() || !occupation.value.trim() || !monthlyNetIncome.value) {
      error.value = 'Completa tus datos laborales e ingresos netos.'
      return false
    }

    if (!incomeProof.value) {
      error.value = 'Adjunta al menos un sustento de ingresos (boleta de pago o recibo de honorarios).'
      return false
    }

    isSubmitting.value = true
    error.value = null

    try {
      const created = await applicationsService.createApplication({
        productId: data.productId,
        amount: String(data.amount),
        term: data.term,
        frequency: data.frequency,
        destination: destination.value,
        employerName: employerName.value.trim(),
        occupation: occupation.value.trim(),
        monthlyNetIncome: monthlyNetIncome.value,
        incomeProofUrl: incomeProof.value,
      })

      toast.success('¡Solicitud enviada con éxito!', {
        description: `Código #${created.id}. Nuestro motor de riesgo está evaluando tu crédito.`,
      })

      await router?.push('/solicitudes')
      return true
    } catch (err) {
      const apiErr = toApiError(err)
      error.value =
        apiErr.status === 422 && apiErr.detail.includes('KYC')
          ? 'Debes verificar tu identidad antes de solicitar un préstamo.'
          : apiErr.detail || 'No se pudo enviar la solicitud de préstamo.'
      toast.error('Error al registrar solicitud', { description: error.value })
      return false
    } finally {
      isSubmitting.value = false
    }
  }

  return {
    destination,
    employerName,
    occupation,
    monthlyNetIncome,
    incomeProof,
    isSubmitting,
    error,
    simulateUploadIncomeProof,
    submit,
  }
}
