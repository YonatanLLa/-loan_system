/**
 * Catálogo móvil unificado de estados -> etiqueta, tono y colores nativos.
 * Espeja frontend/packages/ui/src/status.ts (ARQUITECTURA_FRONTEND §8, ARQUITECTURA_MOBILE §7).
 */

export type StatusTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral';

export interface StatusInfo {
  label: string;
  tone: StatusTone;
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
} as const satisfies Record<string, Record<string, StatusInfo>>;

export type StatusKind = keyof typeof statusCatalog;

export function getStatusInfo(kind: StatusKind, status: string): StatusInfo {
  const catalog: Record<string, StatusInfo> = statusCatalog[kind];
  return catalog[status] ?? { label: status, tone: 'neutral' };
}

/** Colores para badges y chips en React Native */
export const statusToneColors: Record<
  StatusTone,
  { background: string; border: string; text: string }
> = {
  success: {
    background: '#ECFDF5',
    border: '#A7F3D0',
    text: '#065F46',
  },
  warning: {
    background: '#FFFBEB',
    border: '#FDE68A',
    text: '#92400E',
  },
  danger: {
    background: '#FEF2F2',
    border: '#FECACA',
    text: '#991B1B',
  },
  info: {
    background: '#EFF6FF',
    border: '#BFDBFE',
    text: '#1E40AF',
  },
  neutral: {
    background: '#F3F4F6',
    border: '#E5E7EB',
    text: '#374151',
  },
};
