import type { RouteRecordRaw } from 'vue-router'
import { Package } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

export const productsRoutes: RouteRecordRaw[] = [
  {
    path: 'productos',
    name: 'products',
    component: () => import('./pages/ProductsListPage.vue'),
    meta: {
      title: 'Productos',
      permission: PERMISSIONS.products,
      menu: { label: 'Productos', icon: Package, group: 'Configuración', order: 1 },
    },
  },
]
