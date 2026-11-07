import type { RouteRecordRaw } from 'vue-router'
import { Users } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

export const customersRoutes: RouteRecordRaw[] = [
  {
    path: 'clientes',
    name: 'customers',
    component: () => import('./pages/CustomersListPage.vue'),
    meta: {
      title: 'Clientes',
      permission: PERMISSIONS.customers,
      menu: { label: 'Clientes', icon: Users, group: 'Operación', order: 1 },
    },
  },
  {
    path: 'clientes/:id',
    name: 'customer-detail',
    component: () => import('./pages/Customer360Page.vue'),
    meta: {
      title: 'Ficha 360 del Cliente',
      permission: PERMISSIONS.customers,
    },
  },
]
