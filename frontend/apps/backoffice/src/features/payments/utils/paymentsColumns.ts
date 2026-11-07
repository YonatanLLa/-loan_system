import type { Payment } from '@prestamos/api-client'
import type { TableColumn } from '@prestamos/ui'

export const pendingPaymentColumns: TableColumn<Payment>[] = [
  { key: 'id', label: 'ID Pago', kind: 'mono', cellClass: 'font-bold' },
  { key: 'borrower', label: 'Prestatario', cellClass: 'font-medium text-foreground' },
  {
    key: 'amount',
    label: 'Monto Reportado',
    kind: 'money',
    cellClass: 'text-sm font-bold text-foreground',
  },
  { key: 'channel', label: 'Canal / Referencia' },
  { key: 'suggestion', label: 'Movimiento de Extracto Sugerido para Emparejar (FE-BO-21)' },
  { key: 'status', label: 'Estado', kind: 'status', statusKind: 'payment' },
  { key: 'action', label: 'Acción', align: 'right' },
]

export const basePaymentColumns: TableColumn<Payment>[] = [
  { key: 'id', label: 'ID Pago', kind: 'mono', cellClass: 'font-bold' },
  { key: 'paidAt', label: 'Fecha y Hora', kind: 'datetime', cellClass: 'text-muted-foreground' },
  { key: 'borrower', label: 'Prestatario', cellClass: 'font-medium text-foreground' },
  { key: 'amount', label: 'Monto', kind: 'money', cellClass: 'font-bold text-foreground' },
  { key: 'method', label: 'Canal', cellClass: 'font-medium' },
  {
    key: 'reference',
    label: 'Referencia / Recibo',
    kind: 'mono',
    cellClass: 'text-[11px] text-muted-foreground',
    value: (pay) => pay.receiptNumber || pay.externalReference || '—',
  },
  { key: 'status', label: 'Estado', kind: 'status', statusKind: 'payment' },
]

export const paymentColumnsWithReverse: TableColumn<Payment>[] = [
  ...basePaymentColumns,
  { key: 'reverse', label: 'Reverso', align: 'right' },
]
