import type {
  LoanContractDocument,
  OtpSendResponse,
  SignContractRequest,
  SignContractResponse,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

/**
 * Servicio de Contrato Electrónico y Firma Digital (FE-CP-09).
 * Encapsula la obtención del contrato, envío de OTP de firma y descarga del documento legal.
 */
export const contractService = {
  /** Obtiene la minuta legal del contrato, hoja resumen y cronograma oficial. */
  async fetchContract(applicationId?: string): Promise<LoanContractDocument> {
    const { data } = await useApi().get<LoanContractDocument>('/client/contract', {
      params: applicationId ? { applicationId } : undefined,
    })
    return data
  },

  /** Solicita el código OTP de seguridad para la firma digital del contrato. */
  async requestSigningOtp(phone: string): Promise<OtpSendResponse> {
    const { data } = await useApi().post<OtpSendResponse>('/auth/otp/send', {
      phone: phone.trim(),
      purpose: 'CONTRACT_SIGN',
    })
    return data
  },

  /** Registra la firma electrónica con validación de código OTP (FE-CP-09). */
  async signContract(payload: SignContractRequest): Promise<SignContractResponse> {
    const { data } = await useApi().post<SignContractResponse>(
      '/client/contract/sign',
      payload,
    )
    return data
  },

  /** Descarga el contrato y pagaré firmado en formato PDF. */
  async downloadContractPdf(filename = 'contrato_prestamo_firmado'): Promise<void> {
    const response = await useApi().get('/client/contract/document.pdf', {
      responseType: 'blob',
    })

    const blob = new Blob([response.data], { type: 'application/pdf' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', `${filename}.pdf`)
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
  },
}
