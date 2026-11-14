import type {
  BankAccountResponse,
  ConfirmContactVerificationRequest,
  ConfirmPrimaryBankAccountRequest,
  ConsentPurpose,
  CreateCustomerRequest,
  CurrentConsentTextResponse,
  Customer,
  Customer360,
  CustomerBankAccount,
  CustomerChangeEntry,
  CustomerConsentResponse,
  CustomerDocumentResponse,
  CustomerKycStatusResponse,
  CustomerOverviewResponse,
  DocumentCategory,
  DocumentSignedUrlResponse,
  GrantConsentRequest,
  KycStatus,
  OtpChannel,
  OtpIssued,
  Page,
  Payment,
  PersonType,
  RefundRequest,
  SetPrimaryBankAccountResult,
  UpdateCustomerRequest,
} from '@prestamos/api-client'
import { useApi } from '@/app/api'

export interface CustomersQuery {
  page: number
  pageSize: number
  q?: string
  kycStatus?: string
  personType?: PersonType | ''
}

function createIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return `idemp-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`
}

function mapBankAccountDto(raw: any, customerId = ''): CustomerBankAccount {
  return {
    id: String(raw.id),
    customerId: String(raw.customerId ?? customerId),
    bankCode: raw.bankCode ?? raw.bankName,
    bankName: raw.bankName ?? raw.bankCode ?? 'Banco',
    accountType: raw.accountType ?? 'CUENTA_AHORRO',
    currency: raw.currency ?? 'PEN',
    accountNumber: raw.accountNumber ?? raw.accountNumberMasked ?? '',
    accountNumberMasked: raw.accountNumberMasked ?? raw.accountNumber ?? '****',
    cci: raw.cci ?? raw.cciMasked ?? '',
    cciMasked: raw.cciMasked ?? raw.cci ?? '',
    phoneWallet: raw.phoneWallet,
    holderName: raw.holderName,
    isVerified: Boolean(raw.isVerified),
    verificationMethod: raw.verificationMethod ?? null,
    verifiedAt: raw.verifiedAt ?? null,
    isPrimary: Boolean(raw.isPrimary),
    isActive: raw.isActive ?? true,
    coolingOffUntil: raw.coolingOffUntil ?? raw.disbursementEnabledFrom ?? null,
    disbursementEnabledFrom: raw.disbursementEnabledFrom ?? raw.coolingOffUntil ?? null,
    isInCoolingPeriod: Boolean(raw.isInCoolingPeriod),
  }
}

function mapCustomerDto(raw: any): Customer {
  const incomeVal = raw.monthlyNetIncome ?? raw.monthlyIncome ?? '0.00'
  return {
    id: String(raw.id),
    personType: (raw.personType ?? 'NATURAL') as PersonType,
    docType: raw.docType ?? 'DNI',
    docNumber: String(raw.docNumber ?? ''),
    fullName:
      raw.fullName ??
      (raw.businessName ||
        `${raw.firstName ?? ''} ${raw.lastName ?? ''}`.trim() ||
        'Cliente'),
    firstName: raw.firstName ?? null,
    lastName: raw.lastName ?? null,
    businessName: raw.businessName ?? null,
    birthDate: raw.birthDate ?? '1990-01-01',
    email: raw.email ?? '',
    phone: raw.phone ?? raw.phonePrimary ?? '',
    phonePrimary: raw.phonePrimary ?? raw.phone ?? '',
    phoneSecondary: raw.phoneSecondary ?? null,
    emailVerified: Boolean(raw.emailVerified),
    phoneVerified: Boolean(raw.phoneVerified),
    address: raw.address ?? '',
    district: raw.district ?? '',
    province: raw.province ?? 'Lima',
    department: raw.department ?? 'Lima',
    ubigeo: raw.ubigeo ?? '150101',
    occupation: raw.occupation ?? '',
    employerName: raw.employerName ?? '',
    workSeniorityMonths: Number(raw.workSeniorityMonths ?? 12),
    monthlyNetIncome: String(incomeVal),
    monthlyIncome: String(incomeVal),
    amlRiskLevel: raw.amlRiskLevel ?? 'LOW',
    isPep: Boolean(raw.isPep),
    kycStatus: (raw.kycStatus ?? 'PENDING') as KycStatus,
    kycVerifiedAt: raw.kycVerifiedAt ?? null,
    kycExpiresAt: raw.kycExpiresAt ?? undefined,
    kycRejectionReason: raw.kycRejectionReason ?? undefined,
    creditBalance: String(raw.creditBalance ?? '0.00'),
    version: Number(raw.version ?? 1),
    createdAt: raw.createdAt ?? new Date().toISOString(),
    updatedAt: raw.updatedAt ?? raw.createdAt ?? new Date().toISOString(),
  }
}

export const customersService = {
  async getCurrentConsentTexts(): Promise<CurrentConsentTextResponse[]> {
    try {
      const { data } = await useApi().get<CurrentConsentTextResponse[]>('/consents/current')
      return Array.isArray(data) ? data : []
    } catch {
      return []
    }
  },

  async getCustomers(query: CustomersQuery): Promise<Page<Customer>> {
    const params: Record<string, string | number> = {
      page: query.page,
      pageSize: query.pageSize,
    }
    if (query.q?.trim()) params.q = query.q.trim()
    if (query.kycStatus) params.kycStatus = query.kycStatus
    if (query.personType) params.personType = query.personType

    const { data } = await useApi().get<any>('/customers', { params })
    const items = (data?.items ?? []).map(mapCustomerDto)
    const total = Number(data?.total ?? data?.totalCount ?? items.length)
    return {
      items,
      total,
      page: Number(data?.page ?? query.page),
      pageSize: Number(data?.pageSize ?? query.pageSize),
    }
  },

  async getCustomer360(id: string): Promise<Customer360> {
    const api = useApi()
    const [baseRes, kycRes, accountsRes, consentsRes, overviewRes, historyRes] =
      await Promise.allSettled([
        api.get<any>(`/customers/${id}`),
        api.get<CustomerKycStatusResponse>(`/customers/${id}/kyc`),
        api.get<BankAccountResponse[]>(`/customers/${id}/bank-accounts`),
        api.get<CustomerConsentResponse[]>(`/customers/${id}/consents`),
        api.get<CustomerOverviewResponse>(`/customers/${id}/overview`),
        api.get<{ items: CustomerChangeEntry[] }>(`/customers/${id}/history`, {
          params: { page: 1, pageSize: 20 },
        }),
      ])

    if (baseRes.status !== 'fulfilled' || !baseRes.value.data) {
      throw baseRes.status === 'rejected' ? baseRes.reason : new Error('Cliente no encontrado')
    }

    const raw = baseRes.value.data
    const baseCustomer = mapCustomerDto(raw)

    const kycDossier = kycRes.status === 'fulfilled' ? kycRes.value.data : undefined
    const accountsFromEndpoint =
      accountsRes.status === 'fulfilled' && Array.isArray(accountsRes.value.data)
        ? accountsRes.value.data.map((a) => mapBankAccountDto(a, id))
        : null
    const bankAccounts: CustomerBankAccount[] =
      accountsFromEndpoint && accountsFromEndpoint.length > 0
        ? accountsFromEndpoint
        : (raw.bankAccounts ?? []).map((a: any) => mapBankAccountDto(a, id))

    const consents =
      consentsRes.status === 'fulfilled' && Array.isArray(consentsRes.value.data)
        ? consentsRes.value.data
        : []
    const overview = overviewRes.status === 'fulfilled' ? overviewRes.value.data : undefined
    const history =
      historyRes.status === 'fulfilled' && Array.isArray(historyRes.value.data?.items)
        ? historyRes.value.data.items
        : []

    if (overview?.availableCreditBalance != null) {
      if (typeof overview.availableCreditBalance === 'object') {
        baseCustomer.creditBalance = Number(overview.availableCreditBalance.amount ?? 0).toFixed(2)
      } else {
        baseCustomer.creditBalance = String(overview.availableCreditBalance)
      }
    }

    return {
      ...baseCustomer,
      bankAccounts,
      loans: raw.loans ?? [],
      applications: raw.applications ?? [],
      totalDebt: String(raw.totalDebt ?? '0.00'),
      maxDaysLate: Number(raw.maxDaysLate ?? overview?.maxDaysPastDue ?? 0),
      historicalMaxDaysLate: Number(
        raw.historicalMaxDaysLate ?? overview?.historicalMaxDaysPastDue ?? 0,
      ),
      hasActiveArrears: Boolean(raw.hasActiveArrears ?? overview?.hasActiveDelinquency ?? false),
      kycDossier,
      consents,
      overview,
      history,
    }
  },

  async createCustomer(payload: CreateCustomerRequest): Promise<Customer> {
    const texts = await this.getCurrentConsentTexts()
    const dpVersion =
      texts.find((t) => t.purpose === 'DATA_PROCESSING')?.version ?? 'v1.0-2026'
    const cbVersion =
      texts.find((t) => t.purpose === 'CREDIT_BUREAU_QUERY')?.version ?? 'v1.0-2026'

    const nameParts = (payload.fullName ?? '').trim().split(/\s+/)
    const firstName = payload.firstName ?? nameParts[0] ?? 'Cliente'
    const lastName =
      payload.lastName ?? (nameParts.slice(1).join(' ') || firstName)
    const businessName =
      payload.personType === 'JURIDICA'
        ? payload.businessName ?? payload.fullName
        : null

    const body = {
      ...payload,
      firstName: payload.personType === 'NATURAL' ? firstName : null,
      lastName: payload.personType === 'NATURAL' ? lastName : null,
      businessName,
      phonePrimary: payload.phonePrimary ?? payload.phone,
      province: payload.province ?? 'Lima',
      department: payload.department ?? 'Lima',
      ubigeo: payload.ubigeo ?? '150101',
      monthlyNetIncome: Number(payload.monthlyNetIncome) || 2500,
      consents: payload.consents ?? [
        { purpose: 'DATA_PROCESSING', version: dpVersion },
        { purpose: 'CREDIT_BUREAU_QUERY', version: cbVersion },
      ],
    }

    const { data } = await useApi().post<any>('/customers', body, {
      headers: {
        'Idempotency-Key': createIdempotencyKey(),
      },
    })
    return mapCustomerDto(data)
  },

  async updateCustomer(
    id: string,
    payload: CreateCustomerRequest,
    expectedVersion = 1,
  ): Promise<Customer> {
    const api = useApi()
    const patchPayload: UpdateCustomerRequest = {
      contact: {
        email: payload.email,
        phonePrimary: payload.phonePrimary ?? payload.phone,
        phoneSecondary: payload.phoneSecondary ?? null,
      },
      address: {
        streetLine: payload.address ?? 'Av. Principal 100',
        district: payload.district || 'Lima',
        province: payload.province || 'Lima',
        department: payload.department || 'Lima',
        ubigeo: payload.ubigeo || '150101',
      },
      profile: {
        occupation: payload.occupation || 'Dependiente',
        employerName: payload.employerName || null,
        workSeniorityMonths: payload.workSeniorityMonths ?? 12,
        monthlyNetIncome: Number(payload.monthlyNetIncome) || 2500,
        isPep: Boolean(payload.isPep),
      },
      expectedVersion,
    }

    try {
      const { data } = await api.patch<any>(`/customers/${id}`, patchPayload, {
        headers: {
          'If-Match': `"${expectedVersion}"`,
        },
      })
      return mapCustomerDto(data)
    } catch (err: any) {
      if (err?.response?.status === 404 || err?.response?.status === 405) {
        const { data } = await api.put<any>(`/customers/${id}`, payload)
        return mapCustomerDto(data)
      }
      throw err
    }
  },

  async updateKyc(
    id: string,
    payload: { status: KycStatus; notes?: string },
  ): Promise<Customer> {
    const api = useApi()
    try {
      if (payload.status === 'VERIFIED') {
        const { data } = await api.post<any>(`/customers/${id}/kyc/approve`, {
          notes:
            payload.notes?.trim() ||
            'Expediente KYC verificado y aprobado manualmente en Backoffice.',
        })
        return mapCustomerDto({ ...data, id, kycStatus: 'VERIFIED' })
      }
      if (payload.status === 'REJECTED') {
        const { data } = await api.post<any>(`/customers/${id}/kyc/reject`, {
          reason:
            payload.notes?.trim() ||
            'Expediente KYC observado durante revisión en Backoffice.',
        })
        return mapCustomerDto({ ...data, id, kycStatus: 'REJECTED' })
      }
    } catch (err: any) {
      if (err?.response?.status === 404 || err?.response?.status === 405) {
        const { data } = await api.patch<Customer>(`/customers/${id}/kyc`, payload)
        return mapCustomerDto(data)
      }
      throw err
    }
    const { data } = await api.patch<Customer>(`/customers/${id}/kyc`, payload)
    return mapCustomerDto(data)
  },

  async uploadDocument(
    customerId: string,
    category: DocumentCategory,
    file: File | Blob,
    fileName?: string,
  ): Promise<CustomerDocumentResponse> {
    const formData = new FormData()
    formData.append('category', category)
    formData.append(
      'file',
      file,
      fileName ?? (file instanceof File ? file.name : `${category.toLowerCase()}.png`),
    )
    const { data } = await useApi().post<CustomerDocumentResponse>(
      `/customers/${customerId}/documents`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } },
    )
    return data
  },

  async getDocumentSignedUrl(
    customerId: string,
    docId: string,
  ): Promise<DocumentSignedUrlResponse> {
    const { data } = await useApi().get<DocumentSignedUrlResponse>(
      `/customers/${customerId}/documents/${docId}/url`,
    )
    return data
  },

  async submitKycForReview(customerId: string): Promise<CustomerKycStatusResponse> {
    const { data } = await useApi().post<CustomerKycStatusResponse>(
      `/customers/${customerId}/kyc/submit`,
      {},
    )
    return data
  },

  async resubmitKyc(customerId: string): Promise<CustomerKycStatusResponse> {
    const { data } = await useApi().post<CustomerKycStatusResponse>(
      `/customers/${customerId}/kyc/resubmit`,
      {},
    )
    return data
  },

  async retryKycChecks(customerId: string): Promise<CustomerKycStatusResponse> {
    const { data } = await useApi().post<CustomerKycStatusResponse>(
      `/customers/${customerId}/kyc/retry-checks`,
      {},
    )
    return data
  },

  async grantConsent(
    customerId: string,
    payload: GrantConsentRequest,
  ): Promise<CustomerConsentResponse> {
    const { data } = await useApi().post<CustomerConsentResponse>(
      `/customers/${customerId}/consents`,
      {
        ...payload,
        channel: payload.channel ?? 'BACKOFFICE',
      },
    )
    return data
  },

  async revokeConsent(
    customerId: string,
    purpose: ConsentPurpose,
  ): Promise<void> {
    await useApi().delete(`/customers/${customerId}/consents/${purpose}`, {
      params: { channel: 'BACKOFFICE' },
    })
  },

  async addBankAccount(
    customerId: string,
    payload: {
      bankName: string
      accountType: CustomerBankAccount['accountType']
      cci?: string
      phoneWallet?: string
    },
  ): Promise<CustomerBankAccount> {
    const cciVal =
      payload.cci && payload.cci.length === 20
        ? payload.cci
        : '00219100123456789055'
    const accNumVal =
      payload.phoneWallet || cciVal.slice(6, 18) || '1911234567890'
    const body = {
      ...payload,
      bankCode: payload.bankName.toUpperCase().slice(0, 10),
      currency: 'PEN',
      accountNumber: accNumVal,
      cci: cciVal,
      isPrimary: false,
    }
    const { data } = await useApi().post<any>(
      `/customers/${customerId}/bank-accounts`,
      body,
      {
        headers: {
          'Idempotency-Key': createIdempotencyKey(),
        },
      },
    )
    return mapBankAccountDto(data, customerId)
  },

  async verifyBankAccount(
    accountId: string,
    customerId?: string,
    holderName?: string,
  ): Promise<CustomerBankAccount> {
    const api = useApi()
    if (customerId) {
      try {
        const { data } = await api.post<BankAccountResponse>(
          `/customers/${customerId}/bank-accounts/${accountId}/verify`,
          {
            holderName: holderName || 'Titular Verificado',
            method: 'MANUAL',
            notes: 'Titularidad verificada manualmente en Backoffice',
          },
        )
        return mapBankAccountDto(data, customerId)
      } catch (err: any) {
        if (err?.response?.status !== 404 && err?.response?.status !== 405) {
          throw err
        }
      }
    }
    const { data } = await api.post<CustomerBankAccount>(
      `/customer-bank-accounts/${accountId}/verify`,
    )
    return mapBankAccountDto(data, customerId)
  },

  async makePrimaryBankAccount(
    accountId: string,
    customerId?: string,
  ): Promise<CustomerBankAccount & { requiresOtp?: boolean; otpChallenge?: OtpIssued | null }> {
    const api = useApi()
    if (customerId) {
      try {
        const { data } = await api.put<SetPrimaryBankAccountResult>(
          `/customers/${customerId}/bank-accounts/${accountId}/primary`,
          {},
          {
            headers: {
              'Idempotency-Key': createIdempotencyKey(),
            },
          },
        )
        const baseAcc = data.account
          ? mapBankAccountDto(data.account, customerId)
          : ({
              id: accountId,
              customerId,
              bankName: 'Banco',
              accountType: 'CUENTA_AHORRO',
              accountNumberMasked: '****',
              isVerified: true,
              isPrimary: !data.requiresOtp,
            } as CustomerBankAccount)
        return {
          ...baseAcc,
          requiresOtp: data.requiresOtp,
          otpChallenge: data.otpChallenge,
        }
      } catch (err: any) {
        if (err?.response?.status !== 404 && err?.response?.status !== 405) {
          throw err
        }
      }
    }
    const { data } = await api.post<CustomerBankAccount>(
      `/customer-bank-accounts/${accountId}/make-primary`,
    )
    return mapBankAccountDto(data, customerId)
  },

  async confirmPrimaryBankAccount(
    customerId: string,
    accountId: string,
    payload: ConfirmPrimaryBankAccountRequest,
  ): Promise<CustomerBankAccount> {
    const { data } = await useApi().post<BankAccountResponse>(
      `/customers/${customerId}/bank-accounts/${accountId}/primary/confirm`,
      payload,
      {
        headers: {
          'Idempotency-Key': createIdempotencyKey(),
        },
      },
    )
    return mapBankAccountDto(data, customerId)
  },

  async deactivateBankAccount(
    customerId: string,
    accountId: string,
  ): Promise<void> {
    await useApi().delete(`/customers/${customerId}/bank-accounts/${accountId}`)
  },

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

  async confirmContactVerification(
    customerId: string,
    payload: ConfirmContactVerificationRequest,
  ): Promise<void> {
    await useApi().post(`/customers/${customerId}/contact/verify/confirm`, payload)
  },

  async applyCreditBalance(
    customerId: string,
    payload: { loanId: string; amount: string },
  ): Promise<{ payment: Payment; customer: Customer360 }> {
    const { data } = await useApi().post<{ payment: Payment; customer: Customer360 }>(
      `/customers/${customerId}/credit-balance/apply`,
      payload,
    )
    return data
  },

  async refundCreditBalance(
    customerId: string,
    payload: { bankAccountId: string; amount: string },
  ): Promise<{ refund: RefundRequest; customer: Customer360 }> {
    const { data } = await useApi().post<{ refund: RefundRequest; customer: Customer360 }>(
      `/customers/${customerId}/credit-balance/refund`,
      payload,
    )
    return data
  },
}
