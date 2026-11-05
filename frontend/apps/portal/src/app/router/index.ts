import { createRouter, createWebHistory } from 'vue-router'

import { applicationsRoutes } from '@/features/applications'
import { complaintsRoutes } from '@/features/complaints'
import { contractRoutes } from '@/features/contract'
import { loansRoutes } from '@/features/loans'
import { notificationsRoutes } from '@/features/notifications'
import { onboardingRoutes } from '@/features/onboarding'
import { paymentsRoutes } from '@/features/payments'
import { profileRoutes } from '@/features/profile'

import AppLayout from '../layouts/AppLayout.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
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

router.afterEach((to) => {
  document.title = to.meta.title
    ? `${to.meta.title} · Portal Prestatario`
    : 'Portal Prestatario · Fintech Préstamos'
})

export default router
