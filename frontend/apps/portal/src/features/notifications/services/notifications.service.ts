import type {
  ClientNotification,
  NotificationPreferences,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

/**
 * Servicio de Notificaciones y Preferencias de Canales (FE-CP-20).
 */
export const notificationsService = {
  /**
   * Obtiene la lista de notificaciones del cliente ordenadas por fecha reciente.
   */
  async fetchNotifications(): Promise<ClientNotification[]> {
    const { data } = await useApi().get<ClientNotification[]>('/client/notifications')
    return data
  },

  /**
   * Marca una notificación específica como leída.
   */
  async markAsRead(id: string): Promise<ClientNotification> {
    const { data } = await useApi().patch<ClientNotification>(
      `/client/notifications/${encodeURIComponent(id)}/read`,
    )
    return data
  },

  /**
   * Marca todas las notificaciones del cliente como leídas.
   */
  async markAllAsRead(): Promise<{ success: boolean }> {
    const { data } = await useApi().post<{ success: boolean }>(
      '/client/notifications/mark-all-read',
    )
    return data
  },

  /**
   * Obtiene las preferencias de canales de notificación (Email, WhatsApp, SMS).
   */
  async fetchPreferences(): Promise<NotificationPreferences> {
    const { data } = await useApi().get<NotificationPreferences>(
      '/client/notification-preferences',
    )
    return data
  },

  /**
   * Actualiza las preferencias de canales de notificación del cliente.
   */
  async updatePreferences(prefs: NotificationPreferences): Promise<NotificationPreferences> {
    const { data } = await useApi().put<NotificationPreferences>(
      '/client/notification-preferences',
      prefs,
    )
    return data
  },
}
