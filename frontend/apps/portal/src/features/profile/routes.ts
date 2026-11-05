import type { RouteRecordRaw } from 'vue-router'
import { User } from '@lucide/vue'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const profileRoutes: RouteRecordRaw[] = [
  {
    path: 'perfil',
    name: 'profile',
    component: placeholder,
    props: {
      title: 'Mi perfil',
      description: 'Tus datos, tu cuenta de desembolso y la devolución de tu saldo a favor.',
      requirements: 'FE-CP-18 y FE-CP-19',
    },
    meta: {
      title: 'Mi perfil',
      menu: { label: 'Perfil', icon: User, order: 4 },
    },
  },
]
