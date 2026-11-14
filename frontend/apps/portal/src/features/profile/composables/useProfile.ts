import { ref, computed, onMounted, getCurrentInstance } from 'vue'
import {
  toApiError,
  type ChangePrimaryBankAccountRequest,
  type Customer,
  type CustomerBankAccount,
  type OtpChannel,
  type OtpIssued,
  type RefundRequest,
} from '@prestamos/api-client'

import { profileService, type ChangeAccountResponse } from '../services/profile.service'
import { useSessionStore } from '@/app/stores/session'

export function useProfile() {
  const sessionStore = useSessionStore()

  const profile = ref<Customer | null>(null)
  const bankAccounts = ref<CustomerBankAccount[]>([])
  const refunds = ref<RefundRequest[]>([])

  const isLoading = ref(false)
  const isUpdatingAccount = ref(false)
  const isRequestingRefund = ref(false)

  const errorMessage = ref<string | null>(null)
  const successMessage = ref<string | null>(null)

  const contactOtpIssued = ref<OtpIssued | null>(null)
  const isVerifyingContact = ref(false)

  const primaryAccount = computed(
    () => bankAccounts.value.find((a) => a.isPrimary) || bankAccounts.value[0] || null,
  )

  const coolingOffAccount = computed(() => {
    return bankAccounts.value.find((a) => {
      const coolingDate = a.coolingOffUntil ?? a.coolingPeriodEndsAt
      if (!coolingDate) return false
      return new Date(coolingDate).getTime() > Date.now()
    })
  })

  const hasAvailableCreditBalance = computed(() => {
    const amount = Number(profile.value?.creditBalance ?? '0')
    return amount > 0
  })

  const hasMarketingConsent = computed(() => {
    const consents = profile.value?.consents ?? []
    return consents.some((c) => c.purpose === 'MARKETING' && c.isActive)
  })

  const loadData = async () => {
    isLoading.value = true
    errorMessage.value = null
    const customerId = sessionStore.user?.customerId
    try {
      const [profData, accountsData, refundsData] = await Promise.all([
        profileService.fetchProfile(customerId),
        profileService.fetchBankAccounts(customerId),
        profileService.fetchRefunds(),
      ])
      profile.value = profData
      bankAccounts.value = accountsData
      refunds.value = refundsData
    } catch (err: unknown) {
      const apiErr = toApiError(err)
      errorMessage.value =
        apiErr.detail || 'No pudimos cargar los datos de tu perfil.'
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
    const customerId = sessionStore.user?.customerId

    try {
      const res = await profileService.changePrimaryAccount(payload, customerId)
      successMessage.value = res.message
      await loadData()
      return res
    } catch (err: unknown) {
      const apiErr = toApiError(err)
      errorMessage.value =
        apiErr.detail || 'No se pudo actualizar la cuenta de desembolso.'
      return null
    } finally {
      isUpdatingAccount.value = false
    }
  }

  const submitRefundRequest = async (
    amountOrPayload: string | { amount: string; bankAccountId: string },
    bankAccountId?: string,
  ): Promise<RefundRequest | null> => {
    isRequestingRefund.value = true
    errorMessage.value = null
    successMessage.value = null

    const resolvedAmount =
      typeof amountOrPayload === 'string' ? amountOrPayload : amountOrPayload.amount
    const resolvedAccountId =
      typeof amountOrPayload === 'string'
        ? (bankAccountId ?? '')
        : amountOrPayload.bankAccountId

    try {
      const res = await profileService.requestRefund(resolvedAmount, resolvedAccountId)
      successMessage.value = `Solicitud de devolución por S/ ${res.amount} registrada con éxito.`
      await loadData()
      return res
    } catch (err: unknown) {
      const apiErr = toApiError(err)
      errorMessage.value =
        apiErr.detail || 'No se pudo registrar la solicitud de devolución.'
      return null
    } finally {
      isRequestingRefund.value = false
    }
  }

  const startContactOtp = async (channel: OtpChannel): Promise<boolean> => {
    const customerId = sessionStore.user?.customerId ?? profile.value?.id
    if (!customerId) return false
    errorMessage.value = null
    try {
      contactOtpIssued.value = await profileService.startContactVerification(
        customerId,
        channel,
      )
      return true
    } catch (err: unknown) {
      const apiErr = toApiError(err)
      errorMessage.value = apiErr.detail || 'No se pudo enviar el código OTP.'
      return false
    }
  }

  const confirmContactOtp = async (code: string): Promise<boolean> => {
    const customerId = sessionStore.user?.customerId ?? profile.value?.id
    if (!customerId || !contactOtpIssued.value) return false
    isVerifyingContact.value = true
    errorMessage.value = null
    try {
      await profileService.confirmContactVerification(
        customerId,
        contactOtpIssued.value.otpRequestId,
        code,
      )
      contactOtpIssued.value = null
      successMessage.value = 'Canal de contacto verificado exitosamente.'
      profile.value = await profileService.fetchProfile(customerId)
      return true
    } catch (err: unknown) {
      const apiErr = toApiError(err)
      errorMessage.value = apiErr.detail || 'Código OTP inválido o expirado.'
      return false
    } finally {
      isVerifyingContact.value = false
    }
  }

  const toggleMarketing = async (): Promise<boolean> => {
    const customerId = sessionStore.user?.customerId ?? profile.value?.id
    if (!customerId) return false
    errorMessage.value = null
    try {
      await profileService.toggleMarketingConsent(customerId, !hasMarketingConsent.value)
      profile.value = await profileService.fetchProfile(customerId)
      successMessage.value = hasMarketingConsent.value
        ? 'Consentimiento de comunicaciones comerciales activado.'
        : 'Consentimiento de comunicaciones comerciales revocado.'
      return true
    } catch (err: unknown) {
      const apiErr = toApiError(err)
      errorMessage.value = apiErr.detail || 'No se pudo actualizar la preferencia LPDP.'
      return false
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
    refundHistory: refunds,
    primaryAccount,
    coolingOffAccount,
    hasAvailableCreditBalance,
    hasMarketingConsent,
    contactOtpIssued,
    isVerifyingContact,
    isLoading,
    isUpdatingAccount,
    isRequestingRefund,
    errorMessage,
    successMessage,
    loadData,
    loadAll: loadData,
    changePrimaryAccount,
    submitRefundRequest,
    requestRefund: submitRefundRequest,
    startContactOtp,
    confirmContactOtp,
    toggleMarketing,
  }
}
