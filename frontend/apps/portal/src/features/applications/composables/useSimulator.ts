import { computed, ref, watch } from 'vue'
import { toApiError } from '@prestamos/api-client'
import type { LoanProduct, PaymentFrequency, Simulation } from '@prestamos/api-client'

import { applicationsService } from '../services/applications.service'

/**
 * Composable para el Simulador Interactivo de Préstamos (FE-CP-06).
 * Recalcula cuotas en tiempo real mostrando TEA, TCEA, comisiones y cronograma.
 */
export function useSimulator() {
  const products = ref<LoanProduct[]>([])
  const selectedProductId = ref<string>('')
  const amount = ref<number>(2000)
  const term = ref<number>(12)
  const frequency = ref<PaymentFrequency>('MENSUAL')

  const simulation = ref<Simulation | null>(null)
  const isCalculating = ref(false)
  const productsLoading = ref(false)
  const error = ref<string | null>(null)

  const selectedProduct = computed(() => {
    return products.value.find((p) => p.id === selectedProductId.value) || products.value[0] || null
  })

  // Rangos mínimos y máximos según el producto seleccionado
  const minAmount = computed(() => (selectedProduct.value ? Number(selectedProduct.value.minAmount) : 500))
  const maxAmount = computed(() => (selectedProduct.value ? Number(selectedProduct.value.maxAmount) : 10000))
  const minTerm = computed(() => selectedProduct.value?.minTerm ?? 3)
  const maxTerm = computed(() => selectedProduct.value?.maxTerm ?? 36)

  // Resumen formateado para la interfaz
  const teaDisplay = computed(() => {
    if (!simulation.value?.summary.tea) return ''
    return `${(Number(simulation.value.summary.tea) * 100).toFixed(2)}%`
  })

  const tceaDisplay = computed(() => {
    if (!simulation.value?.summary.tcea) return ''
    return `${(Number(simulation.value.summary.tcea) * 100).toFixed(2)}%`
  })

  const installmentAmount = computed(() => simulation.value?.summary.installmentAmount ?? '0.00')
  const totalInterest = computed(() => simulation.value?.summary.totalInterest ?? '0.00')
  const totalFees = computed(() => simulation.value?.summary.totalFees ?? '0.00')
  const totalCost = computed(() => simulation.value?.summary.totalCost ?? '0.00')
  const scheduleRows = computed(() => simulation.value?.installments ?? [])

  async function loadProducts(): Promise<void> {
    productsLoading.value = true
    try {
      products.value = await applicationsService.fetchActiveProducts()
      if (products.value.length > 0 && !selectedProductId.value) {
        selectedProductId.value = products.value[0]!.id
        amount.value = Math.max(Number(products.value[0]!.minAmount), 2000)
        term.value = Math.max(products.value[0]!.minTerm, 12)
        frequency.value = products.value[0]!.allowedFrequencies[0] ?? 'MENSUAL'
        await calculate()
      }
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail
    } finally {
      productsLoading.value = false
    }
  }

  async function calculate(): Promise<void> {
    if (!selectedProductId.value || !amount.value || !term.value) return
    isCalculating.value = true
    error.value = null
    try {
      simulation.value = await applicationsService.simulate({
        productId: selectedProductId.value,
        amount: String(amount.value),
        term: term.value,
        frequency: frequency.value,
      })
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail
      simulation.value = null
    } finally {
      isCalculating.value = false
    }
  }

  // Recalcula automáticamente ante cambios en los parámetros del simulador
  let debounceTimer: ReturnType<typeof setTimeout> | null = null
  watch([selectedProductId, amount, term, frequency], () => {
    if (debounceTimer) clearTimeout(debounceTimer)
    debounceTimer = setTimeout(() => {
      void calculate()
    }, 250)
  })

  // Ajusta montos y plazos cuando cambia el producto si caen fuera de rango
  watch(selectedProduct, (prod) => {
    if (prod) {
      if (amount.value < Number(prod.minAmount)) amount.value = Number(prod.minAmount)
      if (amount.value > Number(prod.maxAmount)) amount.value = Number(prod.maxAmount)
      if (term.value < prod.minTerm) term.value = prod.minTerm
      if (term.value > prod.maxTerm) term.value = prod.maxTerm
      if (!prod.allowedFrequencies.includes(frequency.value)) {
        frequency.value = prod.allowedFrequencies[0] ?? 'MENSUAL'
      }
    }
  })

  return {
    products,
    selectedProductId,
    selectedProduct,
    amount,
    term,
    frequency,
    minAmount,
    maxAmount,
    minTerm,
    maxTerm,
    simulation,
    isCalculating,
    productsLoading,
    error,
    teaDisplay,
    tceaDisplay,
    installmentAmount,
    totalInterest,
    totalFees,
    totalCost,
    scheduleRows,
    loadProducts,
    calculate,
  }
}
