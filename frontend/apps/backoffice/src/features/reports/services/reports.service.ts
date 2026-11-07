import type {
  AccountStatement,
  AgingBucket,
  CashflowReport,
  DashboardKpis,
  Loan,
  Page,
  ProductivityReport,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

export interface AgingSummaryRow {
  bucket: AgingBucket
  loans: number
  overduePrincipal: string
  totalDue: string
}

export interface PortfolioReportParams {
  q?: string
  page?: number
  pageSize?: number
}

export interface CashflowReportParams {
  from?: string
  to?: string
}

/**
 * Servicio de Reportes (FE-BO-37 a FE-BO-40).
 * Maneja llamadas a la API .NET / backend simulado (MSW) y exportaciones.
 */
export const reportsService = {
  /** Obtiene los KPIs ejecutivos del dashboard (FE-BO-37 y FE-BO-38). */
  async fetchDashboardKpis(): Promise<DashboardKpis> {
    const { data } = await useApi().get<DashboardKpis>('/reports/dashboard-kpis')
    return data
  },

  /** Obtiene el reporte de cartera de créditos (FE-BO-39). */
  async fetchPortfolioReport(params: PortfolioReportParams = {}): Promise<Page<Loan>> {
    const queryParams: Record<string, string | number> = {}
    if (params.page != null) queryParams.page = params.page
    if (params.pageSize != null) queryParams.pageSize = params.pageSize
    if (params.q?.trim()) queryParams.q = params.q.trim()

    const { data } = await useApi().get<Page<Loan>>('/reports/portfolio', {
      params: queryParams,
    })
    return data
  },

  /** Obtiene el consolidado de cartera por tramos de aging (FE-BO-39). */
  async fetchAgingReport(): Promise<AgingSummaryRow[]> {
    const { data } = await useApi().get<AgingSummaryRow[]>('/reports/aging')
    return data
  },

  /** Obtiene el reporte de flujo de caja para un rango de fechas (FE-BO-39). */
  async fetchCashflowReport(params: CashflowReportParams = {}): Promise<CashflowReport> {
    const queryParams: Record<string, string> = {}
    if (params.from) queryParams.from = params.from
    if (params.to) queryParams.to = params.to

    const { data } = await useApi().get<CashflowReport>('/reports/cashflow', {
      params: queryParams,
    })
    return data
  },

  /** Obtiene la productividad de analistas de crédito y gestores de cobranza (FE-BO-39). */
  async fetchProductivityReport(): Promise<ProductivityReport> {
    const { data } = await useApi().get<ProductivityReport>('/reports/productivity')
    return data
  },

  /** Obtiene el estado de cuenta oficial de un préstamo con sus movimientos (FE-BO-40). */
  async fetchAccountStatement(loanId: string): Promise<AccountStatement> {
    const { data } = await useApi().get<AccountStatement>(`/reports/account-statement/${loanId}`)
    return data
  },

  /** Exporta una matriz de datos a archivo CSV descargable en el navegador. */
  exportToCsv(filename: string, headers: string[], rows: (string | number)[][]): void {
    const sanitize = (val: string | number) => `"${String(val).replace(/"/g, '""')}"`
    const csvContent = [
      headers.map(sanitize).join(','),
      ...rows.map((row) => row.map(sanitize).join(',')),
    ].join('\r\n')

    const blob = new Blob(['\uFEFF' + csvContent], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', `${filename}.csv`)
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
  },
}
