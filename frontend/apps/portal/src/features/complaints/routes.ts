import type { RouteRecordRaw } from 'vue-router'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const complaintsRoutes: RouteRecordRaw[] = [
  {
    path: 'libro-de-reclamaciones',
    name: 'complaints',
    component: placeholder,
    props: {
      title: 'Libro de Reclamaciones',
      description: 'Registra tu reclamo o queja y recibe tu constancia.',
      requirements: 'FE-CP-21',
    },
    meta: {
      title: 'Libro de Reclamaciones',
      public: true,
    },
  },
]
