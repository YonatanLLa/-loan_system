import type { RouteRecordRaw } from 'vue-router'
import { ShieldCheck } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

export const auditRoutes: RouteRecordRaw[] = [
  {
    path: 'auditoria',
    name: 'audit',
    component: () => import('./pages/AuditLogPage.vue'),
    meta: {
      title: 'Auditoría',
      permission: PERMISSIONS.audit,
      menu: { label: 'Auditoría', icon: ShieldCheck, group: 'Control', order: 2 },
    },
  },
]
