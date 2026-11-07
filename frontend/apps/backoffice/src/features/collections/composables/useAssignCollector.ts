import { ref } from 'vue'
import { toApiError } from '@prestamos/api-client'
import { toast } from 'vue-sonner'

import { collectionsService } from '../services/collections.service'

export function useAssignCollector() {
  const isBusy = ref(false)
  const error = ref<string | null>(null)

  async function assign(
    loanId: string,
    collectorName: string,
    customerName?: string,
  ): Promise<boolean> {
    if (!loanId || !collectorName) return false
    isBusy.value = true
    error.value = null
    try {
      await collectionsService.assignCollector(loanId, collectorName)
      toast.success('Gestor asignado', {
        description: customerName
          ? `${customerName} quedó a cargo de ${collectorName}.`
          : `Crédito asignado a ${collectorName}.`,
      })
      return true
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail
      toast.error('No se pudo asignar el gestor', { description: apiErr.detail })
      return false
    } finally {
      isBusy.value = false
    }
  }

  return {
    isBusy,
    error,
    assign,
  }
}
