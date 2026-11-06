import type { RouteRecordRaw } from 'vue-router'
import { FileText } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

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
