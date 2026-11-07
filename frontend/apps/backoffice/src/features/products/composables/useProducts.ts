import { ref } from 'vue'
import { toast } from 'vue-sonner'
import type { LoanProduct } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { productsService } from '../services/products.service'

export function useProducts() {
  const loading = ref(false)
  const products = ref<LoanProduct[]>([])
  const filterStatus = ref('')

  async function loadProducts() {
    loading.value = true
    try {
      products.value = await productsService.getProducts(filterStatus.value || undefined)
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al cargar productos', { description: apiError.detail })
    } finally {
      loading.value = false
    }
  }

  async function saveProduct(
    payload: Omit<LoanProduct, 'id'>,
    editId?: string,
  ): Promise<LoanProduct> {
    if (editId) {
      return await productsService.updateProduct(editId, payload)
    }
    return await productsService.createProduct(payload)
  }

  return {
    loading,
    products,
    filterStatus,
    loadProducts,
    saveProduct,
  }
}
