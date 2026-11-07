import { describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { applicationsService } from '../features/applications/services'
import {
  useApplicationsTimeline,
  useLoanApplication,
  useSimulator,
} from '../features/applications/composables'

describe('Applications Service (FE-CP-06 a FE-CP-08)', () => {
  it('expone todas las operaciones de productos, simulación y solicitudes', () => {
    expect(typeof applicationsService.fetchActiveProducts).toBe('function')
    expect(typeof applicationsService.simulate).toBe('function')
    expect(typeof applicationsService.createApplication).toBe('function')
    expect(typeof applicationsService.fetchMyApplications).toBe('function')
    expect(typeof applicationsService.fetchApplicationById).toBe('function')
  })
})

describe('useSimulator Composable (FE-CP-06)', () => {
  it('inicializa con valores por defecto y expone propiedades reactivas de simulación', () => {
    setActivePinia(createPinia())
    const sim = useSimulator()

    expect(sim.amount.value).toBe(2000)
    expect(sim.term.value).toBe(12)
    expect(sim.frequency.value).toBe('MENSUAL')
    expect(sim.simulation.value).toBeNull()
    expect(sim.isCalculating.value).toBe(false)
  })
})

describe('useLoanApplication Composable (FE-CP-07)', () => {
  it('exige datos de empleador, ocupación, ingreso y sustento adjunto antes de enviar', async () => {
    setActivePinia(createPinia())
    const app = useLoanApplication()

    // Intento con campos vacíos
    const ok = await app.submit({
      productId: 'prod-001',
      amount: 2500,
      term: 12,
      frequency: 'MENSUAL',
    })

    expect(ok).toBe(false)
    expect(app.error.value).toContain('datos laborales')

    // Llena datos laborales pero sin sustento de ingresos
    app.employerName.value = 'Empresa Demo SAC'
    app.occupation.value = 'Asistente Administrativo'
    app.monthlyNetIncome.value = '3200.00'

    const okWithoutProof = await app.submit({
      productId: 'prod-001',
      amount: 2500,
      term: 12,
      frequency: 'MENSUAL',
    })

    expect(okWithoutProof).toBe(false)
    expect(app.error.value).toContain('sustento de ingresos')

    // Adjunta sustento
    app.simulateUploadIncomeProof()
    expect(app.incomeProof.value).toBe('boleta_pago_sustento.pdf')
  })
})

describe('useApplicationsTimeline Composable (FE-CP-08)', () => {
  it('construye la línea de tiempo de 4 pasos secuenciales según el estado del crédito', () => {
    setActivePinia(createPinia())
    const timeline = useApplicationsTimeline()

    // Estado: SOLICITADO
    const stepsRequested = timeline.getTimelineSteps('SOLICITADO')
    expect(stepsRequested).toHaveLength(4)
    expect(stepsRequested[0]?.isDone).toBe(true)
    expect(stepsRequested[0]?.isCurrent).toBe(true)
    expect(stepsRequested[1]?.isDone).toBe(false)

    // Estado: EN_EVALUACION
    const stepsReview = timeline.getTimelineSteps('EN_EVALUACION')
    expect(stepsReview[0]?.isDone).toBe(true)
    expect(stepsReview[1]?.isDone).toBe(true)
    expect(stepsReview[1]?.isCurrent).toBe(true)
    expect(stepsReview[2]?.isDone).toBe(false)

    // Estado: APROBADO
    const stepsApproved = timeline.getTimelineSteps('APROBADO')
    expect(stepsApproved[0]?.isDone).toBe(true)
    expect(stepsApproved[1]?.isDone).toBe(true)
    expect(stepsApproved[2]?.isDone).toBe(true)
    expect(stepsApproved[2]?.isCurrent).toBe(true)
    expect(stepsApproved[3]?.isDone).toBe(false)

    // Estado: DESEMBOLSADO
    const stepsDisbursed = timeline.getTimelineSteps('DESEMBOLSADO')
    expect(stepsDisbursed.every((s) => s.isDone)).toBe(true)
    expect(stepsDisbursed[3]?.isCurrent).toBe(true)
  })
})
