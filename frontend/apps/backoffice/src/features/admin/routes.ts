import type { RouteRecordRaw } from 'vue-router'
import { Settings } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

export const adminRoutes: RouteRecordRaw[] = [
  {
    path: 'administracion',
    name: 'admin',
    component: () => import('./pages/AdminPage.vue'),
    meta: {
      title: 'Administración',
      permission: PERMISSIONS.admin,
      menu: { label: 'Administración', icon: Settings, group: 'Configuración', order: 2 },
    },
  },
]
