import { computed, ref } from 'vue'
import type {
  AccountStatement,
  CashflowReport,
  Loan,
  Page,
  ProductivityReport,
} from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { toast } from 'vue-sonner'

import { reportsService, type AgingSummaryRow } from '../services/reports.service'

export function useReports() {
  const activeTab = ref<'portfolio' | 'aging' | 'cashflow' | 'productivity' | 'statement'>('portfolio')

  // 1. Cartera
  const portfolioPage = ref<Page<Loan>>({ items: [], total: 0, page: 1, pageSize: 10 })
  const portfolioQuery = ref('')
  const portfolioLoading = ref(false)

  // 2. Aging
  const agingSummary = ref<AgingSummaryRow[]>([])
  const agingLoading = ref(false)

  // 3. Flujo de Caja
  const cashflow = ref<CashflowReport | null>(null)
  const cashflowFrom = ref(new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10))
  const cashflowTo = ref(new Date().toISOString().slice(0, 10))
  const cashflowLoading = ref(false)

  // 4. Productividad
  const productivity = ref<ProductivityReport | null>(null)
  const productivityLoading = ref(false)

  // 5. Estado de Cuenta
  const statementSearchId = ref('')
  const statement = ref<AccountStatement | null>(null)
  const statementLoading = ref(false)

  // ── Acciones de Cartera ───────────────────────────────────────────────
  async function loadPortfolio(page = 1) {
    portfolioLoading.value = true
    try {
      portfolioPage.value = await reportsService.fetchPortfolioReport({
        page,
        pageSize: portfolioPage.value.pageSize,
        q: portfolioQuery.value,
      })
    } catch (err) {
      toast.error('Error al cargar reporte de cartera', { description: toApiError(err).detail })
    } finally {
      portfolioLoading.value = false
    }
  }

  function exportPortfolioCsv() {
    if (portfolioPage.value.items.length === 0) return
    const headers = [
      'Código',
      'Cliente',
      'Producto',
      'Capital',
      'Saldo Insoluto',
      'Tasa Anual',
      'Plazo',
      'Estado',
    ]
    const rows = portfolioPage.value.items.map((l) => [
      l.id,
      l.customerName,
      l.productName,
      l.principal,
      l.outstandingPrincipal,
      `${(Number(l.annualRate) * 100).toFixed(2)}%`,
      l.term,
      l.status,
    ])
    reportsService.exportToCsv(`Reporte-Cartera-${new Date().toISOString().slice(0, 10)}`, headers, rows)
  }

  // ── Acciones de Aging ────────────────────────────────────────────────
  async function loadAging() {
    agingLoading.value = true
    try {
      agingSummary.value = await reportsService.fetchAgingReport()
    } catch (err) {
      toast.error('Error al cargar reporte de aging', { description: toApiError(err).detail })
    } finally {
      agingLoading.value = false
    }
  }

  function exportAgingCsv() {
    if (agingSummary.value.length === 0) return
    const headers = ['Tramo', 'Cantidad de Créditos', 'Capital Vencido', 'Total Exigible']
    const rows = agingSummary.value.map((r) => [
      r.bucket,
      r.loans,
      r.overduePrincipal,
      r.totalDue,
    ])
    reportsService.exportToCsv(`Reporte-Aging-${new Date().toISOString().slice(0, 10)}`, headers, rows)
  }

  // ── Acciones de Flujo de Caja ────────────────────────────────────────
  async function loadCashflow() {
    cashflowLoading.value = true
    try {
      cashflow.value = await reportsService.fetchCashflowReport({
        from: cashflowFrom.value,
        to: cashflowTo.value,
      })
    } catch (err) {
      toast.error('Error al cargar flujo de caja', { description: toApiError(err).detail })
    } finally {
      cashflowLoading.value = false
    }
  }

  function exportCashflowCsv() {
    if (!cashflow.value) return
    const headers = ['Concepto', 'Monto (S/)']
    const rows = [
      ['Ingreso: Amortización de Capital', cashflow.value.inflows.principal],
      ['Ingreso: Intereses Corrientes', cashflow.value.inflows.interest],
      ['Ingreso: Penalidad de Mora', cashflow.value.inflows.mora],
      ['Ingreso: Comisiones y Gastos', cashflow.value.inflows.fees],
      ['Egreso: Desembolsos de Crédito', cashflow.value.outflows.disbursements],
      ['Egreso: Comisiones Bancarias', cashflow.value.outflows.bankFees],
      ['Flujo Neto del Periodo', cashflow.value.net],
    ]
    reportsService.exportToCsv(
      `Flujo-Caja-${cashflow.value.from}-al-${cashflow.value.to}`,
      headers,
      rows,
    )
  }

  // ── Acciones de Productividad ────────────────────────────────────────
  async function loadProductivity() {
    productivityLoading.value = true
    try {
      productivity.value = await reportsService.fetchProductivityReport()
    } catch (err) {
      toast.error('Error al cargar productividad', { description: toApiError(err).detail })
    } finally {
      productivityLoading.value = false
    }
  }

  // ── Acciones de Estado de Cuenta ─────────────────────────────────────
  async function loadStatement(loanId?: string) {
    const target = loanId || statementSearchId.value.trim()
    if (!target) return
    statementLoading.value = true
    try {
      statement.value = await reportsService.fetchAccountStatement(target)
      statementSearchId.value = target
    } catch (err) {
      toast.error('No se encontró el préstamo', { description: toApiError(err).detail })
    } finally {
      statementLoading.value = false
    }
  }

  const portfolioPages = computed(() =>
    Math.max(1, Math.ceil(portfolioPage.value.total / portfolioPage.value.pageSize)),
  )

  return {
    activeTab,
    // Cartera
    portfolioPage,
    portfolioQuery,
    portfolioLoading,
    portfolioPages,
    loadPortfolio,
    exportPortfolioCsv,
    // Aging
    agingSummary,
    agingLoading,
    loadAging,
    exportAgingCsv,
    // Flujo de Caja
    cashflow,
    cashflowFrom,
    cashflowTo,
    cashflowLoading,
    loadCashflow,
    exportCashflowCsv,
    // Productividad
    productivity,
    productivityLoading,
    loadProductivity,
    // Estado de Cuenta
    statementSearchId,
    statement,
    statementLoading,
    loadStatement,
  }
}
