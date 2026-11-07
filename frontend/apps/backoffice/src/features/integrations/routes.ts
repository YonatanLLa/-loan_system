import type { RouteRecordRaw } from 'vue-router'
import { Plug } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

export const integrationsRoutes: RouteRecordRaw[] = [
  {
    path: 'integraciones',
    name: 'integrations',
    component: () => import('./pages/IntegrationsEventsPage.vue'),
    meta: {
      title: 'Integraciones',
      permission: PERMISSIONS.integrations,
      menu: { label: 'Integraciones', icon: Plug, group: 'Tesorería', order: 2 },
    },
  },
]
