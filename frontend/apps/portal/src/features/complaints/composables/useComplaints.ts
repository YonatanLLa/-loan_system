import { ref } from 'vue'
import {
  toApiError,
  type ComplaintClaimRecord,
  type ComplaintClaimRequest,
} from '@prestamos/api-client'

import { complaintsService } from '../services/complaints.service'

export function useComplaints() {
  const form = ref<ComplaintClaimRequest>({
    claimType: 'RECLAMO',
    consumerFullName: '',
    docType: 'DNI',
    docNumber: '',
    email: '',
    phone: '',
    address: '',
    contractedGood: 'SERVICIO',
    amountClaimed: '',
    description: '',
    consumerOrder: '',
  })

  const isSubmitting = ref(false)
  const isQuerying = ref(false)
  const errorMessage = ref<string | null>(null)
  const registeredRecord = ref<ComplaintClaimRecord | null>(null)

  const queriedCorrelative = ref('')
  const queriedRecord = ref<ComplaintClaimRecord | null>(null)
  const queryError = ref<string | null>(null)

  const submitClaim = async (): Promise<ComplaintClaimRecord | null> => {
    // Validaciones
    if (!form.value.consumerFullName.trim()) {
      errorMessage.value = 'Indica tus nombres y apellidos completos.'
      return null
    }
    if (!form.value.docNumber.trim()) {
      errorMessage.value = 'Indica tu número de documento de identidad.'
      return null
    }
    if (!form.value.email.trim() || !form.value.email.includes('@')) {
      errorMessage.value = 'Indica un correo electrónico válido para recibir tu constancia.'
      return null
    }
    if (!form.value.phone.trim()) {
      errorMessage.value = 'Indica un número telefónico de contacto.'
      return null
    }
    if (!form.value.description.trim()) {
      errorMessage.value = 'Describe con claridad el motivo de tu reclamo o queja.'
      return null
    }
    if (!form.value.consumerOrder.trim()) {
      errorMessage.value = 'Indica qué solicitas a la empresa como pedido o solución concreta.'
      return null
    }

    isSubmitting.value = true
    errorMessage.value = null

    try {
      const record = await complaintsService.submitComplaint(form.value)
      registeredRecord.value = record
      return record
    } catch (err: unknown) {
      const apiErr = toApiError(err)
      errorMessage.value =
        apiErr.detail ||
        'No pudimos registrar tu hoja de reclamación. Intenta nuevamente.'
      return null
    } finally {
      isSubmitting.value = false
    }
  }

  const queryByCorrelative = async (correlative: string) => {
    if (!correlative.trim()) {
      queryError.value = 'Ingresa el número de hoja de reclamación (ej: LR-2026-00001).'
      return null
    }

    isQuerying.value = true
    queryError.value = null
    queriedRecord.value = null

    try {
      const record = await complaintsService.getComplaint(correlative.trim())
      queriedRecord.value = record
      return record
    } catch (err: unknown) {
      const apiErr = toApiError(err)
      queryError.value =
        apiErr.detail ||
        'No encontramos ninguna reclamación registrada con ese código.'
      return null
    } finally {
      isQuerying.value = false
    }
  }

  const resetForm = () => {
    form.value = {
      claimType: 'RECLAMO',
      consumerFullName: '',
      docType: 'DNI',
      docNumber: '',
      email: '',
      phone: '',
      address: '',
      contractedGood: 'SERVICIO',
      amountClaimed: '',
      description: '',
      consumerOrder: '',
    }
    registeredRecord.value = null
    errorMessage.value = null
  }

  return {
    form,
    isSubmitting,
    isQuerying,
    errorMessage,
    registeredRecord,
    queriedCorrelative,
    queriedRecord,
    queryError,
    submitClaim,
    queryByCorrelative,
    resetForm,
  }
}
