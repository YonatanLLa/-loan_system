import type { LoanProduct } from '@prestamos/api-client'
import { useApi } from '@/app/api'

export const productsService = {
  async getProducts(status?: string): Promise<LoanProduct[]> {
    const params: Record<string, string> = {}
    if (status) params.status = status
    const { data } = await useApi().get<LoanProduct[]>('/loan-products', { params })
    return data
  },

  async createProduct(payload: Omit<LoanProduct, 'id'>): Promise<LoanProduct> {
    const { data } = await useApi().post<LoanProduct>('/loan-products', payload)
    return data
  },

  async updateProduct(id: string, payload: Omit<LoanProduct, 'id'>): Promise<LoanProduct> {
    const { data } = await useApi().put<LoanProduct>(`/loan-products/${id}`, payload)
    return data
  },
}
