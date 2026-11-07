import { ref } from 'vue'
import type { LoanProduct } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { toast } from 'vue-sonner'

import { collectionsService, type CollectorOption } from '../services/collections.service'

export function useCollectionsCatalogs() {
  const collectors = ref<CollectorOption[]>([])
  const products = ref<LoanProduct[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function loadCatalogs() {
    loading.value = true
    error.value = null
    try {
      const [people, catalog] = await Promise.all([
        collectionsService.fetchCollectors(),
        collectionsService.fetchActiveProducts(),
      ])
      collectors.value = people
      products.value = catalog
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail
      toast.error('No se pudieron cargar los filtros de cobranza', { description: apiErr.detail })
    } finally {
      loading.value = false
    }
  }

  return {
    collectors,
    products,
    loading,
    error,
    loadCatalogs,
  }
}
