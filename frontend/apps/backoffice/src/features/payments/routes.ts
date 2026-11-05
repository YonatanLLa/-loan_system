import type { RouteRecordRaw } from 'vue-router'
import { CreditCard } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const paymentsRoutes: RouteRecordRaw[] = [
  {
    path: 'pagos',
    name: 'payments',
    component: placeholder,
    props: {
      title: 'Pagos',
      description: 'Pagos pendientes de confirmación, registro de pagos identificados, reversos y saldo a favor.',
      requirements: 'FE-BO-21 a FE-BO-24',
    },
    meta: {
      title: 'Pagos',
      permission: PERMISSIONS.payments,
      menu: { label: 'Pagos', icon: CreditCard, group: 'Operación', order: 4 },
    },
  },
]
