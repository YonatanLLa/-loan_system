import { ref, watch } from 'vue'
import { toast } from 'vue-sonner'
import type { Page, Payment } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { paymentsService } from '../services/payments.service'

export function usePayments() {
  const loading = ref(false)
  const paymentsPage = ref<Page<Payment>>({
    items: [],
    total: 0,
    page: 1,
    pageSize: 10,
  })

  const activeTab = ref<'pending' | 'all'>('pending')
  const searchQuery = ref('')
  const currentPage = ref(1)
  const confirmingId = ref<string | null>(null)
  const totalPages = ref(1)

  watch(
    () => paymentsPage.value.total,
    (total) => {
      totalPages.value = Math.max(1, Math.ceil(total / paymentsPage.value.pageSize))
    },
    { immediate: true },
  )

  async function loadPayments() {
    loading.value = true
    try {
      paymentsPage.value = await paymentsService.getPayments({
        page: currentPage.value,
        pageSize: paymentsPage.value.pageSize,
        q: searchQuery.value,
        status: activeTab.value === 'pending' ? 'PENDING_CONFIRMATION' : undefined,
      })
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al cargar pagos', { description: apiError.detail })
    } finally {
      loading.value = false
    }
  }

  async function confirmVoucher(payment: Payment) {
    confirmingId.value = payment.id
    try {
      await paymentsService.confirmPayment(payment.id)
      toast.success('Pago confirmado y aplicado', {
        description: `El voucher por S/ ${payment.amount} fue conciliado y amortizado exitosamente.`,
      })
      void loadPayments()
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al confirmar pago', { description: apiError.detail })
    } finally {
      confirmingId.value = null
    }
  }

  function onSearch() {
    currentPage.value = 1
    void loadPayments()
  }

  function switchTab(tab: 'pending' | 'all') {
    activeTab.value = tab
    currentPage.value = 1
    void loadPayments()
  }

  function goToPage(p: number) {
    if (p < 1 || p > totalPages.value) return
    currentPage.value = p
    void loadPayments()
  }

  return {
    loading,
    paymentsPage,
    activeTab,
    searchQuery,
    currentPage,
    confirmingId,
    totalPages,
    loadPayments,
    confirmVoucher,
    onSearch,
    switchTab,
    goToPage,
    reversePayment: paymentsService.reversePayment,
    registerPayment: paymentsService.registerPayment,
    getActiveLoans: paymentsService.getActiveLoans,
    getCollectionBankAccounts: paymentsService.getCollectionBankAccounts,
  }
}
