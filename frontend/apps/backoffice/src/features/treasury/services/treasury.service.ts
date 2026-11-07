import type {
  BankStatementLine,
  CompanyBankAccount,
  CreateInternalTransferRequest,
  DailyBalance,
  Disbursement,
  InternalAccountTransfer,
  Payment,
} from '@prestamos/api-client'
import { useApi } from '@/app/api'

export interface ImportStatementResult {
  imported: number
  matched: number
  pending: number
  duplicated: number
}

export const treasuryService = {
  async getCompanyAccounts(): Promise<CompanyBankAccount[]> {
    const { data } = await useApi().get<CompanyBankAccount[]>('/treasury/bank-accounts')
    return data
  },

  async createCompanyAccount(payload: {
    bankName: string
    accountNumberMasked: string
    cci: string
    currency: 'PEN' | 'USD'
    isDisbursement: boolean
    isCollection: boolean
  }): Promise<CompanyBankAccount> {
    const { data } = await useApi().post<CompanyBankAccount>('/treasury/bank-accounts', payload)
    return data
  },

  async toggleCompanyAccountActive(
    id: string,
    isActive: boolean,
  ): Promise<CompanyBankAccount> {
    const { data } = await useApi().patch<CompanyBankAccount>(`/treasury/bank-accounts/${id}`, {
      isActive,
    })
    return data
  },

  async getDisbursements(status?: string): Promise<Disbursement[]> {
    const params: Record<string, string> = {}
    if (status) params.status = status
    const { data } = await useApi().get<{ items: Disbursement[] }>('/treasury/disbursements', {
      params,
    })
    return data.items
  },

  async retryDisbursement(id: string): Promise<Disbursement> {
    const { data } = await useApi().post<Disbursement>(`/disbursements/${id}/retry`, {})
    return data
  },

  async getReconciliationLines(): Promise<BankStatementLine[]> {
    const { data } = await useApi().get<{ items: BankStatementLine[] }>('/reconciliation')
    return data.items
  },

  async importStatement(payload: {
    bankAccountId: string
    lines: {
      operationNumber: string
      valueDate: string
      amount: string
      description: string
    }[]
  }): Promise<ImportStatementResult> {
    const { data } = await useApi().post<ImportStatementResult>('/reconciliation/import', payload)
    return data
  },

  async matchStatementLine(statementLineId: string, paymentId: string): Promise<void> {
    await useApi().post(`/reconciliation/${statementLineId}/match`, { paymentId })
  },

  async getPendingConfirmationPayments(): Promise<Payment[]> {
    const { data } = await useApi().get<{ items: Payment[] }>(
      '/payments?status=PENDING_CONFIRMATION',
    )
    return data.items
  },

  async getDailyBalance(bankAccountId: string, date: string): Promise<DailyBalance> {
    const { data } = await useApi().get<DailyBalance>('/treasury/daily-balance', {
      params: { bankAccountId, date },
    })
    return data
  },

  async getInternalTransfers(): Promise<InternalAccountTransfer[]> {
    const { data } = await useApi().get<{ items: InternalAccountTransfer[] }>(
      '/treasury/internal-transfers',
    )
    return data.items
  },

  async createInternalTransfer(
    payload: CreateInternalTransferRequest,
  ): Promise<InternalAccountTransfer> {
    const { data } = await useApi().post<InternalAccountTransfer>(
      '/treasury/internal-transfers',
      payload,
    )
    return data
  },
}
