import type {
  BankAccountResponse,
  ChangePrimaryBankAccountRequest,
  Customer,
  CustomerBankAccount,
  CustomerConsentResponse,
  CustomerOverviewResponse,
  CustomerResponse,
  OtpChannel,
  OtpIssued,
  RefundRequest,
  SetPrimaryBankAccountResult,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

export interface ChangeAccountResponse {
  account: CustomerBankAccount
  coolingOffUntil: string
  message: string
}

function createIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return `idemp-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`
}

function mapBankAccountDto(dto: BankAccountResponse, customerId: string): CustomerBankAccount {
  return {
    id: dto.id,
    customerId,
    bankName: dto.bankName || dto.bankCode,
    accountType: dto.accountType === 'CHECKING' ? 'CUENTA_CORRIENTE' : 'CUENTA_AHORRO',
    currency: dto.currency,
    accountNumber: dto.accountNumberMasked,
    accountNumberMasked: dto.accountNumberMasked,
    cci: dto.cciMasked,
    isVerified: dto.isVerified,
    isPrimary: dto.isPrimary,
    status: dto.isInCoolingPeriod ? 'PENDING_COOLING' : 'ACTIVE',
    coolingOffUntil: dto.disbursementEnabledFrom ?? null,
    coolingPeriodEndsAt: dto.disbursementEnabledFrom ?? null,
  }
}

/**
 * Servicio de Perfil, Cuentas Bancarias y Devolución de Saldo a Favor (FE-CP-18 y FE-CP-19).
 */
export const profileService = {
  /**
   * Obtiene los datos del perfil del cliente autenticado.
   */
  async fetchProfile(customerId?: string): Promise<Customer> {
    if (customerId) {
      try {
        const [customerRes, consentsRes, overviewRes] = await Promise.allSettled([
          useApi().get<CustomerResponse>(`/customers/${customerId}`),
          useApi().get<CustomerConsentResponse[]>(`/customers/${customerId}/consents`),
          useApi().get<CustomerOverviewResponse>(`/customers/${customerId}/overview`),
        ])
        if (customerRes.status === 'fulfilled' && customerRes.value.data) {
          const c = customerRes.value.data
          const consents =
            consentsRes.status === 'fulfilled' && Array.isArray(consentsRes.value.data)
              ? consentsRes.value.data
              : []
          const overview =
            overviewRes.status === 'fulfilled' ? overviewRes.value.data : undefined

          let creditBalanceStr = '0.00'
          if (overview?.availableCreditBalance != null) {
            if (typeof overview.availableCreditBalance === 'object') {
              creditBalanceStr = Number(overview.availableCreditBalance.amount ?? 0).toFixed(2)
            } else {
              creditBalanceStr = String(overview.availableCreditBalance)
            }
          }

          return {
            id: c.id,
            personType: c.personType,
            docType: c.docType,
            docNumber: c.docNumber,
            fullName: c.fullName,
            email: c.email,
            phone: c.phonePrimary,
            phonePrimary: c.phonePrimary,
            address: c.address,
            district: c.district ?? 'Lima',
            birthDate: c.birthDate ?? '1990-01-01',
            occupation: c.occupation,
            employerName: c.employerName ?? '',
            workSeniorityMonths: c.workSeniorityMonths ?? 12,
            monthlyNetIncome: String(c.monthlyNetIncome),
            monthlyIncome: String(c.monthlyNetIncome),
            isPep: c.isPep,
            kycStatus: c.kycStatus,
            creditBalance: creditBalanceStr,
            emailVerified: c.emailVerified,
            phoneVerified: c.phoneVerified,
            consents,
            createdAt: c.createdAt,
          }
        }
      } catch {
        // Fallback a /client/profile
      }
    }
    const { data } = await useApi().get<Customer>('/client/profile')
    return data
  },

  /**
   * Obtiene la lista de cuentas bancarias y billeteras del cliente (FE-CP-18).
   */
  async fetchBankAccounts(customerId?: string): Promise<CustomerBankAccount[]> {
    if (customerId) {
      try {
        const { data } = await useApi().get<BankAccountResponse[]>(
          `/customers/${customerId}/bank-accounts`,
        )
        return (Array.isArray(data) ? data : []).map((b) => mapBankAccountDto(b, customerId))
      } catch {
        // Fallback a /client/bank-accounts
      }
    }
    const { data } = await useApi().get<CustomerBankAccount[]>('/client/bank-accounts')
    return data
  },

  /**
   * Cambia la cuenta de desembolso principal exigiendo validación OTP y periodo de espera (FE-CP-18 / BE-CUS-11).
   */
  async changePrimaryAccount(
    payload: ChangePrimaryBankAccountRequest,
    customerId?: string,
  ): Promise<ChangeAccountResponse> {
    if (customerId) {
      try {
        let targetAccountId = payload.bankAccountId ?? payload.accountId

        if (!targetAccountId && payload.newAccount) {
          const cciVal =
            payload.newAccount.cci && payload.newAccount.cci.length === 20
              ? payload.newAccount.cci
              : '00219100123456789055'
          const accNumVal =
            payload.newAccount.phoneWallet || cciVal.slice(6, 18) || '1911234567890'
          const { data: createdAcc } = await useApi().post<BankAccountResponse>(
            `/customers/${customerId}/bank-accounts`,
            {
              bankCode: payload.newAccount.bankName.toUpperCase().slice(0, 10),
              accountType:
                payload.newAccount.accountType === 'CUENTA_CORRIENTE' ||
                payload.newAccount.accountType === 'CHECKING'
                  ? 'CHECKING'
                  : 'SAVINGS',
              currency: 'PEN',
              accountNumber: accNumVal,
              cci: cciVal,
              isPrimary: false,
            },
            {
              headers: {
                'Idempotency-Key': createIdempotencyKey(),
              },
            },
          )
          targetAccountId = createdAcc.id
        }

        if (targetAccountId) {
          const { data: setPrimaryRes } = await useApi().put<SetPrimaryBankAccountResult>(
            `/customers/${customerId}/bank-accounts/${targetAccountId}/primary`,
            {},
            {
              headers: {
                'Idempotency-Key': createIdempotencyKey(),
              },
            },
          )

          if (setPrimaryRes.requiresOtp && setPrimaryRes.otpChallenge) {
            const { data: confirmedAcc } = await useApi().post<BankAccountResponse>(
              `/customers/${customerId}/bank-accounts/${targetAccountId}/primary/confirm`,
              {
                otpRequestId: setPrimaryRes.otpChallenge.otpRequestId,
                code: payload.otpCode,
              },
              {
                headers: {
                  'Idempotency-Key': createIdempotencyKey(),
                },
              },
            )
            const mapped = mapBankAccountDto(confirmedAcc, customerId)
            return {
              account: mapped,
              coolingOffUntil: confirmedAcc.disbursementEnabledFrom ?? new Date().toISOString(),
              message:
                'Cuenta principal actualizada con confirmación OTP. Se ha activado el periodo de enfriamiento de 24 horas.',
            }
          }

          if (setPrimaryRes.account) {
            const mapped = mapBankAccountDto(setPrimaryRes.account, customerId)
            return {
              account: mapped,
              coolingOffUntil:
                setPrimaryRes.account.disbursementEnabledFrom ?? new Date().toISOString(),
              message: 'Cuenta principal actualizada con éxito.',
            }
          }
        }
      } catch {
        // Fallback a /client/bank-accounts/change-primary
      }
    }

    const { data } = await useApi().post<ChangeAccountResponse>(
      '/client/bank-accounts/change-primary',
      payload,
    )
    return data
  },

  /**
   * Inicia verificación OTP de Email o SMS del cliente (BE-CUS-08).
   */
  async startContactVerification(
    customerId: string,
    channel: OtpChannel,
  ): Promise<OtpIssued> {
    const { data } = await useApi().post<OtpIssued>(
      `/customers/${customerId}/contact/verify/start`,
      { channel },
    )
    return data
  },

  /**
   * Confirma verificación OTP de Email o SMS del cliente (BE-CUS-08).
   */
  async confirmContactVerification(
    customerId: string,
    otpRequestId: string,
    code: string,
  ): Promise<void> {
    await useApi().post(`/customers/${customerId}/contact/verify/confirm`, {
      otpRequestId,
      code,
    })
  },

  /**
   * Otorga o revoca el consentimiento de comunicaciones comerciales LPDP (BE-CUS-06).
   */
  async toggleMarketingConsent(
    customerId: string,
    grant: boolean,
    version = 'v1.0-2026',
  ): Promise<void> {
    if (grant) {
      await useApi().post(`/customers/${customerId}/consents`, {
        purpose: 'MARKETING',
        version,
        channel: 'PORTAL',
      })
    } else {
      await useApi().delete(`/customers/${customerId}/consents/MARKETING`, {
        params: { channel: 'PORTAL' },
      })
    }
  },

  /**
   * Obtiene las solicitudes de devolución de saldo a favor del cliente (FE-CP-19).
   */
  async fetchRefunds(): Promise<RefundRequest[]> {
    const { data } = await useApi().get<RefundRequest[]>('/client/refunds')
    return data
  },

  /**
   * Solicita la devolución de saldo a favor a una cuenta bancaria verificada (FE-CP-19 / RF-17.3).
   */
  async requestRefund(amount: string, bankAccountId: string): Promise<RefundRequest> {
    const { data } = await useApi().post<RefundRequest>('/client/refunds', {
      amount,
      bankAccountId,
    })
    return data
  },
}
