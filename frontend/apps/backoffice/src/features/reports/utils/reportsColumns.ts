import type {
  AccountStatement,
  Loan,
  ProductivityReport,
} from '@prestamos/api-client'
import type { TableColumn } from '@prestamos/ui'
import type { AgingSummaryRow } from '../services/reports.service'

export type AnalystRow = ProductivityReport['analysts'][number]
export type CollectorRow = ProductivityReport['collectors'][number]
export type StatementMovement = AccountStatement['movements'][number]

export const portfolioColumns: TableColumn<Loan>[] = [
  { key: 'id', label: 'Código', kind: 'mono', cellClass: 'font-semibold' },
  {
    key: 'customer',
    label: 'Cliente',
    value: (loan) => loan.customerName,
    cellClass: 'font-medium',
  },
  {
    key: 'product',
    label: 'Producto',
    value: (loan) => loan.productName,
    cellClass: 'text-muted-foreground',
  },
  { key: 'principal', label: 'Monto Capital', kind: 'money' },
  {
    key: 'outstanding',
    label: 'Saldo Insoluto',
    kind: 'money',
    cellClass: 'font-bold',
    value: (loan) => loan.outstandingPrincipal,
  },
  {
    key: 'rate',
    label: 'TEA',
    value: (loan) => `${(Number(loan.annualRate) * 100).toFixed(1)}%`,
  },
  { key: 'status', label: 'Estado', kind: 'status', statusKind: 'loan' },
]

export const agingReportColumns: TableColumn<AgingSummaryRow>[] = [
  {
    key: 'bucket',
    label: 'Tramo de Aging',
    value: (row) => row.bucket.replace(/_/g, ' '),
    cellClass: 'font-semibold',
  },
  {
    key: 'loans',
    label: 'Préstamos en Mora',
    value: (row) => `${row.loans} créditos`,
    cellClass: 'font-bold',
  },
  {
    key: 'principal',
    label: 'Capital Vencido',
    kind: 'money',
    cellClass: 'text-destructive',
    value: (row) => row.overduePrincipal,
  },
  {
    key: 'total',
    label: 'Total Exigible (Capital + Mora)',
    kind: 'money',
    cellClass: 'font-bold text-destructive',
    value: (row) => row.totalDue,
  },
]

export const analystProductivityColumns: TableColumn<AnalystRow>[] = [
  { key: 'name', label: 'Analista', value: (a) => a.fullName, cellClass: 'font-medium' },
  { key: 'decided', label: 'Dictaminados', value: (a) => a.decidedCount, cellClass: 'font-bold' },
  {
    key: 'approved',
    label: 'Aprobados',
    value: (a) => a.approvedCount,
    cellClass: 'text-success font-semibold',
  },
  {
    key: 'rejected',
    label: 'Rechazados',
    value: (a) => a.rejectedCount,
    cellClass: 'text-destructive',
  },
]

export const collectorProductivityColumns: TableColumn<CollectorRow>[] = [
  { key: 'name', label: 'Gestor', value: (c) => c.fullName, cellClass: 'font-medium' },
  {
    key: 'actions',
    label: 'Acciones Registradas',
    value: (c) => c.actionsCount,
    cellClass: 'font-bold text-primary',
  },
  {
    key: 'promises',
    label: 'Promesas Acordadas',
    value: (c) => c.promisesCount,
    cellClass: 'text-emerald-600 font-semibold',
  },
]

export const statementMovementColumns: TableColumn<StatementMovement>[] = [
  { key: 'date', label: 'Fecha y Hora', kind: 'datetime', value: (m) => m.date },
  { key: 'type', label: 'Operación' },
  {
    key: 'receiptNumber',
    label: 'Comprobante / Recibo',
    kind: 'mono',
    cellClass: 'text-[11px] text-muted-foreground',
    value: (m) => m.receiptNumber || 'REC-INICIAL',
  },
  {
    key: 'amount',
    label: 'Monto',
    kind: 'money',
    align: 'right',
    cellClass: 'font-bold',
  },
]

export interface MonthlyEvolutionRow {
  month: string
  disbursed: string
  collected: string
}

export const monthlyEvolutionColumns: TableColumn<MonthlyEvolutionRow>[] = [
  { key: 'month', label: 'Mes', cellClass: 'font-semibold text-foreground' },
  { key: 'disbursed', label: 'Desembolsado', kind: 'money', cellClass: 'text-primary' },
  {
    key: 'collected',
    label: 'Recaudado',
    kind: 'money',
    cellClass: 'text-emerald-600 font-semibold',
  },
  {
    key: 'net',
    label: 'Balance Neto',
    kind: 'money',
    align: 'right',
    cellClass: 'font-bold',
    value: (m) => (Number(m.collected) - Number(m.disbursed)).toFixed(2),
  },
]
