import { ref } from 'vue'
import type { CollectionAction } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { toast } from 'vue-sonner'

import {
  collectionsService,
  type CreateCollectionActionPayload,
} from '../services/collections.service'

export function useCollectionActions() {
  const history = ref<CollectionAction[]>([])
  const loading = ref(false)
  const isSubmitting = ref(false)
  const error = ref<string | null>(null)

  async function loadHistory(loanId: string) {
    if (!loanId) return
    loading.value = true
    error.value = null
    try {
      history.value = await collectionsService.fetchCollectionActions(loanId)
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail
      toast.error('No se pudo cargar la bitácora de cobranzas', { description: apiErr.detail })
    } finally {
      loading.value = false
    }
  }

  async function recordAction(payload: CreateCollectionActionPayload): Promise<CollectionAction> {
    isSubmitting.value = true
    error.value = null
    try {
      const created = await collectionsService.createCollectionAction(payload)
      history.value.unshift(created)
      return created
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail
      toast.error('No se pudo registrar la gestión', { description: apiErr.detail })
      throw err
    } finally {
      isSubmitting.value = false
    }
  }

  return {
    history,
    loading,
    isSubmitting,
    error,
    loadHistory,
    recordAction,
  }
}
