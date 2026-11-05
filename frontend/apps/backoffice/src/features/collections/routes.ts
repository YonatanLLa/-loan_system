import type { RouteRecordRaw } from 'vue-router'
import { PhoneCall } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const collectionsRoutes: RouteRecordRaw[] = [
  {
    path: 'cobranza',
    name: 'collections',
    component: placeholder,
    props: {
      title: 'Cobranza',
      description: 'Bandeja de aging, gestiones, promesas de pago, asignación, castigo y cuentas por cobrar.',
      requirements: 'FE-BO-32 a FE-BO-36',
    },
    meta: {
      title: 'Cobranza',
      permission: PERMISSIONS.collections,
      menu: { label: 'Cobranza', icon: PhoneCall, group: 'Cobranza', order: 1 },
    },
  },
]
