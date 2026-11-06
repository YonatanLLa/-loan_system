import type { RouteRecordRaw } from 'vue-router'
import { PhoneCall } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

export const collectionsRoutes: RouteRecordRaw[] = [
  {
    path: 'cobranza',
    name: 'collections',
    component: () => import('./pages/CollectionsPage.vue'),
    meta: {
      title: 'Cobranza',
      permission: PERMISSIONS.collections,
      menu: { label: 'Cobranza', icon: PhoneCall, group: 'Cobranza', order: 1 },
    },
  },
]
