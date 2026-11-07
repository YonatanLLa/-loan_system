import type { RouteRecordRaw } from 'vue-router'

export const landingRoutes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'landing',
    component: () => import('./pages/LandingPage.vue'),
    meta: { public: true, title: 'Préstamos Personales 100% Digitales' },
  },
]
