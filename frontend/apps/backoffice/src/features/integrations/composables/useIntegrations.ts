import { ref, watch } from 'vue'
import { toast } from 'vue-sonner'
import type { IntegrationEvent, Page } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { integrationsService } from '../services/integrations.service'

export function useIntegrations() {
  const loading = ref(false)
  const eventsPage = ref<Page<IntegrationEvent>>({
    items: [],
    total: 0,
    page: 1,
    pageSize: 10,
  })

  const selectedStatus = ref('')
  const currentPage = ref(1)
  const retryingId = ref<string | null>(null)
  const totalPages = ref(1)

  watch(
    () => eventsPage.value.total,
    (total) => {
      totalPages.value = Math.max(1, Math.ceil(total / eventsPage.value.pageSize))
    },
    { immediate: true },
  )

  async function loadEvents() {
    loading.value = true
    try {
      eventsPage.value = await integrationsService.getEvents({
        page: currentPage.value,
        pageSize: eventsPage.value.pageSize,
        status: selectedStatus.value || undefined,
      })
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al cargar eventos de integración', { description: apiError.detail })
    } finally {
      loading.value = false
    }
  }

  async function retryEvent(event: IntegrationEvent) {
    retryingId.value = event.id
    try {
      const data = await integrationsService.retryEvent(event.id)
      toast.success('Evento reintentado', {
        description: `El evento ${data.externalId} fue reprocesado con estado ${data.status}.`,
      })
      void loadEvents()
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al reintentar evento', { description: apiError.detail })
    } finally {
      retryingId.value = null
    }
  }

  function goToPage(p: number) {
    if (p < 1 || p > totalPages.value) return
    currentPage.value = p
    void loadEvents()
  }

  return {
    loading,
    eventsPage,
    selectedStatus,
    currentPage,
    retryingId,
    totalPages,
    loadEvents,
    retryEvent,
    goToPage,
  }
}
