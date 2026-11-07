import type { RouteRecordRaw } from 'vue-router'
import { ChartColumn, LayoutDashboard } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

export const reportsRoutes: RouteRecordRaw[] = [
  {
    path: 'dashboard',
    name: 'dashboard',
    component: () => import('./pages/DashboardPage.vue'),
    meta: {
      title: 'Dashboard',
      permission: PERMISSIONS.dashboard,
      menu: { label: 'Dashboard', icon: LayoutDashboard, group: 'Principal', order: 1 },
    },
  },
  {
    path: 'reportes',
    name: 'reports',
    component: () => import('./pages/ReportsPage.vue'),
    meta: {
      title: 'Reportes',
      permission: PERMISSIONS.reports,
      menu: { label: 'Reportes', icon: ChartColumn, group: 'Control', order: 1 },
    },
  },
]
