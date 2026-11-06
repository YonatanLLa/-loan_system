import type { RouteRecordRaw } from 'vue-router'
import { ChartColumn, LayoutDashboard } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

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
    component: placeholder,
    props: {
      title: 'Reportes',
      description: 'Cartera, aging, flujo de caja, productividad, conciliación y estado de cuenta.',
      requirements: 'FE-BO-39 y FE-BO-40',
    },
    meta: {
      title: 'Reportes',
      permission: PERMISSIONS.reports,
      menu: { label: 'Reportes', icon: ChartColumn, group: 'Control', order: 1 },
    },
  },
]
