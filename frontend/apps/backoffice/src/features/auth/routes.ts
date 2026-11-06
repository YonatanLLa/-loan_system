import type { RouteRecordRaw } from 'vue-router'

export const authRoutes: RouteRecordRaw[] = [
  {
    path: '/ingresar',
    name: 'login',
    component: () => import('./pages/LoginPage.vue'),
    meta: { public: true, title: 'Ingresar' },
  },
  {
    path: '/verificacion-dos-pasos',
    name: 'totp',
    component: () => import('./pages/TotpPage.vue'),
    meta: { public: true, title: 'Verificación en dos pasos' },
  },
]
