import type { RouteRecordRaw } from 'vue-router'
import { Settings } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const adminRoutes: RouteRecordRaw[] = [
  {
    path: 'administracion',
    name: 'admin',
    component: placeholder,
    props: {
      title: 'Administración',
      description: 'Empresa, canales digitales, feriados, catálogos, plantillas legales, usuarios y parámetros.',
      requirements: 'FE-BO-41 y FE-BO-42',
    },
    meta: {
      title: 'Administración',
      permission: PERMISSIONS.admin,
      menu: { label: 'Administración', icon: Settings, group: 'Configuración', order: 2 },
    },
  },
]
