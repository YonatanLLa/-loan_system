import type {
  AuthTokensResponse,
  ClientRegisterRequest,
  ClientRegisterResponse,
  ConsentsSubmitRequest,
  CreateClientBankAccountRequest,
  Customer,
  CustomerBankAccount,
  KycSubmitRequest,
  LoginResponse,
  MfaVerifyRequest,
  OtpSendRequest,
  OtpSendResponse,
  OtpVerifyRequest,
  OtpVerifyResponse,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

/**
 * Servicio de Onboarding, Autenticación y Cuentas para el Portal del Cliente (FE-CP-01 a FE-CP-05).
 * Encapsula la comunicación HTTP con el backend / mocks.
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

  /** Consulta el perfil del cliente autenticado en el portal (FE-CP-03). */
  async fetchProfile(): Promise<Customer> {
    const { data } = await useApi().get<Customer>('/client/profile')
    return data
  },

  /** Envía el expediente biométrico KYC (DNI y selfie con prueba de vida) (FE-CP-03). */
  async submitKyc(payload: KycSubmitRequest): Promise<{ customer: Customer; message: string }> {
    const { data } = await useApi().post<{ customer: Customer; message: string }>(
      '/client/onboarding/kyc',
      payload,
    )
    return data
  },

  /** Registra los consentimientos legales por finalidad (FE-CP-04). */
  async submitConsents(
    payload: ConsentsSubmitRequest,
  ): Promise<{ success: boolean; message: string }> {
    const { data } = await useApi().post<{ success: boolean; message: string }>(
      '/client/onboarding/consents',
      payload,
    )
    return data
  },

  /** Consulta las cuentas bancarias registradas del cliente (FE-CP-05). */
  async fetchBankAccounts(): Promise<CustomerBankAccount[]> {
    const { data } = await useApi().get<CustomerBankAccount[]>('/client/bank-accounts')
    return data
  },

  /** Registra una cuenta bancaria (CCI) o billetera de desembolso (FE-CP-05). */
  async registerBankAccount(
    payload: CreateClientBankAccountRequest,
  ): Promise<CustomerBankAccount> {
    const { data } = await useApi().post<CustomerBankAccount>('/client/bank-accounts', payload)
    return data
  },
}
