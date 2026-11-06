import { ref } from 'vue'
import { toApiError } from '@prestamos/api-client'
import type { ApplicationStatus, LoanApplication } from '@prestamos/api-client'

import { applicationsService } from '../services/applications.service'

export interface TimelineStep {
  key: string
  label: string
  description: string
  isDone: boolean
  isCurrent: boolean
}

/**
 * Composable para la Línea de Tiempo de Solicitudes de Préstamo (FE-CP-08).
 * Rastrea la evolución: SOLICITADO → EN_EVALUACION → APROBADO → DESEMBOLSADO.
 */
export function useApplicationsTimeline() {
  const applications = ref<LoanApplication[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  async function loadApplications(): Promise<void> {
    isLoading.value = true
    error.value = null
    try {
      applications.value = await applicationsService.fetchMyApplications()
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail
    } finally {
      isLoading.value = false
    }
  }

  /** Genera los 4 hitos secuenciales de la línea de tiempo para una solicitud. */
  function getTimelineSteps(status: ApplicationStatus): TimelineStep[] {
    const isRejected = status === 'RECHAZADO'
    const isDisbursed = status === 'DESEMBOLSADO'
    const isApproved = status === 'APROBADO' || status === 'APROBADO_CON_CONDICIONES'
    const isReview = status === 'EN_EVALUACION'
    const isRequested = status === 'SOLICITADO'

    return [
      {
        key: 'SOLICITADO',
        label: '1. Solicitud Recibida',
        description: 'Tus datos laborales y de contacto fueron ingresados correctamente.',
        isDone: true,
        isCurrent: isRequested,
      },
      {
        key: 'EN_EVALUACION',
        label: '2. Evaluación Crediticia',
        description: 'Validación de tu capacidad de pago y score ante la SBS y centrales de riesgo.',
        isDone: isReview || isApproved || isDisbursed,
        isCurrent: isReview,
      },
      {
        key: 'APROBADO',
        label: isRejected ? 'Dictamen: No Aprobado' : '3. Crédito Aprobado',
        description: isRejected
          ? 'No fue posible aprobar el crédito en esta oportunidad por políticas de riesgo.'
          : 'Monto y condiciones aprobados. Listo para firma de contrato y pagaré digital.',
        isDone: isApproved || isDisbursed,
        isCurrent: isApproved,
      },
      {
        key: 'DESEMBOLSADO',
        label: '4. Dinero Desembolsado',
        description: 'Transferencia realizada a tu cuenta bancaria o billetera registrada.',
        isDone: isDisbursed,
        isCurrent: isDisbursed,
      },
    ]
  }

  return {
    applications,
    isLoading,
    error,
    loadApplications,
    getTimelineSteps,
  }
}
