import type { RouteRecordRaw } from 'vue-router'

export const onboardingRoutes: RouteRecordRaw[] = [
  {
    path: 'login',
    name: 'login',
    component: () => import('./pages/LoginPage.vue'),
    meta: {
      public: true,
      title: 'Iniciar Sesión',
    },
  },
  {
    path: 'registro',
    name: 'register',
    component: () => import('./pages/RegisterPage.vue'),
    meta: {
      public: true,
      title: 'Registro de Cuenta',
    },
  },
  {
    path: 'verificacion',
    name: 'onboarding',
    component: () => import('./pages/OnboardingPage.vue'),
    meta: {
      title: 'Verifica tu Identidad',
    },
  },
]
