import type { RouteRecordRaw } from 'vue-router'
import { User } from '@lucide/vue'

export const profileRoutes: RouteRecordRaw[] = [
  {
    path: 'perfil',
    name: 'profile',
    component: () => import('./pages/ProfilePage.vue'),
    meta: {
      title: 'Mi perfil',
      menu: { label: 'Mi perfil', icon: User, group: 'Configuración', order: 1 },
    },
  },
]
