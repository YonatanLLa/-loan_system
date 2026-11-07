import { computed, ref } from 'vue'
import { toApiError } from '@prestamos/api-client'
import type { Installment, InstallmentStatus } from '@prestamos/api-client'

import { loansService } from '../services/loans.service'

/**
 * Composable para el Cronograma de Pagos y Semaforización de Cuotas (FE-CP-11).
 * Regla FE-CP-11: verde pagada, amarillo próxima, rojo vencida, azul parcial.
 */
export function useLoanSchedule() {
  const schedule = ref<Installment[]>([])
  const scheduleVersion = ref<number>(1)
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  /** Próxima cuota exigible (la primera vencida o la primera pendiente). */
  const nextInstallment = computed(() => {
    return (
      schedule.value.find((i) => i.status === 'OVERDUE') ||
      schedule.value.find((i) => i.status === 'PENDING' || i.status === 'PARTIAL') ||
      null
    )
  })

  /** Mora acumulada exigible del día. */
  const totalMoraAccrued = computed(() => {
    return schedule.value
      .filter((i) => i.status === 'OVERDUE' || Number(i.lateFee) > 0)
      .reduce((sum, i) => sum + (Number(i.lateFee) - Number(i.paidLateFee || '0')), 0)
      .toFixed(2)
  })

  /** Días restantes para el vencimiento o días de retraso en mora (FE-CP-10). */
  const dueCountdownText = computed(() => {
    const inst = nextInstallment.value
    if (!inst) return 'Al día en tus pagos'
    if (inst.status === 'OVERDUE' || inst.daysLate > 0) {
      return `${inst.daysLate} día${inst.daysLate === 1 ? '' : 's'} de atraso`
    }
    // Calcula días restantes aproximados comparando fechas
    const todayDate = new Date()
    todayDate.setHours(0, 0, 0, 0)
    const dueDate = new Date(inst.dueDate + 'T00:00:00')
    const diffMs = dueDate.getTime() - todayDate.getTime()
    const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24))

    if (diffDays <= 0) return 'Vence hoy'
    if (diffDays === 1) return 'Vence mañana'
    return `Vence en ${diffDays} días`
  })

  /**
   * Estilo de color semaforizado por estado de cuota según la norma FE-CP-11:
   * - PAID: Verde
   * - PENDING: Amarillo
   * - OVERDUE: Rojo
   * - PARTIAL: Azul
   */
  function getInstallmentColorClass(inst: Installment): string {
    switch (inst.status) {
      case 'PAID':
        return 'text-success bg-success/10 border-success/30'
      case 'OVERDUE':
        return 'text-destructive bg-destructive/10 border-destructive/30'
      case 'PARTIAL':
        return 'text-primary bg-primary/10 border-primary/30'
      case 'PENDING':
        return 'text-amber-700 bg-amber-500/10 border-amber-500/30'
      case 'CANCELLED_BY_PREPAYMENT':
        return 'text-muted-foreground bg-muted border-border'
      default:
        return 'text-muted-foreground bg-muted border-border'
    }
  }

  function getInstallmentStatusLabel(status: InstallmentStatus): string {
    switch (status) {
      case 'PAID':
        return 'Pagada'
      case 'OVERDUE':
        return 'Vencida en Mora'
      case 'PARTIAL':
        return 'Pago Parcial'
      case 'PENDING':
        return 'Pendiente'
      case 'CANCELLED_BY_PREPAYMENT':
        return 'Cancelada por Prepago'
      default:
        return status
    }
  }

  async function loadSchedule(loanId: string): Promise<void> {
    if (!loanId) return
    isLoading.value = true
    error.value = null
    try {
      const data = await loansService.fetchLoanSchedule(loanId)
      schedule.value = data.installments
      scheduleVersion.value = data.version
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail || 'No se pudo cargar el cronograma.'
      schedule.value = []
    } finally {
      isLoading.value = false
    }
  }

  return {
    schedule,
    scheduleVersion,
    nextInstallment,
    totalMoraAccrued,
    dueCountdownText,
    isLoading,
    error,
    getInstallmentColorClass,
    getInstallmentStatusLabel,
    loadSchedule,
  }
}
