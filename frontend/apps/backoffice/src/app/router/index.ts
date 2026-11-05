import { createRouter, createWebHistory } from 'vue-router'

import { adminRoutes } from '@/features/admin'
import { auditRoutes } from '@/features/audit'
import { authRoutes } from '@/features/auth'
import { collectionsRoutes } from '@/features/collections'
import { customersRoutes } from '@/features/customers'
import { integrationsRoutes } from '@/features/integrations'
import { loansRoutes } from '@/features/loans'
import { originationRoutes } from '@/features/origination'
import { paymentsRoutes } from '@/features/payments'
import { productsRoutes } from '@/features/products'
import { reportsRoutes } from '@/features/reports'
import { treasuryRoutes } from '@/features/treasury'

import AppLayout from '../layouts/AppLayout.vue'
import { installGuards } from './guards'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    ...authRoutes,
    {
      path: '/',
      component: AppLayout,
      children: [
        { path: '', redirect: { name: 'dashboard' } },
        ...reportsRoutes,
        ...customersRoutes,
        ...originationRoutes,
        ...loansRoutes,
        ...paymentsRoutes,
        ...treasuryRoutes,
        ...integrationsRoutes,
        ...collectionsRoutes,
        ...auditRoutes,
        ...productsRoutes,
        ...adminRoutes,
        {
          path: 'sin-permiso',
          name: 'forbidden',
          component: () => import('../pages/ForbiddenPage.vue'),
          meta: { title: 'Sin permiso' },
        },
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

installGuards(router)

export default router
