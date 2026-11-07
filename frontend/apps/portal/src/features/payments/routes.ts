import type { RouteRecordRaw } from 'vue-router'
import { Wallet } from '@lucide/vue'

const PayPage = () => import('./pages/PayPage.vue')

export const paymentsRoutes: RouteRecordRaw[] = [
  {
    path: 'pagar',
    name: 'pay',
    component: PayPage,
    meta: {
      title: 'Pagar cuota',
      menu: { label: 'Pagar', icon: Wallet, order: 3 },
    },
  },
]
