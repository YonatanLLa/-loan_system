import type { RouteRecordRaw } from 'vue-router'
import { CheckSquare } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

export const approvalsRoutes: RouteRecordRaw[] = [
  {
    path: 'aprobaciones',
    name: 'approvals',
    component: () => import('./pages/ApprovalsPage.vue'),
    meta: {
      title: 'Aprobaciones',
      permission: PERMISSIONS.approvalsRead,
      menu: { label: 'Aprobaciones', icon: CheckSquare, group: 'Control', order: 3 },
    },
  },
]
