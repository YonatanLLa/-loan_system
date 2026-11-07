import { ref, computed, onMounted, getCurrentInstance } from 'vue'
import {
  toApiError,
  type ClientNotification,
  type NotificationPreferences,
} from '@prestamos/api-client'

import { notificationsService } from '../services/notifications.service'

export function useNotifications() {
  const notifications = ref<ClientNotification[]>([])
  const preferences = ref<NotificationPreferences>({
    emailEnabled: true,
    whatsappEnabled: true,
    smsEnabled: false,
    notifyDaysBeforeDue: 3,
  })

  const isLoading = ref(false)
  const isSavingPreferences = ref(false)
  const errorMessage = ref<string | null>(null)
  const successMessage = ref<string | null>(null)

  const unreadCount = computed(
    () => notifications.value.filter((n) => !n.read).length,
  )

  const loadNotifications = async () => {
    isLoading.value = true
    errorMessage.value = null
    try {
      const [notifs, prefs] = await Promise.all([
        notificationsService.fetchNotifications(),
        notificationsService.fetchPreferences(),
      ])
      notifications.value = notifs
      preferences.value = prefs
    } catch (err: unknown) {
      const apiErr = toApiError(err)
      errorMessage.value =
        apiErr.detail || 'No pudimos cargar tus notificaciones.'
    } finally {
      isLoading.value = false
    }
  }

  const markAsRead = async (id: string) => {
    try {
      await notificationsService.markAsRead(id)
      const target = notifications.value.find((n) => n.id === id)
      if (target) target.read = true
    } catch {
      // Ignorar fallo puntual
    }
  }

  const markAllAsRead = async () => {
    try {
      await notificationsService.markAllAsRead()
      for (const n of notifications.value) {
        n.read = true
      }
    } catch {
      // Ignorar fallo puntual
    }
  }

  const savePreferences = async () => {
    isSavingPreferences.value = true
    errorMessage.value = null
    successMessage.value = null
    try {
      preferences.value = await notificationsService.updatePreferences(preferences.value)
      successMessage.value = 'Preferencias de notificación guardadas con éxito.'
    } catch (err: unknown) {
      const apiErr = toApiError(err)
      errorMessage.value =
        apiErr.detail || 'No se pudieron guardar tus preferencias.'
    } finally {
      isSavingPreferences.value = false
    }
  }

  if (getCurrentInstance()) {
    onMounted(() => {
      loadNotifications()
    })
  }

  return {
    notifications,
    preferences,
    unreadCount,
    isLoading,
    isSavingPreferences,
    errorMessage,
    successMessage,
    loadNotifications,
    markAsRead,
    markAllAsRead,
    savePreferences,
  }
}
