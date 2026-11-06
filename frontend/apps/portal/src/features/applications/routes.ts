import type { RouteRecordRaw } from 'vue-router'
import { FilePlus } from '@lucide/vue'

export const applicationsRoutes: RouteRecordRaw[] = [
  {
    path: 'solicitar',
    name: 'apply',
    component: () => import('./pages/ApplyPage.vue'),
    meta: {
      title: 'Solicitar Préstamo',
      requiresKyc: true,
      menu: { label: 'Solicitar', icon: FilePlus, order: 2 },
    },
  },
  {
    path: 'solicitudes',
    name: 'applications',
    component: () => import('./pages/ApplicationsPage.vue'),
    meta: {
      title: 'Mis Solicitudes',
    },
  },
]
