import type { IntegrationEvent } from '@prestamos/api-client'
import type { TableColumn } from '@prestamos/ui'

export const integrationEventColumns: TableColumn<IntegrationEvent>[] = [
  { key: 'id', label: 'ID Evento', kind: 'mono', cellClass: 'font-bold' },
  { key: 'provider', label: 'Proveedor', cellClass: 'font-bold uppercase text-foreground' },
  { key: 'eventType', label: 'Tipo de Evento', kind: 'mono', cellClass: 'text-muted-foreground' },
  {
    key: 'externalId',
    label: 'External ID',
    kind: 'mono',
    cellClass: 'text-foreground font-semibold',
  },
  {
    key: 'retryCount',
    label: 'Reintentos',
    kind: 'mono',
    cellClass: (ev) => (ev.retryCount > 0 ? 'text-warning font-bold' : 'text-muted-foreground'),
    value: (ev) => `${ev.retryCount} veces`,
  },
  { key: 'createdAt', label: 'Fecha', kind: 'datetime', cellClass: 'text-muted-foreground' },
  { key: 'error', label: 'Error / Causa en DLQ', cellClass: 'max-w-xs' },
  { key: 'status', label: 'Estado' },
  { key: 'action', label: 'Acción', align: 'right' },
]
