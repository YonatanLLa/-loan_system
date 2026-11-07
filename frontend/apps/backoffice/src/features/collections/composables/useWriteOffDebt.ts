import { ref } from 'vue'
import { toApiError } from '@prestamos/api-client'
import { toast } from 'vue-sonner'

import { collectionsService, type WriteOffPayload } from '../services/collections.service'

export function useWriteOffDebt() {
  const isBusy = ref(false)
  const error = ref<string | null>(null)

  async function writeOff(loanId: string, payload: WriteOffPayload): Promise<boolean> {
    if (!loanId || !payload.reason.trim() || !payload.minuteRef.trim() || !payload.approvedBy) {
      return false
    }
    isBusy.value = true
    error.value = null
    try {
      await collectionsService.writeOffLoan(loanId, payload)
      toast.success('Deuda castigada exitosamente', {
        description: `${loanId} pasó a CASTIGADO. El historial crediticio del deudor se conserva.`,
      })
      return true
    } catch (err) {
      const apiErr = toApiError(err)
      error.value =
        apiErr.status === 409 ? 'Este préstamo ya se encuentra castigado.' : apiErr.detail
      toast.error(apiErr.title, { description: error.value })
      return false
    } finally {
      isBusy.value = false
    }
  }

  return {
    isBusy,
    error,
    writeOff,
  }
}
