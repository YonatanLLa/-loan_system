import type { RouteRecordRaw } from 'vue-router'
import { HandCoins } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const loansRoutes: RouteRecordRaw[] = [
  {
    path: 'prestamos',
    name: 'loans',
    component: placeholder,
    props: {
      title: 'Préstamos',
      description: 'Detalle, cronograma, desembolso, documentos, prepago, reestructuración y refinanciamiento.',
      requirements: 'FE-BO-18 a FE-BO-20, FE-BO-25 y FE-BO-26',
    },
    meta: {
      title: 'Préstamos',
      permission: PERMISSIONS.loans,
      menu: { label: 'Préstamos', icon: HandCoins, group: 'Operación', order: 3 },
    },
  },
]
