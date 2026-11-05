import type { RouteRecordRaw } from 'vue-router'
import { Landmark } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const treasuryRoutes: RouteRecordRaw[] = [
  {
    path: 'tesoreria',
    name: 'treasury',
    component: placeholder,
    props: {
      title: 'Tesorería',
      description: 'Cuentas bancarias propias, desembolsos en tránsito, conciliación de extractos y cuadre diario.',
      requirements: 'FE-BO-27 a FE-BO-30',
    },
    meta: {
      title: 'Tesorería',
      permission: PERMISSIONS.treasury,
      menu: { label: 'Tesorería', icon: Landmark, group: 'Tesorería', order: 1 },
    },
  },
]
