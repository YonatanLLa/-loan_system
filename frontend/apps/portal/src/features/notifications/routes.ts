import type { RouteRecordRaw } from 'vue-router'

const NotificationsPage = () => import('./pages/NotificationsPage.vue')

export const notificationsRoutes: RouteRecordRaw[] = [
  {
    path: 'notificaciones',
    name: 'notifications',
    component: NotificationsPage,
    meta: {
      title: 'Notificaciones',
    },
  },
]
