import { PERMISSIONS } from '@/app/permissions'

/** Etiquetas en español para los roles definidos en `RolePermissions.cs`. */
export const SECURITY_ROLE_LABELS: Record<string, string> = {
  ROLE_ADMIN: 'Administrador',
  ROLE_ANALYST: 'Analista de crédito',
  ROLE_OPERATOR: 'Operador financiero',
  ROLE_COLLECTOR: 'Gestor de cobranzas',
  ROLE_AUDITOR: 'Auditor interno',
  ROLE_CLIENT: 'Cliente (Portal)',
}

/** Etiquetas amigables para los permisos efectivos emitidos por `GET /api/me`. */
export const PERMISSION_LABELS: Record<string, string> = {
  [PERMISSIONS.dashboard]: 'Ver panel principal (Dashboard)',
  [PERMISSIONS.customers]: 'Consultar clientes y vista 360',
  [PERMISSIONS.applications]: 'Consultar solicitudes de crédito',
  [PERMISSIONS.loans]: 'Consultar préstamos y cronogramas',
  [PERMISSIONS.payments]: 'Consultar pagos registrados',
  [PERMISSIONS.paymentsReverse]: 'Solicitar reverso de pagos',
  [PERMISSIONS.treasury]: 'Consultar tesorería y conciliación',
  [PERMISSIONS.integrations]: 'Consultar eventos de integraciones',
  [PERMISSIONS.collections]: 'Consultar cartera en cobranza',
  [PERMISSIONS.reports]: 'Consultar reportes y flujo de caja',
  [PERMISSIONS.audit]: 'Consultar bitácora de auditoría',
  [PERMISSIONS.products]: 'Gestionar productos de crédito',
  [PERMISSIONS.admin]: 'Administrar seguridad y usuarios',
  [PERMISSIONS.approvalsRead]: 'Consultar solicitudes de aprobación',
  [PERMISSIONS.approvalsPaymentsReverse]: 'Aprobar reversos de pagos (cuatro ojos)',
  [PERMISSIONS.approvalsLoansWriteOff]: 'Aprobar castigos de préstamos (cuatro ojos)',
  [PERMISSIONS.approvalsLoansAdjust]: 'Aprobar ajustes manuales de préstamos (cuatro ojos)',
  [PERMISSIONS.approvalsProductsRates]: 'Aprobar cambios de tasas (cuatro ojos)',
  [PERMISSIONS.approvalsLoansRestructure]: 'Aprobar reestructuraciones (cuatro ojos)',
  [PERMISSIONS.approvalsUsersRole]: 'Aprobar cambios de rol (cuatro ojos)',
  'client.portal': 'Acceso al Portal de Clientes',
}

/** Devuelve la etiqueta legible de un rol RBAC, o la clave cruda si es desconocida. */
export function getRoleLabel(role?: string | null): string {
  if (!role) return 'Sin rol asignado'
  return SECURITY_ROLE_LABELS[role] ?? role
}

/** Devuelve la descripción amigable de un permiso, o la clave cruda como respaldo. */
export function getPermissionLabel(permission: string): string {
  return PERMISSION_LABELS[permission] ?? permission
}
