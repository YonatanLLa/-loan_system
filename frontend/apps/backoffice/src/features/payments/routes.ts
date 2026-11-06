import type { RouteRecordRaw } from 'vue-router'
import { CreditCard } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const paymentsRoutes: RouteRecordRaw[] = [
  {
    path: 'pagos',
    name: 'payments',
    component: () => import('./pages/PaymentsListPage.vue'),
    meta: {
      title: 'Pagos',
      permission: PERMISSIONS.payments,
      menu: { label: 'Pagos', icon: CreditCard, group: 'Operación', order: 4 },
    },
  },
]
