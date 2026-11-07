import type { AgingBucket, AgingRow, ReceivableItem } from '@prestamos/api-client'
import type { TableColumn } from '@prestamos/ui'

export const BUCKET_LABEL: Record<AgingBucket, string> = {
  PREVENTIVO: 'Preventivo',
  TRAMO_1: '1–30 días',
  TRAMO_2: '31–60 días',
  TRAMO_3: '61–90 días',
  TRAMO_4: 'Más de 90 días',
}

export const agingColumns: TableColumn<AgingRow>[] = [
  { key: 'customer', label: 'Cliente' },
  { key: 'bucket', label: 'Tramo' },
  {
    key: 'daysLate',
    label: 'Atraso',
    value: (row) => `${row.daysLate} días`,
    cellClass: 'font-medium',
  },
  { key: 'overdueInstallments', label: 'Cuotas vencidas' },
  { key: 'overduePrincipal', label: 'Capital impago', kind: 'money' },
  { key: 'totalDue', label: 'Total exigible', kind: 'money', cellClass: 'font-semibold' },
  {
    key: 'assignedTo',
    label: 'Gestor',
    value: (row) => row.assignedTo || 'Sin asignar',
  },
  { key: 'actions', label: 'Acciones', align: 'right' },
]

export const receivableColumns: TableColumn<ReceivableItem>[] = [
  { key: 'customer', label: 'Cliente' },
  {
    key: 'installment',
    label: 'Cuota',
    value: (item) => `N.º ${item.installmentNumber}`,
  },
  { key: 'dueDate', label: 'Vencimiento', kind: 'date' },
  {
    key: 'daysLate',
    label: 'Atraso',
    value: (item) => (item.daysLate > 0 ? `${item.daysLate} días` : '—'),
  },
  { key: 'amount', label: 'Saldo de la cuota', kind: 'money', cellClass: 'font-semibold' },
  {
    key: 'outstandingPrincipal',
    label: 'Capital pendiente del préstamo',
    kind: 'money',
  },
  { key: 'status', label: 'Estado', kind: 'status', statusKind: 'receivable' },
]
