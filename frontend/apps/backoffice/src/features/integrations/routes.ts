import type { RouteRecordRaw } from 'vue-router'
import { Plug } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const integrationsRoutes: RouteRecordRaw[] = [
  {
    path: 'integraciones',
    name: 'integrations',
    component: placeholder,
    props: {
      title: 'Integraciones',
      description: 'Eventos de bancos y pasarelas con error (DLQ) y reintento manual.',
      requirements: 'FE-BO-31',
    },
    meta: {
      title: 'Integraciones',
      permission: PERMISSIONS.integrations,
      menu: { label: 'Integraciones', icon: Plug, group: 'Tesorería', order: 2 },
    },
  },
]
