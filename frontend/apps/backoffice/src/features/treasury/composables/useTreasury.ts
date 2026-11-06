import { ref } from 'vue'
import { toast } from 'vue-sonner'
import type {
  BankStatementLine,
  CompanyBankAccount,
  DailyBalance,
  Disbursement,
  InternalAccountTransfer,
} from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { treasuryService } from '../services/treasury.service'

export function useTreasury() {
  const loading = ref(false)
  const activeTab = ref<'accounts' | 'transfers' | 'disbursements' | 'reconciliation' | 'balance'>(
    'accounts',
  )

  const companyAccounts = ref<CompanyBankAccount[]>([])
  const internalTransfers = ref<InternalAccountTransfer[]>([])
  const disbursements = ref<Disbursement[]>([])
  const selectedDisbursementStatus = ref('PENDING,PROCESSING,FAILED')
  const retryingDisbursementId = ref<string | null>(null)
  const statements = ref<BankStatementLine[]>([])
  const dailyBalance = ref<DailyBalance | null>(null)
  const selectedBalanceAccId = ref('bca-001')
  const selectedBalanceDate = ref(new Date().toISOString().slice(0, 10))
  const loadingBalance = ref(false)

  async function loadCompanyAccounts() {
    try {
      companyAccounts.value = await treasuryService.getCompanyAccounts()
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al cargar cuentas corporativas', { description: apiError.detail })
    }
  }

  async function toggleAccountActive(acc: CompanyBankAccount) {
    try {
      const data = await treasuryService.toggleCompanyAccountActive(acc.id, !acc.isActive)
      acc.isActive = data.isActive
      toast.success(acc.isActive ? 'Cuenta activada' : 'Cuenta desactivada', {
        description: `La cuenta ${acc.bankName} cambió de estado.`,
      })
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('No se pudo actualizar cuenta', { description: apiError.detail })
    }
  }

  async function loadDisbursements() {
    try {
      disbursements.value = await treasuryService.getDisbursements(
        selectedDisbursementStatus.value || undefined,
      )
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al cargar desembolsos', { description: apiError.detail })
    }
  }

  async function retryDisbursement(disb: Disbursement) {
    retryingDisbursementId.value = disb.id
    try {
      await treasuryService.retryDisbursement(disb.id)
      toast.success('Desembolso reintentado', {
        description: `La orden de transferencia #${disb.id} volvió al estado PENDIENTE de transmisión.`,
      })
      void loadDisbursements()
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al reintentar desembolso', { description: apiError.detail })
    } finally {
      retryingDisbursementId.value = null
    }
  }

  async function loadReconciliation() {
    try {
      statements.value = await treasuryService.getReconciliationLines()
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al cargar extractos', { description: apiError.detail })
    }
  }

  async function loadDailyBalance() {
    loadingBalance.value = true
    try {
      dailyBalance.value = await treasuryService.getDailyBalance(
        selectedBalanceAccId.value,
        selectedBalanceDate.value,
      )
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al consultar cuadre diario', { description: apiError.detail })
    } finally {
      loadingBalance.value = false
    }
  }

  async function loadInternalTransfers() {
    try {
      internalTransfers.value = await treasuryService.getInternalTransfers()
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al cargar transferencias internas', { description: apiError.detail })
    }
  }

  function switchTab(tab: typeof activeTab.value) {
    activeTab.value = tab
    if (tab === 'accounts') void loadCompanyAccounts()
    if (tab === 'transfers') void loadInternalTransfers()
    if (tab === 'disbursements') void loadDisbursements()
    if (tab === 'reconciliation') void loadReconciliation()
    if (tab === 'balance') void loadDailyBalance()
  }

  return {
    loading,
    activeTab,
    companyAccounts,
    internalTransfers,
    disbursements,
    selectedDisbursementStatus,
    retryingDisbursementId,
    statements,
    dailyBalance,
    selectedBalanceAccId,
    selectedBalanceDate,
    loadingBalance,
    loadCompanyAccounts,
    loadInternalTransfers,
    toggleAccountActive,
    loadDisbursements,
    retryDisbursement,
    loadReconciliation,
    loadDailyBalance,
    switchTab,
    createCompanyAccount: treasuryService.createCompanyAccount,
    createInternalTransfer: treasuryService.createInternalTransfer,
    importStatement: treasuryService.importStatement,
    matchStatementLine: treasuryService.matchStatementLine,
    getPendingConfirmationPayments: treasuryService.getPendingConfirmationPayments,
  }
}
