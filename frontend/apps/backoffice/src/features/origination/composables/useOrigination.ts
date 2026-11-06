import { ref, watch } from 'vue'
import { toast } from 'vue-sonner'
import type { LoanApplication, Page } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { originationService } from '../services/origination.service'

export function useOrigination() {
  const loading = ref(false)
  const applicationsPage = ref<Page<LoanApplication>>({
    items: [],
    total: 0,
    page: 1,
    pageSize: 10,
  })

  const searchQuery = ref('')
  const selectedStatus = ref('SOLICITADO,EN_EVALUACION')
  const currentPage = ref(1)
  const totalPages = ref(1)

  watch(
    () => applicationsPage.value.total,
    (total) => {
      totalPages.value = Math.max(1, Math.ceil(total / applicationsPage.value.pageSize))
    },
    { immediate: true },
  )

  async function loadApplications() {
    loading.value = true
    try {
      applicationsPage.value = await originationService.getApplications({
        page: currentPage.value,
        pageSize: applicationsPage.value.pageSize,
        q: searchQuery.value,
        status: selectedStatus.value,
      })
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al cargar solicitudes', { description: apiError.detail })
    } finally {
      loading.value = false
    }
  }

  function onSearch() {
    currentPage.value = 1
    void loadApplications()
  }

  function resetFilters() {
    searchQuery.value = ''
    selectedStatus.value = ''
    currentPage.value = 1
    void loadApplications()
  }

  function goToPage(p: number) {
    if (p < 1 || p > totalPages.value) return
    currentPage.value = p
    void loadApplications()
  }

  return {
    loading,
    applicationsPage,
    searchQuery,
    selectedStatus,
    currentPage,
    totalPages,
    loadApplications,
    onSearch,
    resetFilters,
    goToPage,
    getApplication: originationService.getApplication,
    simulateExistingApplication: originationService.simulateExistingApplication,
    simulateNewApplication: originationService.simulateNewApplication,
    createApplication: originationService.createApplication,
    startReview: originationService.startReview,
    submitDecision: originationService.submitDecision,
    getCustomer360: originationService.getCustomer360,
    getActiveProducts: originationService.getActiveProducts,
    getCustomersDirectory: originationService.getCustomersDirectory,
  }
}
