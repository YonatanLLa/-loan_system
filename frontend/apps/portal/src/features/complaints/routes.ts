import type { RouteRecordRaw } from 'vue-router'

const ComplaintsPage = () => import('./pages/ComplaintsPage.vue')

export const complaintsRoutes: RouteRecordRaw[] = [
  {
    path: 'libro-de-reclamaciones',
    name: 'complaints',
    component: ComplaintsPage,
    meta: {
      title: 'Libro de Reclamaciones',
      public: true,
    },
  },
]
