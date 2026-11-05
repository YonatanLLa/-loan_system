import type { RouteRecordRaw } from 'vue-router'
import { ShieldCheck } from '@lucide/vue'

import { PERMISSIONS } from '@/app/permissions'

// Pantalla provisional hasta que la feature tenga sus páginas en ./pages.
const placeholder = () => import('@/shared/components/FeaturePlaceholder.vue')

export const auditRoutes: RouteRecordRaw[] = [
  {
    path: 'auditoria',
    name: 'audit',
    component: placeholder,
    props: {
      title: 'Auditoría',
      description: 'Bitácora de cambios con usuario, fecha y vista antes y después.',
      requirements: 'FE-BO-05',
    },
    meta: {
      title: 'Auditoría',
      permission: PERMISSIONS.audit,
      menu: { label: 'Auditoría', icon: ShieldCheck, group: 'Control', order: 2 },
    },
  },
]
