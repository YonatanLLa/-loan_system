import { ref, computed, onMounted, getCurrentInstance } from 'vue'
import type {
  ChangePrimaryBankAccountRequest,
  Customer,
  CustomerBankAccount,
  RefundRequest,
} from '@prestamos/api-client'

import { profileService, type ChangeAccountResponse } from '../services/profile.service'

export function useProfile() {
  const profile = ref<Customer | null>(null)
  const bankAccounts = ref<CustomerBankAccount[]>([])
  const refunds = ref<RefundRequest[]>([])

  const isLoading = ref(false)
  const isUpdatingAccount = ref(false)
  const isRequestingRefund = ref(false)

  const errorMessage = ref<string | null>(null)
  const successMessage = ref<string | null>(null)

  const primaryAccount = computed(() =>
    bankAccounts.value.find((a) => a.isPrimary) || bankAccounts.value[0] || null,
  )

  const coolingOffAccount = computed(() => {
    return bankAccounts.value.find((a) => {
      if (!a.coolingOffUntil) return false
      return new Date(a.coolingOffUntil).getTime() > Date.now()
    })
  })

  const loadData = async () => {
    isLoading.value = true
    errorMessage.value = null
    try {
      const [profData, accountsData, refundsData] = await Promise.all([
        profileService.fetchProfile(),
        profileService.fetchBankAccounts(),
        profileService.fetchRefunds(),
      ])
      profile.value = profData
      bankAccounts.value = accountsData
      refunds.value = refundsData
    } catch (err: any) {
      errorMessage.value =
        err?.response?.data?.detail ||
        err?.message ||
        'No pudimos cargar los datos de tu perfil.'
    } finally {
      isLoading.value = false
    }
  }

  const changePrimaryAccount = async (
    payload: ChangePrimaryBankAccountRequest,
  ): Promise<ChangeAccountResponse | null> => {
    isUpdatingAccount.value = true
    errorMessage.value = null
    successMessage.value = null

    try {
      const res = await profileService.changePrimaryAccount(payload)
      successMessage.value = res.message
      await loadData()
      return res
    } catch (err: any) {
      errorMessage.value =
        err?.response?.data?.detail ||
        err?.message ||
        'No se pudo actualizar la cuenta de desembolso.'
      return null
    } finally {
      isUpdatingAccount.value = false
    }
  }

  const submitRefundRequest = async (
    amount: string,
    bankAccountId: string,
  ): Promise<RefundRequest | null> => {
    isRequestingRefund.value = true
    errorMessage.value = null
    successMessage.value = null

    try {
      const res = await profileService.requestRefund(amount, bankAccountId)
      successMessage.value = `Solicitud de devolución por S/ ${res.amount} registrada con éxito.`
      await loadData()
      return res
    } catch (err: any) {
      errorMessage.value =
        err?.response?.data?.detail ||
        err?.message ||
        'No se pudo registrar la solicitud de devolución.'
      return null
    } finally {
      isRequestingRefund.value = false
    }
  }

  if (getCurrentInstance()) {
    onMounted(() => {
      loadData()
    })
  }

  return {
    profile,
    bankAccounts,
    refunds,
    primaryAccount,
    coolingOffAccount,
    isLoading,
    isUpdatingAccount,
    isRequestingRefund,
    errorMessage,
    successMessage,
    loadData,
    changePrimaryAccount,
    submitRefundRequest,
  }
}
