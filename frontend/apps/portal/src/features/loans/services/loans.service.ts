import type {
  AccountStatement,
  Installment,
  Loan,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

export interface LoanScheduleResponse {
  loanId: string
  version: number
  installments: Installment[]
}

/**
 * Servicio de Préstamos y Cronograma para el Portal del Cliente (FE-CP-10 y FE-CP-11).
 * Encapsula la comunicación con la API de clientes / mocks.
 */
export const loansService = {
  /** Obtiene la lista de préstamos pertenecientes al cliente autenticado (FE-CP-10). */
  async fetchMyLoans(): Promise<Loan[]> {
    const { data } = await useApi().get<Loan[]>('/client/loans')
    return data
  },

  /** Obtiene el detalle financiero de un préstamo específico. */
  async fetchLoanDetail(loanId: string): Promise<Loan> {
    const { data } = await useApi().get<Loan>(`/client/loans/${encodeURIComponent(loanId)}`)
    return data
  },

  /** Obtiene el cronograma oficial de cuotas de un crédito (FE-CP-11). */
  async fetchLoanSchedule(loanId: string): Promise<LoanScheduleResponse> {
    const { data } = await useApi().get<LoanScheduleResponse>(
      `/client/loans/${encodeURIComponent(loanId)}/schedule`,
    )
    return data
  },

  /** Obtiene el estado de cuenta y movimientos históricos (FE-CP-15). */
  async fetchLoanStatement(loanId: string): Promise<AccountStatement> {
    const { data } = await useApi().get<AccountStatement>(
      `/client/loans/${encodeURIComponent(loanId)}/statement`,
    )
    return data
  },
}
