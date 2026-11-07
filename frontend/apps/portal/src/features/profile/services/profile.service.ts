import type {
  ChangePrimaryBankAccountRequest,
  Customer,
  CustomerBankAccount,
  RefundRequest,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

export interface ChangeAccountResponse {
  account: CustomerBankAccount
  message: string
  coolingOffUntil: string
}

/**
 * Servicio de Perfil, Cuentas Bancarias y Devolución de Saldo a Favor (FE-CP-18 y FE-CP-19).
 */
export const profileService = {
  /**
   * Obtiene los datos del perfil del cliente autenticado.
   */
  async fetchProfile(): Promise<Customer> {
    const { data } = await useApi().get<Customer>('/client/profile')
    return data
  },

  /**
   * Obtiene la lista de cuentas bancarias y billeteras del cliente (FE-CP-18).
   */
  async fetchBankAccounts(): Promise<CustomerBankAccount[]> {
    const { data } = await useApi().get<CustomerBankAccount[]>('/client/bank-accounts')
    return data
  },

  /**
   * Cambia la cuenta de desembolso principal exigiendo validación OTP y periodo de espera (FE-CP-18).
   */
  async changePrimaryAccount(
    payload: ChangePrimaryBankAccountRequest,
  ): Promise<ChangeAccountResponse> {
    const { data } = await useApi().post<ChangeAccountResponse>(
      '/client/bank-accounts/change-primary',
      payload,
    )
    return data
  },

  /**
   * Obtiene las solicitudes de devolución de saldo a favor del cliente (FE-CP-19).
   */
  async fetchRefunds(): Promise<RefundRequest[]> {
    const { data } = await useApi().get<RefundRequest[]>('/client/refunds')
    return data
  },

  /**
   * Solicita la devolución de saldo a favor a una cuenta bancaria verificada (FE-CP-19 / RF-17.3).
   */
  async requestRefund(amount: string, bankAccountId: string): Promise<RefundRequest> {
    const { data } = await useApi().post<RefundRequest>('/client/refunds', {
      amount,
      bankAccountId,
    })
    return data
  },
}
