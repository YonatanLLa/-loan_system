import { ref, computed, onMounted, getCurrentInstance } from 'vue'
import { toApiError, type Payment, type PaymentStatus } from '@prestamos/api-client'

import { paymentsService } from '../services/payments.service'

export function usePaymentHistory() {
  const payments = ref<Payment[]>([])
  const isLoading = ref(false)
  const errorMessage = ref<string | null>(null)
  const statusFilter = ref<'ALL' | PaymentStatus>('ALL')

  const fetchHistory = async () => {
    isLoading.value = true
    errorMessage.value = null
    try {
      payments.value = await paymentsService.getPaymentHistory()
    } catch (err: unknown) {
      const apiErr = toApiError(err)
      errorMessage.value =
        apiErr.detail || 'No pudimos cargar el historial de pagos.'
    } finally {
      isLoading.value = false
    }
  }

  const filteredPayments = computed(() => {
    if (statusFilter.value === 'ALL') {
      return payments.value
    }
    return payments.value.filter((p) => p.status === statusFilter.value)
  })

  const pendingCount = computed(
    () => payments.value.filter((p) => p.status === 'PENDING_CONFIRMATION').length,
  )

  const appliedCount = computed(
    () => payments.value.filter((p) => p.status === 'APPLIED').length,
  )

  const downloadReceipt = async (payment: Payment) => {
    try {
      await paymentsService.downloadReceiptPdf(payment.id, payment.receiptNumber)
    } catch {
      errorMessage.value = 'No se pudo descargar la constancia de pago.'
    }
  }

  if (getCurrentInstance()) {
    onMounted(() => {
      fetchHistory()
    })
  }

  return {
    payments,
    filteredPayments,
    isLoading,
    errorMessage,
    statusFilter,
    pendingCount,
    appliedCount,
    fetchHistory,
    downloadReceipt,
  }
}
