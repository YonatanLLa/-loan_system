import type {
  CompanyBankAccount,
  Loan,
  Page,
  Payment,
  PaymentMethodType,
} from '@prestamos/api-client'
import { useApi } from '@/app/api'

export interface PaymentsQuery {
  page: number
  pageSize: number
  q?: string
  status?: string
}

export const paymentsService = {
  async getPayments(query: PaymentsQuery): Promise<Page<Payment>> {
    const params: Record<string, string | number> = {
      page: query.page,
      pageSize: query.pageSize,
    }
    if (query.q?.trim()) params.q = query.q.trim()
    if (query.status) params.status = query.status

    const { data } = await useApi().get<Page<Payment>>('/payments', { params })
    return data
  },

  async confirmPayment(paymentId: string): Promise<Payment> {
    const { data } = await useApi().post<Payment>(`/payments/${paymentId}/confirm`)
    return data
  },

  async reversePayment(paymentId: string, reason: string): Promise<Payment> {
    const { data } = await useApi().post<Payment>(`/payments/${paymentId}/reverse`, { reason })
    return data
  },

  async registerPayment(
    loanId: string,
    payload: {
      amount: string
      method: PaymentMethodType
      companyBankAccountId?: string
      externalReference: string
      paidAt: string
    },
    idempotencyKey: string,
  ): Promise<Payment> {
    const { data } = await useApi().post<Payment>(`/loans/${loanId}/payments`, payload, {
      headers: { 'Idempotency-Key': idempotencyKey },
    })
    return data
  },

  async getActiveLoans(): Promise<Loan[]> {
    const { data } = await useApi().get<{ items: Loan[] }>('/loans?status=VIGENTE,EN_MORA')
    return data.items
  },

  async getCollectionBankAccounts(): Promise<CompanyBankAccount[]> {
    const { data } = await useApi().get<CompanyBankAccount[]>('/treasury/bank-accounts')
    return data.filter((a) => a.isActive && a.isCollection)
  },
}
