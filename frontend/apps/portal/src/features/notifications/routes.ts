import type { RouteRecordRaw } from 'vue-router'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const notificationsRoutes: RouteRecordRaw[] = [
  {
    path: 'notificaciones',
    name: 'notifications',
    component: placeholder,
    props: {
      title: 'Notificaciones',
      description: 'Avisos de vencimientos, pagos confirmados y desembolsos, y tus preferencias de canal.',
      requirements: 'FE-CP-20',
    },
    meta: {
      title: 'Notificaciones',
    },
  },
]
