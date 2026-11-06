import type { RouteRecordRaw } from 'vue-router'
import { Wallet } from '@lucide/vue'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const paymentsRoutes: RouteRecordRaw[] = [
  {
    path: 'pagar',
    name: 'pay',
    component: placeholder,
    props: {
      title: 'Pagar cuota',
      description: 'Paga con QR (Yape o Plin), tarjeta o transferencia y descarga tu recibo.',
      requirements: 'FE-CP-12 a FE-CP-14',
    },
    meta: {
      title: 'Pagar cuota',
      menu: { label: 'Pagar', icon: Wallet, order: 3 },
    },
  },
]
