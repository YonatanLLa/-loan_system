import type { RouteRecordRaw } from 'vue-router'
import { FileText } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const originationRoutes: RouteRecordRaw[] = [
  {
    path: 'solicitudes',
    name: 'applications',
    component: placeholder,
    props: {
      title: 'Solicitudes',
      description: 'Simulador, bandeja de solicitudes, evaluación con DTI y dictamen.',
      requirements: 'FE-BO-13 a FE-BO-17',
    },
    meta: {
      title: 'Solicitudes',
      permission: PERMISSIONS.applications,
      menu: { label: 'Solicitudes', icon: FileText, group: 'Operación', order: 2 },
    },
  },
]
