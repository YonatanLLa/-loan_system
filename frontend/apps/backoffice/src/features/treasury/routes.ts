import type { RouteRecordRaw } from 'vue-router'
import { Landmark } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const treasuryRoutes: RouteRecordRaw[] = [
  {
    path: 'tesoreria',
    name: 'treasury',
    component: () => import('./pages/TreasuryDashboardPage.vue'),
    meta: {
      title: 'Tesorería',
      permission: PERMISSIONS.treasury,
      menu: { label: 'Tesorería', icon: Landmark, group: 'Tesorería', order: 1 },
    },
  },
]
