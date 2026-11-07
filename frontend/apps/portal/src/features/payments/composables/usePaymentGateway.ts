import { computed, getCurrentInstance, onUnmounted, ref } from 'vue'
import {
  toApiError,
  type PaymentIntent,
  type PaymentMethodType,
} from '@prestamos/api-client'

import { paymentsService, type PaymentWithLoan } from '../services/payments.service'

export type PaymentFlowStep =
  | 'SELECT_METHOD'
  | 'AWAITING_PAYMENT'
  | 'CONFIRMED'
  | 'IN_VALIDATION'

export interface PaymentGatewayOptions {
  loanId: string
  defaultAmount?: string
  installmentNumber?: number
  concept?: string
  onSuccess?: (payment: PaymentWithLoan) => void
}

export function usePaymentGateway(options?: Partial<PaymentGatewayOptions>) {
  const loanId = ref(options?.loanId ?? '')
  const amount = ref(options?.defaultAmount ?? '')
  const installmentNumber = ref(options?.installmentNumber)
  const concept = ref(options?.concept ?? 'Pago de cuota')

  const selectedMethod = ref<PaymentMethodType>('YAPE')
  const voucherFileName = ref<string>('')
  const voucherFile = ref<File | null>(null)

  const step = ref<PaymentFlowStep>('SELECT_METHOD')
  const isSubmitting = ref(false)
  const isPolling = ref(false)
  const errorMessage = ref<string | null>(null)

  const activeIntent = ref<PaymentIntent | null>(null)
  const confirmedPayment = ref<PaymentWithLoan | null>(null)

  let pollTimer: ReturnType<typeof setInterval> | null = null

  const isQrMethod = computed(
    () => selectedMethod.value === 'YAPE' || selectedMethod.value === 'PLIN',
  )

  const isCardMethod = computed(
    () =>
      selectedMethod.value === 'TARJETA_DEBITO' ||
      selectedMethod.value === 'TARJETA_CREDITO' ||
      selectedMethod.value === 'PASARELA_PAGO',
  )

  const isVoucherMethod = computed(
    () => selectedMethod.value === 'TRANSFERENCIA_BANCARIA',
  )

  const handleVoucherSelected = (file: File) => {
    voucherFile.value = file
    voucherFileName.value = file.name
    errorMessage.value = null
  }

  const stopPolling = () => {
    if (pollTimer) {
      clearInterval(pollTimer)
      pollTimer = null
    }
    isPolling.value = false
  }

  const startPolling = (paymentId: string) => {
    stopPolling()
    isPolling.value = true
    let attempts = 0
    const MAX_ATTEMPTS = 40 // Polling por hasta 60 segundos

    pollTimer = setInterval(async () => {
      attempts += 1
      try {
        const payment = await paymentsService.getPayment(paymentId)
        if (payment.status === 'APPLIED') {
          stopPolling()
          confirmedPayment.value = payment
          step.value = 'CONFIRMED'
          if (options?.onSuccess) {
            options.onSuccess(payment)
          }
        } else if (attempts >= MAX_ATTEMPTS) {
          stopPolling()
          // Si timeout en QR pero sigue pendiente, queda en validación
          step.value = 'IN_VALIDATION'
        }
      } catch {
        // En caso de fallo transitorio en el polling, continuar reintentando
        if (attempts >= MAX_ATTEMPTS) {
          stopPolling()
        }
      }
    }, 1500)
  }

  if (getCurrentInstance()) {
    onUnmounted(() => {
      stopPolling()
    })
  }

  const initiatePayment = async (): Promise<PaymentIntent | null> => {
    if (!loanId.value) {
      errorMessage.value = 'Por favor selecciona un préstamo válido.'
      return null
    }

    const numAmount = parseFloat(amount.value)
    if (isNaN(numAmount) || numAmount <= 0) {
      errorMessage.value = 'Ingresa un monto válido mayor a cero.'
      return null
    }

    if (isVoucherMethod.value && !voucherFileName.value) {
      errorMessage.value = 'Debes adjuntar el comprobante o voucher de la transferencia.'
      return null
    }

    errorMessage.value = null
    isSubmitting.value = true

    try {
      const intent = await paymentsService.createPaymentIntent({
        loanId: loanId.value,
        amount: numAmount.toFixed(2),
        method: selectedMethod.value,
        voucherFileName: isVoucherMethod.value ? voucherFileName.value : undefined,
      })

      activeIntent.value = intent

      if (isVoucherMethod.value) {
        // Transferencias manuales van directo al estado "En validación" (FE-CP-13)
        step.value = 'IN_VALIDATION'
      } else {
        // Métodos con webhook (QR / Tarjetas) esperan confirmación instantánea (FE-CP-14)
        step.value = 'AWAITING_PAYMENT'
        startPolling(intent.paymentId)
      }

      return intent
    } catch (err: unknown) {
      const apiErr = toApiError(err)
      errorMessage.value =
        apiErr.detail || 'No pudimos iniciar el pago. Por favor intenta nuevamente.'
      return null
    } finally {
      isSubmitting.value = false
    }
  }

  const downloadReceipt = async () => {
    const paymentId = confirmedPayment.value?.id || activeIntent.value?.paymentId
    if (!paymentId) return

    try {
      await paymentsService.downloadReceiptPdf(
        paymentId,
        confirmedPayment.value?.receiptNumber,
      )
    } catch {
      errorMessage.value = 'No se pudo descargar la constancia de pago.'
    }
  }

  const reset = () => {
    stopPolling()
    step.value = 'SELECT_METHOD'
    isSubmitting.value = false
    errorMessage.value = null
    activeIntent.value = null
    confirmedPayment.value = null
    voucherFile.value = null
    voucherFileName.value = ''
  }

  return {
    loanId,
    amount,
    installmentNumber,
    concept,
    selectedMethod,
    voucherFileName,
    voucherFile,
    step,
    isSubmitting,
    isPolling,
    errorMessage,
    activeIntent,
    confirmedPayment,
    isQrMethod,
    isCardMethod,
    isVoucherMethod,
    handleVoucherSelected,
    initiatePayment,
    downloadReceipt,
    stopPolling,
    reset,
  }
}
