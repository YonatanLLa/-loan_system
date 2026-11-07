import { ref, watch } from 'vue'
import { toast } from 'vue-sonner'
import type { Loan, Page } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { loansService } from '../services/loans.service'

export function useLoans() {
  const loading = ref(false)
  const loansPage = ref<Page<Loan>>({
    items: [],
    total: 0,
    page: 1,
    pageSize: 10,
  })

  const searchQuery = ref('')
  const selectedStatus = ref('')
  const currentPage = ref(1)
  const totalPages = ref(1)

  watch(
    () => loansPage.value.total,
    (total) => {
      totalPages.value = Math.max(1, Math.ceil(total / loansPage.value.pageSize))
    },
    { immediate: true },
  )

  async function loadLoans() {
    loading.value = true
    try {
      loansPage.value = await loansService.getLoans({
        page: currentPage.value,
        pageSize: loansPage.value.pageSize,
        q: searchQuery.value,
        status: selectedStatus.value,
      })
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al cargar préstamos', { description: apiError.detail })
    } finally {
      loading.value = false
    }
  }

  function onSearch() {
    currentPage.value = 1
    void loadLoans()
  }

  function resetFilters() {
    searchQuery.value = ''
    selectedStatus.value = ''
    currentPage.value = 1
    void loadLoans()
  }

  function goToPage(p: number) {
    if (p < 1 || p > totalPages.value) return
    currentPage.value = p
    void loadLoans()
  }

  return {
    loading,
    loansPage,
    searchQuery,
    selectedStatus,
    currentPage,
    totalPages,
    loadLoans,
    onSearch,
    resetFilters,
    goToPage,
    getLoan: loansService.getLoan,
    getSchedule: loansService.getSchedule,
    getLoanPayments: loansService.getLoanPayments,
    getCustomer: loansService.getCustomer,
    getDisbursement: loansService.getDisbursement,
    downloadContractPdf: loansService.downloadContractPdf,
    getApprovedApplications: loansService.getApprovedApplications,
    getCompanyDisbursementAccounts: loansService.getCompanyDisbursementAccounts,
    getCustomerVerifiedAccounts: loansService.getCustomerVerifiedAccounts,
    createDisbursementLoan: loansService.createDisbursementLoan,
    quotePrepayment: loansService.quotePrepayment,
    payPrepayment: loansService.payPrepayment,
    restructureLoan: loansService.restructureLoan,
    refinanceLoans: loansService.refinanceLoans,
  }
}
