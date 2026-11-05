import type { RouteRecordRaw } from 'vue-router'
import { FilePlus } from '@lucide/vue'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const applicationsRoutes: RouteRecordRaw[] = [
  {
    path: 'solicitar',
    name: 'apply',
    component: placeholder,
    props: {
      title: 'Solicitar préstamo',
      description: 'Simula tu cuota y envía tu solicitud con tus sustentos de ingresos.',
      requirements: 'FE-CP-06 y FE-CP-07',
    },
    meta: {
      title: 'Solicitar préstamo',
      requiresKyc: true,
      menu: { label: 'Solicitar', icon: FilePlus, order: 2 },
    },
  },
  {
    path: 'solicitudes',
    name: 'applications',
    component: placeholder,
    props: {
      title: 'Mis solicitudes',
      description: 'Línea de tiempo de cada solicitud: solicitado, en evaluación, aprobado y desembolsado.',
      requirements: 'FE-CP-08',
    },
    meta: {
      title: 'Mis solicitudes',
    },
  },
]
