import type {
  ClientPaymentRequest,
  Loan,
  PartialPrepaymentSimulation,
  Payment,
  PaymentIntent,
  PrepaymentQuote,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

export interface PaymentWithLoan extends Payment {
  loan: Loan
}

/**
 * Servicio de Pagos y Cancelación Anticipada para el Portal del Cliente (FE-CP-12 a FE-CP-17).
 * Encapsula la comunicación con la API de pagos y prepagos.
 */
export const paymentsService = {
  /**
   * Inicia una intención de pago multicanal (QR Yape/Plin, Tarjeta, Voucher de transferencia) (FE-CP-12).
   */
  async createPaymentIntent(payload: ClientPaymentRequest): Promise<PaymentIntent> {
    const { data } = await useApi().post<PaymentIntent>('/client/payments', payload)
    return data
  },

  /**
   * Consulta el estado en tiempo real de un pago para webhook / polling (FE-CP-14).
   */
  async getPayment(paymentId: string): Promise<PaymentWithLoan> {
    const { data } = await useApi().get<PaymentWithLoan>(
      `/client/payments/${encodeURIComponent(paymentId)}`,
    )
    return data
  },

  /**
   * Obtiene el historial completo de pagos realizados y en validación del cliente (FE-CP-13 y FE-CP-15).
   */
  async getPaymentHistory(): Promise<Payment[]> {
    const { data } = await useApi().get<Payment[]>('/client/payments')
    return data
  },

  /**
   * Descarga la constancia oficial de pago en formato PDF (FE-CP-14).
   */
  async downloadReceiptPdf(paymentId: string, receiptNumber?: string): Promise<void> {
    const response = await useApi().get<Blob>(
      `/client/payments/${encodeURIComponent(paymentId)}/receipt.pdf`,
      { responseType: 'blob' },
    )
    const blob = new Blob([response.data], { type: 'application/pdf' })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `Recibo-${receiptNumber ?? paymentId}.pdf`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
  },

  /**
   * Cotiza la liquidación por cancelación anticipada total (prepago) con fecha proyectada (FE-CP-16 / US-30).
   */
  async quotePrepayment(loanId: string, asOfDate?: string): Promise<PrepaymentQuote> {
    const { data } = await useApi().post<PrepaymentQuote>(
      `/client/loans/${encodeURIComponent(loanId)}/quote-prepayment`,
      { asOfDate },
    )
    return data
  },

  /**
   * Simula el abono extraordinario a capital (prepago parcial) para reducir plazo o reducir cuota (FE-CP-17).
   */
  async simulatePartialPrepayment(
    loanId: string,
    amount: string,
    option: 'REDUCE_TERM' | 'REDUCE_FEE',
  ): Promise<PartialPrepaymentSimulation> {
    const { data } = await useApi().post<PartialPrepaymentSimulation>(
      `/client/loans/${encodeURIComponent(loanId)}/simulate-partial-prepayment`,
      { amount, option },
    )
    return data
  },
}
