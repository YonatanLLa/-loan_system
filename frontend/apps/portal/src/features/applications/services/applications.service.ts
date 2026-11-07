import type {
  CreateApplicationRequest,
  LoanApplication,
  LoanProduct,
  SimulateRequest,
  Simulation,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

/**
 * Servicio de Simulación y Solicitudes de Préstamo para el Portal del Cliente (FE-CP-06 a FE-CP-08).
 * Encapsula la comunicación con la API / Mocks.
 */
export const applicationsService = {
  /** Obtiene la lista de productos de crédito activos para el cliente. */
  async fetchActiveProducts(): Promise<LoanProduct[]> {
    const { data } = await useApi().get<LoanProduct[]>('/loan-products', {
      params: { status: 'ACTIVO' },
    })
    return data
  },

  /** Simula un préstamo en memoria calculando TEA, TCEA, cuota y cronograma (FE-CP-06). */
  async simulate(payload: SimulateRequest): Promise<Simulation> {
    const { data } = await useApi().post<Simulation>('/loan-applications/simulate', payload)
    return data
  },

  /** Registra una nueva solicitud de préstamo originada desde el portal (FE-CP-07). */
  async createApplication(payload: CreateApplicationRequest): Promise<LoanApplication> {
    const { data } = await useApi().post<LoanApplication>('/client/loan-applications', payload)
    return data
  },

  /** Obtiene las solicitudes de préstamo del cliente autenticado con su estado actual (FE-CP-08). */
  async fetchMyApplications(): Promise<LoanApplication[]> {
    const { data } = await useApi().get<LoanApplication[]>('/client/loan-applications')
    return data
  },

  /** Consulta el detalle de una solicitud específica. */
  async fetchApplicationById(id: string): Promise<LoanApplication> {
    const { data } = await useApi().get<LoanApplication>(
      `/loan-applications/${encodeURIComponent(id)}`,
    )
    return data
  },
}
