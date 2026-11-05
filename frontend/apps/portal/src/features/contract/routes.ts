import type { RouteRecordRaw } from 'vue-router'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const contractRoutes: RouteRecordRaw[] = [
  {
    path: 'contrato',
    name: 'contract',
    component: placeholder,
    props: {
      title: 'Contrato',
      description: 'Revisa el contrato y la hoja resumen y acéptalos con un código OTP.',
      requirements: 'FE-CP-09',
    },
    meta: {
      title: 'Contrato',
      requiresKyc: true,
    },
  },
]
