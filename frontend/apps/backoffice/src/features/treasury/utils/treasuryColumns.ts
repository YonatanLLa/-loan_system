import type {
  BankStatementLine,
  Disbursement,
  InternalAccountTransfer,
} from '@prestamos/api-client'
import type { TableColumn } from '@prestamos/ui'

export const internalTransferColumns: TableColumn<InternalAccountTransfer>[] = [
  { key: 'id', label: 'ID Fondeo', kind: 'mono', cellClass: 'font-bold' },
  { key: 'transferDate', label: 'Fecha Valor', kind: 'date' },
  {
    key: 'source',
    label: 'Cuenta Origen (Salida)',
    value: (trf) => `- ${trf.sourceBankName}`,
    cellClass: 'font-medium text-destructive',
  },
  {
    key: 'destination',
    label: 'Cuenta Destino (Entrada)',
    value: (trf) => `+ ${trf.destinationBankName}`,
    cellClass: 'font-medium text-success',
  },
  { key: 'amount', label: 'Monto', kind: 'money', cellClass: 'font-bold text-foreground' },
  {
    key: 'operationNumber',
    label: 'Nº Operación',
    kind: 'mono',
    value: (trf) => trf.bankOperationNumber,
  },
  { key: 'notes', label: 'Glosa / Registrado por' },
]

export const disbursementColumns: TableColumn<Disbursement>[] = [
  { key: 'id', label: 'ID Orden', kind: 'mono', cellClass: 'font-bold' },
  {
    key: 'initiatedAt',
    label: 'Iniciado',
    kind: 'datetime',
    cellClass: 'text-muted-foreground',
  },
  { key: 'recipient', label: 'Destinatario', cellClass: 'font-medium text-foreground' },
  { key: 'amount', label: 'Importe', kind: 'money', cellClass: 'font-bold text-foreground' },
  { key: 'channel', label: 'Canal', cellClass: 'font-medium' },
  { key: 'status', label: 'Estado Bancario', kind: 'status', statusKind: 'disbursement' },
  { key: 'error', label: 'Detalle / Error Banco' },
  { key: 'action', label: 'Acción', align: 'right' },
]

export const statementColumns: TableColumn<BankStatementLine>[] = [
  { key: 'operationNumber', label: 'Nº Operación', kind: 'mono', cellClass: 'font-bold' },
  { key: 'valueDate', label: 'Fecha Valor', kind: 'date' },
  {
    key: 'amount',
    label: 'Importe Abono / Cargo',
    kind: 'money',
    cellClass: (s) =>
      Number(s.amount) > 0 ? 'font-bold text-success' : 'font-bold text-destructive',
  },
  {
    key: 'description',
    label: 'Descripción en Extracto',
    cellClass: 'text-foreground font-medium max-w-xs truncate',
  },
  { key: 'status', label: 'Estado de Conciliación' },
  { key: 'action', label: 'Acción', align: 'right' },
]
