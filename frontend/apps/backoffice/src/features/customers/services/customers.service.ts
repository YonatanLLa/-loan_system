import type {
  CreateCustomerRequest,
  Customer,
  Customer360,
  CustomerBankAccount,
  KycStatus,
  Page,
  Payment,
  RefundRequest,
} from '@prestamos/api-client'
import { useApi } from '@/app/api'

export interface CustomersQuery {
  page: number
  pageSize: number
  q?: string
  kycStatus?: string
}

export const customersService = {
  async getCustomers(query: CustomersQuery): Promise<Page<Customer>> {
    const params: Record<string, string | number> = {
      page: query.page,
      pageSize: query.pageSize,
    }
    if (query.q?.trim()) params.q = query.q.trim()
    if (query.kycStatus) params.kycStatus = query.kycStatus

    const { data } = await useApi().get<Page<Customer>>('/customers', { params })
    return data
  },

  async getCustomer360(id: string): Promise<Customer360> {
    const { data } = await useApi().get<Customer360>(`/customers/${id}`)
    return data
  },

  async createCustomer(payload: CreateCustomerRequest): Promise<Customer> {
    const { data } = await useApi().post<Customer>('/customers', payload)
    return data
  },

  async updateCustomer(id: string, payload: CreateCustomerRequest): Promise<Customer> {
    const { data } = await useApi().put<Customer>(`/customers/${id}`, payload)
    return data
  },

  async updateKyc(id: string, payload: { status: KycStatus; notes?: string }): Promise<Customer> {
    const { data } = await useApi().patch<Customer>(`/customers/${id}/kyc`, payload)
    return data
  },

  async addBankAccount(
    customerId: string,
    payload: {
      bankName: string
      accountType: CustomerBankAccount['accountType']
      cci?: string
      phoneWallet?: string
    },
  ): Promise<CustomerBankAccount> {
    const { data } = await useApi().post<CustomerBankAccount>(
      `/customers/${customerId}/bank-accounts`,
      payload,
    )
    return data
  },

  async verifyBankAccount(accountId: string): Promise<CustomerBankAccount> {
    const { data } = await useApi().post<CustomerBankAccount>(
      `/customer-bank-accounts/${accountId}/verify`,
    )
    return data
  },

  async makePrimaryBankAccount(accountId: string): Promise<CustomerBankAccount> {
    const { data } = await useApi().post<CustomerBankAccount>(
      `/customer-bank-accounts/${accountId}/make-primary`,
    )
    return data
  },

  async applyCreditBalance(
    customerId: string,
    payload: { loanId: string; amount: string },
  ): Promise<{ payment: Payment; customer: Customer360 }> {
    const { data } = await useApi().post<{ payment: Payment; customer: Customer360 }>(
      `/customers/${customerId}/credit-balance/apply`,
      payload,
    )
    return data
  },

  async refundCreditBalance(
    customerId: string,
    payload: { bankAccountId: string; amount: string },
  ): Promise<{ refund: RefundRequest; customer: Customer360 }> {
    const { data } = await useApi().post<{ refund: RefundRequest; customer: Customer360 }>(
      `/customers/${customerId}/credit-balance/refund`,
      payload,
    )
    return data
  },
}
