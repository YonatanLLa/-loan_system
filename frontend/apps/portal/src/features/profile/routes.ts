import type { RouteRecordRaw } from 'vue-router'
import { User } from '@lucide/vue'

const ProfilePage = () => import('./pages/ProfilePage.vue')

export const profileRoutes: RouteRecordRaw[] = [
  {
    path: 'perfil',
    name: 'profile',
    component: ProfilePage,
    meta: {
      title: 'Mi perfil',
      menu: { label: 'Perfil', icon: User, order: 4 },
    },
  },
]
