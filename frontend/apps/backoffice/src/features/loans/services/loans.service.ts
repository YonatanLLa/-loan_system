import type {
  CompanyBankAccount,
  Customer360,
  CustomerBankAccount,
  Disbursement,
  Loan,
  LoanApplication,
  Page,
  Payment,
  PrepaymentQuote,
  Schedule,
} from '@prestamos/api-client'
import { useApi } from '@/app/api'

export interface LoansQuery {
  page: number
  pageSize: number
  q?: string
  status?: string
}

export const loansService = {
  async getLoans(query: LoansQuery): Promise<Page<Loan>> {
    const params: Record<string, string | number> = {
      page: query.page,
      pageSize: query.pageSize,
    }
    if (query.q?.trim()) params.q = query.q.trim()
    if (query.status) params.status = query.status

    const { data } = await useApi().get<Page<Loan>>('/loans', { params })
    return data
  },

  async getLoan(id: string): Promise<Loan> {
    const { data } = await useApi().get<Loan>(`/loans/${id}`)
    return data
  },

  async getSchedule(loanId: string, version: number): Promise<Schedule> {
    const { data } = await useApi().get<Schedule>(`/loans/${loanId}/schedule`, {
      params: { version },
    })
    return data
  },

  async getLoanPayments(loanId: string): Promise<Payment[]> {
    const { data } = await useApi().get<Payment[]>(`/loans/${loanId}/payments`)
    return data
  },

  async getCustomer(customerId: string): Promise<Customer360> {
    const { data } = await useApi().get<Customer360>(`/customers/${customerId}`)
    return data
  },

  async getDisbursement(disbursementId: string): Promise<Disbursement> {
    const { data } = await useApi().get<Disbursement>(`/treasury/disbursements/${disbursementId}`)
    return data
  },

  async downloadContractPdf(loanId: string): Promise<Blob> {
    const { data } = await useApi().get<Blob>(`/loans/${loanId}/documents/contract.pdf`, {
      responseType: 'blob',
    })
    return data
  },

  async getApprovedApplications(): Promise<LoanApplication[]> {
    const { data } = await useApi().get<{ items: LoanApplication[] }>(
      '/loan-applications?status=APROBADO,APROBADO_CON_CONDICIONES',
    )
    return data.items
  },

  async getCompanyDisbursementAccounts(): Promise<CompanyBankAccount[]> {
    const { data } = await useApi().get<CompanyBankAccount[]>('/treasury/bank-accounts')
    return data.filter((a) => a.isActive && a.isDisbursement)
  },

  async getCustomerVerifiedAccounts(customerId: string): Promise<CustomerBankAccount[]> {
    const { data } = await useApi().get<CustomerBankAccount[]>(
      `/customers/${customerId}/bank-accounts`,
    )
    return data.filter((a) => a.isVerified)
  },

  async createDisbursementLoan(
    payload: {
      applicationId: string
      companyBankAccountId: string
      customerBankAccountId: string
    },
    idempotencyKey: string,
  ): Promise<Loan> {
    const { data } = await useApi().post<Loan>('/loans', payload, {
      headers: { 'Idempotency-Key': idempotencyKey },
    })
    return data
  },

  async quotePrepayment(loanId: string): Promise<PrepaymentQuote> {
    const { data } = await useApi().post<PrepaymentQuote>(`/loans/${loanId}/quote-prepayment`, {})
    return data
  },

  async payPrepayment(
    loanId: string,
    payload: {
      amount: string
      method: string
      externalReference: string
      companyBankAccountId: string
    },
    idempotencyKey: string,
  ): Promise<Payment> {
    const { data } = await useApi().post<Payment>(`/loans/${loanId}/payments`, payload, {
      headers: { 'Idempotency-Key': idempotencyKey },
    })
    return data
  },

  async restructureLoan(
    loanId: string,
    payload: { term: number; annualRate?: string; graceDays?: number },
  ): Promise<Loan> {
    const { data } = await useApi().post<Loan>(`/loans/${loanId}/restructure`, payload)
    return data
  },

  async refinanceLoans(payload: {
    loanIds: string[]
    term: number
    annualRate?: string
    graceDays?: number
  }): Promise<{ newLoan: Loan; consolidatedLoans: string[] }> {
    const { data } = await useApi().post<{ newLoan: Loan; consolidatedLoans: string[] }>(
      '/loans/refinance',
      payload,
    )
    return data
  },
}
