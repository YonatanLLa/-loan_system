import type { ApplicationSummary, Simulation } from '@prestamos/api-client'
import type { TableColumn } from '@prestamos/ui'

export type SimulatedInstallment = Simulation['installments'][number]

export const simulationInstallmentColumns: TableColumn<SimulatedInstallment>[] = [
  { key: 'number', label: '#', kind: 'mono' },
  { key: 'dueDate', label: 'Vencimiento', kind: 'date' },
  { key: 'principal', label: 'Capital', kind: 'money' },
  { key: 'interest', label: 'Interés', kind: 'money' },
  { key: 'totalDue', label: 'Cuota', kind: 'money', cellClass: 'font-bold text-foreground' },
  { key: 'balance', label: 'Saldo', kind: 'money', cellClass: 'text-muted-foreground' },
]

export const applicationColumns: TableColumn<ApplicationSummary>[] = [
  { key: 'id', label: 'ID', kind: 'mono', cellClass: 'text-xs font-bold' },
  {
    key: 'createdAt',
    label: 'Fecha y Hora',
    kind: 'datetime',
    cellClass: 'text-xs text-muted-foreground',
  },
  { key: 'customer', label: 'Cliente', cellClass: 'font-medium text-foreground' },
  { key: 'productName', label: 'Producto', cellClass: 'text-xs font-medium' },
  { key: 'amount', label: 'Monto / Plazo', cellClass: 'text-xs font-mono' },
  { key: 'dti', label: 'DTI Motor', cellClass: 'text-xs font-mono font-semibold' },
  { key: 'recommendation', label: 'Recomendación' },
  { key: 'status', label: 'Estado', kind: 'status', statusKind: 'application' },
  { key: 'action', label: 'Acciones', align: 'right' },
]
