import type { RouteRecordRaw } from 'vue-router'
import { HandCoins } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

export const loansRoutes: RouteRecordRaw[] = [
  {
    path: 'prestamos',
    name: 'loans',
    component: () => import('./pages/LoansListPage.vue'),
    meta: {
      title: 'Préstamos',
      permission: PERMISSIONS.loans,
      menu: { label: 'Préstamos', icon: HandCoins, group: 'Operación', order: 3 },
    },
  },
  {
    path: 'prestamos/:id',
    name: 'loan-detail',
    component: () => import('./pages/LoanDetailPage.vue'),
    meta: {
      title: 'Detalle del Préstamo',
      permission: PERMISSIONS.loans,
    },
  },
]
