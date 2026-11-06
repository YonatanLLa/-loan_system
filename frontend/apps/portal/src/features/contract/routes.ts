import type { RouteRecordRaw } from 'vue-router'

export const contractRoutes: RouteRecordRaw[] = [
  {
    path: 'contrato',
    name: 'contract',
    component: () => import('./pages/ContractPage.vue'),
    meta: {
      title: 'Contrato y Firma Digital',
      requiresKyc: true,
    },
  },
]
