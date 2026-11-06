import type {
  AgingBucket,
  AgingRow,
  CollectionAction,
  LoanProduct,
  Page,
  ReceivableItem,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

export interface CollectorOption {
  id: string
  fullName: string
  email: string
  role: string
}

export interface AgingQueryParams {
  page?: number
  pageSize?: number
  bucket?: AgingBucket | ''
  assignedTo?: string
  productName?: string
  minAmount?: string
  maxAmount?: string
  q?: string
}

export interface ReceivableQueryParams {
  page?: number
  pageSize?: number
  type?: '' | 'VENCIDA' | 'PENDIENTE'
  q?: string
}

export interface CreateCollectionActionPayload {
  loanId: string
  actionType: CollectionAction['actionType']
  note: string
  nextActionAt?: string
  promiseAmount?: string
  promiseDate?: string
}

export interface WriteOffPayload {
  reason: string
  minuteRef: string
  approvedBy: string
}

/**
 * Servicio de Cobranzas (FE-BO-32 a FE-BO-36).
 * Encapsula todas las peticiones a la API .NET / backend simulado (MSW).
 */
export const collectionsService = {
  /** Obtiene la lista paginada de créditos clasificados por tramos de aging (FE-BO-32). */
  async fetchAging(params: AgingQueryParams = {}): Promise<Page<AgingRow>> {
    const queryParams: Record<string, string | number> = {}
    if (params.page != null) queryParams.page = params.page
    if (params.pageSize != null) queryParams.pageSize = params.pageSize
    if (params.bucket) queryParams.bucket = params.bucket
    if (params.assignedTo) queryParams.assignedTo = params.assignedTo
    if (params.productName) queryParams.productName = params.productName
    if (params.minAmount?.trim()) queryParams.minAmount = params.minAmount.trim()
    if (params.maxAmount?.trim()) queryParams.maxAmount = params.maxAmount.trim()
    if (params.q?.trim()) queryParams.q = params.q.trim()

    const { data } = await useApi().get<Page<AgingRow>>('/collections/aging', {
      params: queryParams,
    })
    return data
  },

  /** Obtiene el conteo de créditos por tramo de aging. */
  async fetchAgingBucketCounts(): Promise<Record<string, number>> {
    const { data } = await useApi().get<Page<AgingRow>>('/collections/aging', {
      params: { page: 1, pageSize: 100 },
    })
    const counts: Record<string, number> = { '': data.total }
    for (const row of data.items) {
      counts[row.bucket] = (counts[row.bucket] ?? 0) + 1
    }
    return counts
  },

  /** Obtiene la lista de gestores y personal habilitado para asignación y aprobación (FE-BO-34). */
  async fetchCollectors(): Promise<CollectorOption[]> {
    const { data } = await useApi().get<CollectorOption[]>('/collections/collectors')
    return data
  },

  /** Obtiene los productos activos para los filtros de cartera. */
  async fetchActiveProducts(): Promise<LoanProduct[]> {
    const { data } = await useApi().get<LoanProduct[]>('/loan-products', {
      params: { status: 'ACTIVO' },
    })
    return data
  },

  /** Asigna un préstamo a un gestor de cobranza (FE-BO-34). */
  async assignCollector(
    loanId: string,
    collectorName: string,
  ): Promise<{ success: boolean; assignedTo: string }> {
    const { data } = await useApi().post<{ success: boolean; assignedTo: string }>(
      `/loans/${loanId}/assign-collector`,
      { collectorName },
    )
    return data
  },

  /** Obtiene las cuotas por cobrar vencidas y por vencer (FE-BO-36). */
  async fetchReceivables(params: ReceivableQueryParams = {}): Promise<Page<ReceivableItem>> {
    const queryParams: Record<string, string | number> = {}
    if (params.page != null) queryParams.page = params.page
    if (params.pageSize != null) queryParams.pageSize = params.pageSize
    if (params.type) queryParams.type = params.type
    if (params.q?.trim()) queryParams.q = params.q.trim()

    const { data } = await useApi().get<Page<ReceivableItem>>('/collections/receivables', {
      params: queryParams,
    })
    return data
  },

  /** Obtiene el historial / bitácora de gestiones de un crédito (FE-BO-33). */
  async fetchCollectionActions(loanId: string): Promise<CollectionAction[]> {
    const { data } = await useApi().get<CollectionAction[]>(
      `/loans/${loanId}/collection-actions`,
    )
    return data
  },

  /** Registra un nuevo contacto, llamada o promesa de pago (FE-BO-33). */
  async createCollectionAction(payload: CreateCollectionActionPayload): Promise<CollectionAction> {
    const body = {
      loanId: payload.loanId,
      actionType: payload.actionType,
      note: payload.note.trim(),
      nextActionAt: payload.nextActionAt ? new Date(payload.nextActionAt).toISOString() : undefined,
      promiseAmount: payload.promiseAmount?.trim() || undefined,
      promiseDate: payload.promiseDate || undefined,
    }
    const { data } = await useApi().post<CollectionAction>('/collections/actions', body)
    return data
  },

  /** Registra el castigo contable de deuda irrecuperable con 4 ojos (FE-BO-35). */
  async writeOffLoan(loanId: string, payload: WriteOffPayload): Promise<void> {
    await useApi().post(`/loans/${loanId}/write-off`, {
      reason: payload.reason.trim(),
      minuteRef: payload.minuteRef.trim(),
      approvedBy: payload.approvedBy,
    })
  },
}
