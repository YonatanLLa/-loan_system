import type { RouteRecordRaw } from 'vue-router'
import { FileText } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

export const originationRoutes: RouteRecordRaw[] = [
  {
    path: 'solicitudes',
    name: 'applications',
    component: () => import('./pages/ApplicationsQueuePage.vue'),
    meta: {
      title: 'Solicitudes',
      permission: PERMISSIONS.applications,
      menu: { label: 'Solicitudes', icon: FileText, group: 'Operación', order: 2 },
    },
  },
  {
    path: 'solicitudes/:id',
    name: 'application-detail',
    component: () => import('./pages/ApplicationEvaluationPage.vue'),
    meta: {
      title: 'Evaluación de Solicitud',
      permission: PERMISSIONS.applications,
    },
  },
]
