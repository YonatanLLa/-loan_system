import type { RouteRecordRaw } from 'vue-router'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const onboardingRoutes: RouteRecordRaw[] = [
  {
    path: 'verificacion',
    name: 'onboarding',
    component: placeholder,
    props: {
      title: 'Verifica tu identidad',
      description: 'Foto de tu DNI, selfie con prueba de vida, consentimientos y cuenta de desembolso.',
      requirements: 'FE-CP-03 a FE-CP-05',
    },
    meta: {
      title: 'Verifica tu identidad',
    },
  },
]
