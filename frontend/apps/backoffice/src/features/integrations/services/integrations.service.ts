import type { IntegrationEvent, Page } from '@prestamos/api-client'
import { useApi } from '@/app/api'

export interface IntegrationEventsQuery {
  page: number
  pageSize: number
  status?: string
}

export const integrationsService = {
  async getEvents(query: IntegrationEventsQuery): Promise<Page<IntegrationEvent>> {
    const params: Record<string, string | number> = {
      page: query.page,
      pageSize: query.pageSize,
    }
    if (query.status) params.status = query.status

    const { data } = await useApi().get<Page<IntegrationEvent>>('/integrations/events', { params })
    return data
  },

  async retryEvent(eventId: string): Promise<IntegrationEvent> {
    const { data } = await useApi().post<IntegrationEvent>(
      `/integrations/events/${eventId}/retry`,
      {},
    )
    return data
  },
}
