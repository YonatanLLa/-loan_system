import { describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { notificationsService } from '../features/notifications/services'
import { useNotifications } from '../features/notifications/composables'

describe('Notifications Service (FE-CP-20)', () => {
  it('expone operaciones de consulta, lectura y preferencias', () => {
    expect(typeof notificationsService.fetchNotifications).toBe('function')
    expect(typeof notificationsService.markAsRead).toBe('function')
    expect(typeof notificationsService.markAllAsRead).toBe('function')
    expect(typeof notificationsService.fetchPreferences).toBe('function')
    expect(typeof notificationsService.updatePreferences).toBe('function')
  })
})

describe('useNotifications Composable (FE-CP-20)', () => {
  it('inicializa estados y calcula contador de notificaciones no leídas', () => {
    setActivePinia(createPinia())
    const notifs = useNotifications()

    expect(notifs.notifications.value).toEqual([])
    expect(notifs.unreadCount.value).toBe(0)

    notifs.notifications.value = [
      {
        id: 'notif-1',
        customerId: 'cus-001',
        title: 'Desembolso',
        message: 'Tu crédito ha sido desembolsado.',
        category: 'DISBURSEMENT',
        read: true,
        createdAt: '2026-11-01T10:00:00Z',
      },
      {
        id: 'notif-2',
        customerId: 'cus-001',
        title: 'Próxima Cuota',
        message: 'Tu cuota vence pronto.',
        category: 'LOAN',
        read: false,
        createdAt: '2026-11-05T08:00:00Z',
      },
    ]

    expect(notifs.unreadCount.value).toBe(1)
  })
})
