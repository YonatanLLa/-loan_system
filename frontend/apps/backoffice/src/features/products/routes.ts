import type { RouteRecordRaw } from 'vue-router'
import { Package } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const productsRoutes: RouteRecordRaw[] = [
  {
    path: 'productos',
    name: 'products',
    component: placeholder,
    props: {
      title: 'Productos',
      description: 'Catálogo de productos de crédito: montos, plazos, tasas, comisiones, mora y elegibilidad.',
      requirements: 'FE-BO-11 y FE-BO-12',
    },
    meta: {
      title: 'Productos',
      permission: PERMISSIONS.products,
      menu: { label: 'Productos', icon: Package, group: 'Configuración', order: 1 },
    },
  },
]
