import { ref } from 'vue'
import type { DashboardKpis } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { toast } from 'vue-sonner'

import { reportsService } from '../services/reports.service'

export function useDashboardKpis() {
  const kpis = ref<DashboardKpis | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function loadKpis() {
    loading.value = true
    error.value = null
    try {
      kpis.value = await reportsService.fetchDashboardKpis()
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail
      toast.error('Error al cargar KPIs del dashboard', { description: apiErr.detail })
    } finally {
      loading.value = false
    }
  }

  function refresh() {
    return loadKpis()
  }

  return {
    kpis,
    loading,
    error,
    loadKpis,
    refresh,
  }
}
