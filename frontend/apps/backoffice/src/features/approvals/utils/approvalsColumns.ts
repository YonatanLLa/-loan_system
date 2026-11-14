import type { ApprovalRequestSummary } from '@prestamos/api-client'
import type { TableColumn } from '@prestamos/ui'

export const APPROVAL_OPERATION_LABELS: Record<string, string> = {
  PAYMENT_REVERSAL: 'Reversa de pago',
  LOAN_WRITE_OFF: 'Castigo de préstamo',
  MANUAL_ADJUSTMENT: 'Ajuste manual',
  PRODUCT_RATE_CHANGE: 'Cambio de tasas de producto',
  LOAN_RESTRUCTURE: 'Reestructuración de préstamo',
  USER_ROLE_CHANGE: 'Cambio de rol de usuario',
}

export function getOperationLabel(operationType: string): string {
  return APPROVAL_OPERATION_LABELS[operationType] ?? operationType
}

export const approvalColumns: TableColumn<ApprovalRequestSummary>[] = [
  { key: 'operationType', label: 'Operación' },
  { key: 'target', label: 'Objetivo' },
  { key: 'status', label: 'Estado' },
  { key: 'requestedBy', label: 'Solicitante' },
  { key: 'requestedAt', label: 'Solicitada', kind: 'datetime', cellClass: 'text-xs' },
  { key: 'expiresAt', label: 'Expira', kind: 'datetime', cellClass: 'text-xs' },
  { key: 'actions', label: 'Detalle', align: 'right' },
]
