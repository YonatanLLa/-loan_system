import { computed, ref } from 'vue'
import { toApiError } from '@prestamos/api-client'
import type { Loan } from '@prestamos/api-client'

import { loansService } from '../services/loans.service'

/**
 * Composable para la consulta de préstamos del cliente prestatario (FE-CP-10).
 */
export function useClientLoans() {
  const loans = ref<Loan[]>([])
  const selectedLoanId = ref<string>('')
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  const activeLoan = computed(() => {
    if (selectedLoanId.value) {
      const match = loans.value.find((l) => l.id === selectedLoanId.value)
      if (match) return match
    }
    // Prioriza el préstamo activo o en mora
    return (
      loans.value.find((l) => l.status === 'EN_MORA' || l.status === 'VIGENTE') ||
      loans.value[0] ||
      null
    )
  })

  const hasLoans = computed(() => loans.value.length > 0)
  const hasOverdueLoans = computed(() =>
    loans.value.some((l) => l.status === 'EN_MORA'),
  )

  const totalOutstanding = computed(() => {
    return loans.value
      .filter((l) => l.status === 'VIGENTE' || l.status === 'EN_MORA')
      .reduce((sum, l) => sum + Number(l.outstandingPrincipal || '0'), 0)
      .toFixed(2)
  })

  async function loadLoans(): Promise<void> {
    isLoading.value = true
    error.value = null
    try {
      loans.value = await loansService.fetchMyLoans()
      if (loans.value.length > 0 && !selectedLoanId.value) {
        selectedLoanId.value = activeLoan.value?.id || loans.value[0]!.id
      }
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail || 'No se pudieron cargar tus préstamos.'
    } finally {
      isLoading.value = false
    }
  }

  return {
    loans,
    selectedLoanId,
    activeLoan,
    hasLoans,
    hasOverdueLoans,
    totalOutstanding,
    isLoading,
    error,
    loadLoans,
  }
}
