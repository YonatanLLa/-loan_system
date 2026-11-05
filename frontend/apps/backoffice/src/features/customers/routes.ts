import type { RouteRecordRaw } from 'vue-router'
import { Users } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const customersRoutes: RouteRecordRaw[] = [
  {
    path: 'clientes',
    name: 'customers',
    component: placeholder,
    props: {
      title: 'Clientes',
      description: 'Registro, expediente KYC, cuentas bancarias del cliente y ficha 360.',
      requirements: 'FE-BO-06 a FE-BO-10',
    },
    meta: {
      title: 'Clientes',
      permission: PERMISSIONS.customers,
      menu: { label: 'Clientes', icon: Users, group: 'Operación', order: 1 },
    },
  },
]
