import { ref } from 'vue'
import {
  toApiError,
  type PartialPrepaymentSimulation,
  type PrepaymentQuote,
} from '@prestamos/api-client'

import { paymentsService } from '../services/payments.service'

export function usePrepayment() {
  const quote = ref<PrepaymentQuote | null>(null)
  const isQuoting = ref(false)
  const quoteError = ref<string | null>(null)

  const partialSimulation = ref<PartialPrepaymentSimulation | null>(null)
  const isSimulatingPartial = ref(false)
  const partialError = ref<string | null>(null)

  const partialAmount = ref<string>('')
  const partialOption = ref<'REDUCE_TERM' | 'REDUCE_FEE'>('REDUCE_TERM')

  /**
   * Cotiza la liquidación por cancelación anticipada total (FE-CP-16 / US-30).
   */
  const loadTotalPrepaymentQuote = async (loanId: string, asOfDate?: string) => {
    isQuoting.value = true
    quoteError.value = null
    try {
      quote.value = await paymentsService.quotePrepayment(loanId, asOfDate)
      return quote.value
    } catch (err: unknown) {
      const apiErr = toApiError(err)
      quoteError.value =
        apiErr.detail || 'No pudimos cotizar la liquidación anticipada.'
      return null
    } finally {
      isQuoting.value = false
    }
  }

  /**
   * Simula el impacto de un abono extraordinario a capital (FE-CP-17).
   */
  const simulatePartialPrepayment = async (
    loanId: string,
    amount?: string,
    option?: 'REDUCE_TERM' | 'REDUCE_FEE',
  ) => {
    const targetAmount = amount ?? partialAmount.value
    const targetOption = option ?? partialOption.value

    if (!targetAmount || parseFloat(targetAmount) <= 0) {
      partialError.value = 'Ingresa un monto de abono mayor a cero.'
      return null
    }

    isSimulatingPartial.value = true
    partialError.value = null

    try {
      const result = await paymentsService.simulatePartialPrepayment(
        loanId,
        parseFloat(targetAmount).toFixed(2),
        targetOption,
      )
      partialSimulation.value = result
      return result
    } catch (err: unknown) {
      const apiErr = toApiError(err)
      partialError.value =
        apiErr.detail || 'No pudimos simular el abono extraordinario.'
      return null
    } finally {
      isSimulatingPartial.value = false
    }
  }

  const resetQuote = () => {
    quote.value = null
    quoteError.value = null
  }

  const resetPartial = () => {
    partialSimulation.value = null
    partialError.value = null
    partialAmount.value = ''
  }

  return {
    quote,
    isQuoting,
    quoteError,
    partialSimulation,
    isSimulatingPartial,
    partialError,
    partialAmount,
    partialOption,
    loadTotalPrepaymentQuote,
    simulatePartialPrepayment,
    resetQuote,
    resetPartial,
  }
}
