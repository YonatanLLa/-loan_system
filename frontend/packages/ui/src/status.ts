/**
 * Mapa único de estados → etiqueta y tono (ARQUITECTURA_FRONTEND §8, RF-28.3).
 * Los códigos son los ENUM de la base de datos (database/migrations/00_extensions_and_enums.sql).
 */

export type StatusTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral'

export interface StatusInfo {
  label: string
  tone: StatusTone
}

export const statusCatalog = {
  installment: {
    PENDING: { label: 'Pendiente', tone: 'warning' },
    PARTIAL: { label: 'Pago parcial', tone: 'info' },
    PAID: { label: 'Pagada', tone: 'success' },
    OVERDUE: { label: 'Vencida', tone: 'danger' },
    CANCELLED_BY_PREPAYMENT: { label: 'Cancelada por prepago', tone: 'neutral' },
  },
  loan: {
    DESEMBOLSADO: { label: 'Desembolsado', tone: 'info' },
    VIGENTE: { label: 'Vigente', tone: 'success' },
    EN_MORA: { label: 'En mora', tone: 'danger' },
    PAGADO: { label: 'Pagado', tone: 'neutral' },
    CASTIGADO: { label: 'Castigado', tone: 'danger' },
    CANCELADO: { label: 'Cancelado', tone: 'neutral' },
  },
  payment: {
    PENDING_CONFIRMATION: { label: 'En validación', tone: 'neutral' },
    APPLIED: { label: 'Aplicado', tone: 'success' },
    REVERSED: { label: 'Revertido', tone: 'danger' },
  },
  disbursement: {
    PENDING: { label: 'Pendiente', tone: 'neutral' },
    PROCESSING: { label: 'En proceso', tone: 'info' },
    COMPLETED: { label: 'Completado', tone: 'success' },
    FAILED: { label: 'Fallido', tone: 'danger' },
    REVERSED: { label: 'Revertido', tone: 'danger' },
  },
  application: {
    SOLICITADO: { label: 'Solicitado', tone: 'neutral' },
    EN_EVALUACION: { label: 'En evaluación', tone: 'info' },
    APROBADO: { label: 'Aprobado', tone: 'success' },
    APROBADO_CON_CONDICIONES: { label: 'Aprobado con condiciones', tone: 'warning' },
    RECHAZADO: { label: 'Rechazado', tone: 'danger' },
    ANULADO: { label: 'Anulado', tone: 'neutral' },
    DESEMBOLSADO: { label: 'Desembolsado', tone: 'success' },
  },
  kyc: {
    PENDING: { label: 'Pendiente', tone: 'warning' },
    VERIFIED: { label: 'Verificado', tone: 'success' },
    REJECTED: { label: 'Rechazado', tone: 'danger' },
    EXPIRED: { label: 'Vencido', tone: 'danger' },
  },
  collection: {
    ACTIVA: { label: 'Activa', tone: 'info' },
    CUMPLIDA: { label: 'Cumplida', tone: 'success' },
    PROMESA_INCUMPLIDA: { label: 'Promesa incumplida', tone: 'danger' },
    CERRADA: { label: 'Cerrada', tone: 'neutral' },
  },
  receivable: {
    PENDIENTE: { label: 'Por vencer', tone: 'warning' },
    VENCIDA: { label: 'Vencida', tone: 'danger' },
  },
  approval: {
    PENDING: { label: 'Pendiente', tone: 'warning' },
    APPROVED: { label: 'Aprobada', tone: 'success' },
    REJECTED: { label: 'Rechazada', tone: 'danger' },
    CANCELLED: { label: 'Cancelada', tone: 'neutral' },
    EXPIRED: { label: 'Expirada', tone: 'neutral' },
  },
} as const satisfies Record<string, Record<string, StatusInfo>>

export type StatusKind = keyof typeof statusCatalog

export function getStatusInfo(kind: StatusKind, status: string): StatusInfo {
  const catalog: Record<string, StatusInfo> = statusCatalog[kind]
  return catalog[status] ?? { label: status, tone: 'neutral' }
}

/** Clases de Tailwind por tono (usan los tokens --success, --warning, --info del tema). */
export const toneClasses: Record<StatusTone, string> = {
  success: 'border-success/30 bg-success/10 text-success',
  warning: 'border-warning/30 bg-warning/10 text-warning',
  danger: 'border-destructive/30 bg-destructive/10 text-destructive',
  info: 'border-info/30 bg-info/10 text-info',
  neutral: 'border-border bg-muted text-muted-foreground',
}
