import type { RouteRecordRaw } from 'vue-router'
import { House } from '@lucide/vue'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const loansRoutes: RouteRecordRaw[] = [
  {
    path: '',
    name: 'home',
    component: placeholder,
    props: {
      title: 'Inicio',
      description: 'Resumen de tus préstamos: próximo vencimiento, días restantes y saldo.',
      requirements: 'FE-CP-10',
    },
    meta: {
      title: 'Inicio',
      menu: { label: 'Inicio', icon: House, order: 1 },
    },
  },
  {
    path: 'prestamos',
    name: 'loans',
    component: placeholder,
    props: {
      title: 'Mis préstamos',
      description: 'Cronograma con estado de cada cuota, estado de cuenta y cancelación anticipada.',
      requirements: 'FE-CP-11, FE-CP-15, FE-CP-16 y FE-CP-17',
    },
    meta: {
      title: 'Mis préstamos',
    },
  },
]
