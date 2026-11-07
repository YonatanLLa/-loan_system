import { ref, watch } from 'vue'
import { toast } from 'vue-sonner'
import type {
  CreateCustomerRequest,
  Customer,
  Customer360,
  CustomerBankAccount,
  KycStatus,
  Page,
} from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { customersService } from '../services/customers.service'

export function useCustomers() {
  const loading = ref(false)
  const customersPage = ref<Page<Customer>>({
    items: [],
    total: 0,
    page: 1,
    pageSize: 10,
  })

  const searchQuery = ref('')
  const selectedKycStatus = ref('')
  const currentPage = ref(1)
  const totalPages = ref(1)

  watch(
    () => customersPage.value.total,
    (total) => {
      totalPages.value = Math.max(1, Math.ceil(total / customersPage.value.pageSize))
    },
    { immediate: true },
  )

  async function loadCustomers() {
    loading.value = true
    try {
      customersPage.value = await customersService.getCustomers({
        page: currentPage.value,
        pageSize: customersPage.value.pageSize,
        q: searchQuery.value,
        kycStatus: selectedKycStatus.value,
      })
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al cargar clientes', { description: apiError.detail })
    } finally {
      loading.value = false
    }
  }

  function onSearch() {
    currentPage.value = 1
    void loadCustomers()
  }

  function resetFilters() {
    searchQuery.value = ''
    selectedKycStatus.value = ''
    currentPage.value = 1
    void loadCustomers()
  }

  function goToPage(p: number) {
    if (p < 1 || p > totalPages.value) return
    currentPage.value = p
    void loadCustomers()
  }

  async function saveCustomer(payload: CreateCustomerRequest, editId?: string): Promise<Customer> {
    if (editId) {
      return await customersService.updateCustomer(editId, payload)
    }
    return await customersService.createCustomer(payload)
  }

  async function submitKycDecision(
    customerId: string,
    payload: { status: KycStatus; notes?: string },
  ): Promise<Customer> {
    return await customersService.updateKyc(customerId, payload)
  }

  async function createBankAccount(
    customerId: string,
    payload: {
      bankName: string
      accountType: CustomerBankAccount['accountType']
      cci?: string
      phoneWallet?: string
    },
  ): Promise<CustomerBankAccount> {
    return await customersService.addBankAccount(customerId, payload)
  }

  return {
    loading,
    customersPage,
    searchQuery,
    selectedKycStatus,
    currentPage,
    totalPages,
    loadCustomers,
    onSearch,
    resetFilters,
    goToPage,
    saveCustomer,
    submitKycDecision,
    createBankAccount,
    getCustomer360: customersService.getCustomer360,
    verifyBankAccount: customersService.verifyBankAccount,
    makePrimaryBankAccount: customersService.makePrimaryBankAccount,
    applyCreditBalance: customersService.applyCreditBalance,
    refundCreditBalance: customersService.refundCreditBalance,
  }
}
