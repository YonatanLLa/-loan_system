import { createRouter, createWebHistory } from 'vue-router'

import { applicationsRoutes } from '@/features/applications'
import { complaintsRoutes } from '@/features/complaints'
import { contractRoutes } from '@/features/contract'
import { landingRoutes } from '@/features/landing'
import { loansRoutes } from '@/features/loans'
import { notificationsRoutes } from '@/features/notifications'
import { onboardingRoutes } from '@/features/onboarding'
import { paymentsRoutes } from '@/features/payments'
import { profileRoutes } from '@/features/profile'
import { useSessionStore } from '@/app/stores/session'

import AppLayout from '../layouts/AppLayout.vue'
import PublicLayout from '../layouts/PublicLayout.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    // Landing pública — no requiere sesión
    {
      path: '/',
      component: PublicLayout,
      children: landingRoutes,
    },
    // Portal autenticado
    {
      path: '/',
      component: AppLayout,
      children: [
        ...loansRoutes,
        ...applicationsRoutes,
        ...paymentsRoutes,
        ...profileRoutes,
        ...onboardingRoutes,
        ...contractRoutes,
        ...complaintsRoutes,
        ...notificationsRoutes,
      ],
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('../pages/NotFoundPage.vue'),
      meta: { public: true, title: 'Página no encontrada' },
    },
  ],
  scrollBehavior: () => ({ top: 0 }),
})

router.beforeEach((to) => {
  const session = useSessionStore()

  // Si ya tiene sesión e intenta ir a landing, login o registro, redirige al home
  if (
    (to.name === 'landing' || to.name === 'login' || to.name === 'register') &&
    session.isAuthenticated
  ) {
    return { path: '/inicio' }
  }

  // Si la ruta requiere autenticación y no tiene sesión, va a la landing
  if (!to.meta.public && !session.isAuthenticated) {
    return { path: '/' }
  }

  // Si la ruta exige KYC verificado y el cliente aún no está verificado, redirige a verificación
  if (
    to.meta.requiresKyc &&
    session.isAuthenticated &&
    session.user?.kycStatus !== 'VERIFIED'
  ) {
    return { path: '/verificacion' }
  }
})

router.afterEach((to) => {
  const scrollContainer = document.getElementById('app-scroll-container')
  if (scrollContainer) {
    scrollContainer.scrollTop = 0
  }

  document.title = to.meta.title
    ? `${to.meta.title} · Portal Prestatario`
    : 'Portal Prestatario · Fintech Préstamos'
})

export default router
