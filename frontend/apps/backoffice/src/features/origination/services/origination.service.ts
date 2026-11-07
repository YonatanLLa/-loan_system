import type {
  Customer,
  Customer360,
  DecisionRequest,
  LoanApplication,
  LoanProduct,
  Page,
  PaymentFrequency,
  Simulation,
} from '@prestamos/api-client'
import { useApi } from '@/app/api'

export interface ApplicationsQuery {
  page: number
  pageSize: number
  q?: string
  status?: string
}

export const originationService = {
  async getApplications(query: ApplicationsQuery): Promise<Page<LoanApplication>> {
    const params: Record<string, string | number> = {
      page: query.page,
      pageSize: query.pageSize,
    }
    if (query.q?.trim()) params.q = query.q.trim()
    if (query.status) params.status = query.status

    const { data } = await useApi().get<Page<LoanApplication>>('/loan-applications', { params })
    return data
  },

  async getApplication(id: string): Promise<LoanApplication> {
    const { data } = await useApi().get<LoanApplication>(`/loan-applications/${id}`)
    return data
  },

  async simulateExistingApplication(id: string): Promise<Simulation> {
    const { data } = await useApi().post<Simulation>(`/loan-applications/${id}/simulate`, {})
    return data
  },

  async simulateNewApplication(payload: {
    productId: string
    amount: string
    term: number
    frequency: PaymentFrequency
  }): Promise<Simulation> {
    const { data } = await useApi().post<Simulation>('/loan-applications/simulate', payload)
    return data
  },

  async createApplication(payload: {
    customerId: string
    productId: string
    amount: string
    term: number
    frequency: PaymentFrequency
    destination: string
  }): Promise<LoanApplication> {
    const { data } = await useApi().post<LoanApplication>('/loan-applications', payload)
    return data
  },

  async startReview(id: string): Promise<LoanApplication> {
    const { data } = await useApi().post<LoanApplication>(`/loan-applications/${id}/start-review`)
    return data
  },

  async submitDecision(id: string, payload: DecisionRequest): Promise<LoanApplication> {
    const { data } = await useApi().post<LoanApplication>(
      `/loan-applications/${id}/decision`,
      payload,
    )
    return data
  },

  async getCustomer360(customerId: string): Promise<Customer360> {
    const { data } = await useApi().get<Customer360>(`/customers/${customerId}`)
    return data
  },

  async getActiveProducts(): Promise<LoanProduct[]> {
    const { data } = await useApi().get<LoanProduct[]>('/loan-products?status=ACTIVO')
    return data
  },

  async getCustomersDirectory(): Promise<Customer[]> {
    const { data } = await useApi().get<{ items: Customer[] }>('/customers?pageSize=50')
    return data.items
  },
}
