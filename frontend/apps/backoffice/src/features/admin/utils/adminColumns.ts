import type { AdminUserDto, Holiday, PaymentMethodConfig } from '@prestamos/api-client'
import type { TableColumn } from '@prestamos/ui'

export const paymentMethodColumns: TableColumn<PaymentMethodConfig>[] = [
  { key: 'name', label: 'Canal de Recaudación', cellClass: 'font-semibold text-foreground' },
  { key: 'type', label: 'Tipo de Método' },
  { key: 'webhook', label: 'Notificación Webhook' },
  { key: 'status', label: 'Estado' },
  { key: 'actions', label: 'Acción' },
]

export const holidayColumns: TableColumn<Holiday>[] = [
  { key: 'date', label: 'Fecha', kind: 'date', cellClass: 'font-mono font-semibold' },
  { key: 'name', label: 'Festividad', cellClass: 'font-medium text-foreground' },
  { key: 'recurrence', label: 'Frecuencia' },
  { key: 'actions', label: 'Acciones' },
]

export const adminUserColumns: TableColumn<AdminUserDto>[] = [
  {
    key: 'name',
    label: 'Nombre Completo',
    value: (u) => u.fullName,
    cellClass: 'font-semibold text-foreground',
  },
  { key: 'email', label: 'Correo Corporativo', kind: 'mono', cellClass: 'text-muted-foreground' },
  { key: 'role', label: 'Rol Asignado' },
  { key: 'permissions', label: 'Permisos' },
  {
    key: 'created',
    label: 'Fecha de Registro',
    kind: 'datetime',
    value: (u) => u.createdAt,
    cellClass: 'text-muted-foreground',
  },
]
