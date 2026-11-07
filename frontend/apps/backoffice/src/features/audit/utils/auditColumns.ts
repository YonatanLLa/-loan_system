import type { AuditLogEntry } from '@prestamos/api-client'
import type { TableColumn } from '@prestamos/ui'

export const auditLogColumns: TableColumn<AuditLogEntry>[] = [
  {
    key: 'id',
    label: 'ID',
    kind: 'mono',
    cellClass: 'text-xs text-muted-foreground',
    value: (entry) => `#${entry.id}`,
  },
  { key: 'createdAt', label: 'Fecha y Hora', kind: 'datetime', cellClass: 'text-xs' },
  { key: 'user', label: 'Usuario / IP' },
  { key: 'entity', label: 'Entidad', cellClass: 'font-medium' },
  { key: 'entityId', label: 'ID Entidad', kind: 'mono', cellClass: 'text-xs text-muted-foreground' },
  { key: 'action', label: 'Acción' },
  { key: 'compare', label: 'Comparar', align: 'right' },
]
