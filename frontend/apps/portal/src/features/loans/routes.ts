import type { RouteRecordRaw } from 'vue-router'
import { House } from '@lucide/vue'

export const loansRoutes: RouteRecordRaw[] = [
  {
    path: '',
    name: 'home',
    component: () => import('./pages/HomePage.vue'),
    meta: {
      title: 'Inicio',
      menu: { label: 'Inicio', icon: House, order: 1 },
    },
  },
  {
    path: 'prestamos',
    name: 'loans',
    component: () => import('./pages/LoansPage.vue'),
    meta: {
      title: 'Mis Préstamos',
    },
  },
]
