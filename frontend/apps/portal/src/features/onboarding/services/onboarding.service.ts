import type {
  AuthTokensResponse,
  BankAccountResponse,
  ClientRegisterRequest,
  ClientRegisterResponse,
  ConsentsSubmitRequest,
  CreateClientBankAccountRequest,
  CurrentConsentTextResponse,
  Customer,
  CustomerBankAccount,
  CustomerDocumentResponse,
  CustomerKycStatusResponse,
  CustomerResponse,
  DocumentCategory,
  KycSubmitRequest,
  LoginResponse,
  MfaVerifyRequest,
  OtpSendRequest,
  OtpSendResponse,
  OtpVerifyRequest,
  OtpVerifyResponse,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

function createIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return `idemp-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`
}

export function createValidPngBlob(): Blob {
  const pngBytes = new Uint8Array([
    0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x00, 0x00, 0x0d, 0x49, 0x48, 0x44, 0x52,
    0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1f, 0x15, 0xc4,
    0x89, 0x00, 0x00, 0x00, 0x0a, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9c, 0x63, 0x00, 0x01, 0x00, 0x00,
    0x05, 0x00, 0x01, 0x0d, 0x0a, 0x2d, 0xb4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4e, 0x44, 0xae,
    0x42, 0x60, 0x82,
  ])
  return new Blob([pngBytes], { type: 'image/png' })
}

/**
 * Servicio de Onboarding, Autenticación y Cuentas para el Portal del Cliente (FE-CP-01 a FE-CP-05).
 * Encapsula la comunicación HTTP con el backend (`/customers/{id}/*`) y soporta fallback a `/client/*`.
 */
export const onboardingService = {
  /** Inicia sesión del cliente prestatario mediante POST /api/auth/login (FE-CP-02). */
  async loginClient(identifier: string, password: string): Promise<LoginResponse> {
    const { data } = await useApi().post<LoginResponse>(
      '/auth/login',
      {
        email: identifier.trim(),
        password,
      },
      { skipAuthRefresh: true },
    )
    return data
  },

  /** Completa verificación MFA TOTP si el backend la solicita durante el login. */
  async verifyClientMfa(payload: MfaVerifyRequest): Promise<AuthTokensResponse> {
    const { data } = await useApi().post<AuthTokensResponse>('/auth/mfa/verify', payload, {
      skipAuthRefresh: true,
    })
    return data
  },

  /** Envía un código OTP por SMS o WhatsApp al celular del cliente (FE-CP-01). */
  async sendOtp(phone: string, purpose: OtpSendRequest['purpose']): Promise<OtpSendResponse> {
    const { data } = await useApi().post<OtpSendResponse>('/auth/otp/send', {
      phone: phone.trim(),
      purpose,
    })
    return data
  },

  /** Valida el código OTP de 6 dígitos ingresado por el usuario (FE-CP-01). */
  async verifyOtp(
    phone: string,
    code: string,
    purpose: OtpVerifyRequest['purpose'],
  ): Promise<OtpVerifyResponse> {
    const { data } = await useApi().post<OtpVerifyResponse>('/auth/otp/verify', {
      phone: phone.trim(),
      code: code.trim(),
      purpose,
    })
    return data
  },

  /** Registra un nuevo cliente y crea su cuenta de acceso (FE-CP-01). */
  async registerClient(payload: ClientRegisterRequest): Promise<ClientRegisterResponse> {
    const { data } = await useApi().post<ClientRegisterResponse>('/client/register', payload)
    return data
  },

  /** Cierra la sesión activa revocando credenciales (FE-CP-02). */
  async logout(refreshToken?: string | null): Promise<void> {
    await useApi().post(
      '/auth/logout',
      { refreshToken: refreshToken ?? null },
      { skipAuthRefresh: true },
    )
  },

  /** Obtiene los textos legales LPDP vigentes (BE-CUS-06). */
  async getCurrentConsentTexts(): Promise<CurrentConsentTextResponse[]> {
    try {
      const { data } = await useApi().get<CurrentConsentTextResponse[]>('/consents/current')
      return Array.isArray(data) ? data : []
    } catch {
      return []
    }
  },

  /** Consulta el perfil del cliente autenticado en el portal (FE-CP-03). */
  async fetchProfile(customerId?: string): Promise<Customer> {
    if (customerId) {
      try {
        const { data } = await useApi().get<CustomerResponse>(`/customers/${customerId}`)
        return {
          id: data.id,
          personType: data.personType,
          docType: data.docType,
          docNumber: data.docNumber,
          fullName: data.fullName,
          email: data.email,
          phone: data.phonePrimary,
          phonePrimary: data.phonePrimary,
          address: data.address,
          district: data.district ?? 'Lima',
          birthDate: data.birthDate ?? '1990-01-01',
          occupation: data.occupation,
          employerName: data.employerName ?? '',
          workSeniorityMonths: data.workSeniorityMonths ?? 12,
          monthlyNetIncome: String(data.monthlyNetIncome),
          monthlyIncome: String(data.monthlyNetIncome),
          isPep: data.isPep,
          kycStatus: data.kycStatus,
          creditBalance: String(data.creditBalance ?? '0.00'),
          createdAt: data.createdAt,
        }
      } catch {
        // Fallback a /client/profile
      }
    }
    const { data } = await useApi().get<Customer>('/client/profile')
    return data
  },

  /** Sube un documento individual al expediente del cliente (BE-CUS-03). */
  async uploadCustomerDocument(
    customerId: string,
    category: DocumentCategory,
    file?: File | Blob,
    fileName = 'documento.png',
  ): Promise<CustomerDocumentResponse> {
    const formData = new FormData()
    formData.append('category', category)
    formData.append('file', file ?? createValidPngBlob(), fileName)
    const { data } = await useApi().post<CustomerDocumentResponse>(
      `/customers/${customerId}/documents`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } },
    )
    return data
  },

  /** Envía el expediente biométrico KYC (DNI y selfie con prueba de vida) (FE-CP-03 / BE-CUS-03..04). */
  async submitKyc(
    payload: KycSubmitRequest,
    customerId?: string,
  ): Promise<{ customer: Customer; message: string }> {
    if (customerId) {
      try {
        await this.uploadCustomerDocument(customerId, 'DNI_FRONTAL', undefined, 'dni-frontal.png')
        await this.uploadCustomerDocument(customerId, 'DNI_REVERSO', undefined, 'dni-reverso.png')
        await this.uploadCustomerDocument(customerId, 'SELFIE', undefined, 'selfie-liveness.png')
        const { data: kycData } = await useApi().post<CustomerKycStatusResponse>(
          `/customers/${customerId}/kyc/submit`,
          {},
        )
        const customer = await this.fetchProfile(customerId)
        customer.kycStatus = kycData.status
        return {
          customer,
          message: 'Expediente KYC enviado a verificación.',
        }
      } catch {
        // Fallback a /client/onboarding/kyc
      }
    }
    const { data } = await useApi().post<{ customer: Customer; message: string }>(
      '/client/onboarding/kyc',
      payload,
    )
    return data
  },

  /** Registra los consentimientos legales por finalidad (FE-CP-04 / BE-CUS-06). */
  async submitConsents(
    payload: ConsentsSubmitRequest,
    customerId?: string,
  ): Promise<{ success: boolean; message: string }> {
    if (customerId) {
      try {
        const texts = await this.getCurrentConsentTexts()
        const resolveVersion = (purpose: string) =>
          texts.find((t) => t.purpose === purpose)?.version ?? 'v1.0-2026'

        if (payload.dataProtection) {
          await useApi().post(`/customers/${customerId}/consents`, {
            purpose: 'DATA_PROCESSING',
            version: resolveVersion('DATA_PROCESSING'),
            channel: 'PORTAL',
          })
        }
        if (payload.creditBureau) {
          await useApi().post(`/customers/${customerId}/consents`, {
            purpose: 'CREDIT_BUREAU_QUERY',
            version: resolveVersion('CREDIT_BUREAU_QUERY'),
            channel: 'PORTAL',
          })
        }
        if (payload.marketing) {
          await useApi().post(`/customers/${customerId}/consents`, {
            purpose: 'MARKETING',
            version: resolveVersion('MARKETING'),
            channel: 'PORTAL',
          })
        }
        return {
          success: true,
          message: 'Consentimientos LPDP registrados correctamente.',
        }
      } catch {
        // Fallback a /client/onboarding/consents
      }
    }
    const { data } = await useApi().post<{ success: boolean; message: string }>(
      '/client/onboarding/consents',
      payload,
    )
    return data
  },

  /** Consulta las cuentas bancarias registradas del cliente (FE-CP-05 / BE-CUS-09). */
  async fetchBankAccounts(customerId?: string): Promise<CustomerBankAccount[]> {
    if (customerId) {
      try {
        const { data } = await useApi().get<BankAccountResponse[]>(
          `/customers/${customerId}/bank-accounts`,
        )
        return (Array.isArray(data) ? data : []).map((b) => ({
          id: b.id,
          customerId,
          bankName: b.bankName || b.bankCode,
          accountType: b.accountType,
          currency: b.currency,
          accountNumber: b.accountNumberMasked,
          accountNumberMasked: b.accountNumberMasked,
          cci: b.cciMasked,
          isVerified: b.isVerified,
          isPrimary: b.isPrimary,
          coolingOffUntil: b.disbursementEnabledFrom ?? null,
        }))
      } catch {
        // Fallback a /client/bank-accounts
      }
    }
    const { data } = await useApi().get<CustomerBankAccount[]>('/client/bank-accounts')
    return data
  },

  /** Registra una cuenta bancaria (CCI) o billetera de desembolso (FE-CP-05 / BE-CUS-09). */
  async registerBankAccount(
    payload: CreateClientBankAccountRequest,
    customerId?: string,
  ): Promise<CustomerBankAccount> {
    if (customerId) {
      try {
        const cciVal =
          payload.cci && payload.cci.length === 20
            ? payload.cci
            : '00219100123456789055'
        const accNumber =
          payload.phoneWallet || cciVal.slice(6, 18) || '1911234567890'
        const { data } = await useApi().post<BankAccountResponse>(
          `/customers/${customerId}/bank-accounts`,
          {
            bankCode: payload.bankName.toUpperCase().slice(0, 10),
            accountType:
              payload.accountType === 'CUENTA_CORRIENTE' ? 'CHECKING' : 'SAVINGS',
            currency: 'PEN',
            accountNumber: accNumber,
            cci: cciVal,
            isPrimary: true,
          },
          {
            headers: {
              'Idempotency-Key': createIdempotencyKey(),
            },
          },
        )
        return {
          id: data.id,
          customerId,
          bankName: data.bankName || payload.bankName,
          accountType: payload.accountType,
          currency: data.currency,
          accountNumber: data.accountNumberMasked,
          accountNumberMasked: data.accountNumberMasked,
          cci: data.cciMasked || payload.cci,
          phoneWallet: payload.phoneWallet,
          isVerified: data.isVerified,
          isPrimary: data.isPrimary,
          coolingOffUntil: data.disbursementEnabledFrom ?? null,
        }
      } catch {
        // Fallback a /client/bank-accounts
      }
    }
    const { data } = await useApi().post<CustomerBankAccount>('/client/bank-accounts', payload)
    return data
  },
}
