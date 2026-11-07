import type { CollectionAction, Installment, Payment } from '@prestamos/api-client'
import type { TableColumn } from '@prestamos/ui'

export function installmentTotalPaid(inst: Installment): string {
  const sum =
    Number(inst.paidPrincipal || 0) +
    Number(inst.paidInterest || 0) +
    Number(inst.paidFee || 0) +
    Number(inst.paidLateFee || 0)
  return sum.toFixed(2)
}

export function installmentPendingBalance(inst: Installment): string {
  if (inst.status === 'PAID' || inst.status === 'CANCELLED_BY_PREPAYMENT') return '0.00'
  const totalWithMora = Number(inst.totalDue || 0) + Number(inst.lateFee || 0)
  const paid = Number(installmentTotalPaid(inst))
  return Math.max(0, totalWithMora - paid).toFixed(2)
}

export const scheduleColumns: TableColumn<Installment>[] = [
  { key: 'number', label: '#', kind: 'mono', cellClass: 'font-semibold' },
  { key: 'dueDate', label: 'Vencimiento', kind: 'date' },
  { key: 'principal', label: 'Capital', kind: 'money' },
  { key: 'interest', label: 'Interés', kind: 'money' },
  { key: 'fee', label: 'Cargos', kind: 'money', cellClass: 'text-muted-foreground' },
  {
    key: 'totalDue',
    label: 'Total Cuota',
    kind: 'money',
    cellClass: 'font-bold text-foreground',
  },
  {
    key: 'lateFee',
    label: 'Mora',
    kind: 'money',
    cellClass: (inst) =>
      Number(inst.lateFee) > 0 ? 'text-destructive font-bold' : 'text-muted-foreground',
  },
  {
    key: 'paid',
    label: 'Total Pagado',
    kind: 'money',
    value: (inst) => installmentTotalPaid(inst),
    cellClass: 'text-success font-semibold',
  },
  {
    key: 'pending',
    label: 'Saldo Pendiente',
    kind: 'money',
    value: (inst) => installmentPendingBalance(inst),
    cellClass: (inst) =>
      Number(installmentPendingBalance(inst)) > 0
        ? 'font-bold text-foreground'
        : 'font-bold text-muted-foreground',
  },
  { key: 'status', label: 'Estado', kind: 'status', statusKind: 'installment' },
  { key: 'action', label: 'Seguimiento / Cobro', align: 'right' },
]

export const paymentColumns: TableColumn<Payment>[] = [
  { key: 'id', label: 'ID Pago', kind: 'mono', cellClass: 'font-bold' },
  { key: 'paidAt', label: 'Fecha Efectiva', kind: 'datetime' },
  {
    key: 'amount',
    label: 'Monto Total',
    kind: 'money',
    cellClass: 'font-bold text-foreground',
  },
  { key: 'method', label: 'Medio de Pago', cellClass: 'font-medium' },
  {
    key: 'reference',
    label: 'Referencia / Recibo',
    kind: 'mono',
    value: (pay) => pay.receiptNumber || pay.externalReference || '—',
    cellClass: 'text-[11px] text-muted-foreground',
  },
  { key: 'allocations', label: 'Imputación a Cuotas' },
  { key: 'status', label: 'Estado', kind: 'status', statusKind: 'payment' },
]

export const collectionColumns: TableColumn<CollectionAction>[] = [
  { key: 'createdAt', label: 'Fecha / Hora', kind: 'datetime' },
  {
    key: 'type',
    label: 'Tipo de Contacto',
    value: (act) => act.actionType.replace(/_/g, ' '),
    cellClass: 'font-semibold',
  },
  { key: 'note', label: 'Detalle de la Gestión', cellClass: 'max-w-xs' },
  { key: 'promise', label: 'Compromiso / Promesa' },
  { key: 'status', label: 'Estado', kind: 'status', statusKind: 'collection' },
  {
    key: 'createdBy',
    label: 'Registrado por',
    cellClass: 'text-muted-foreground',
  },
]
