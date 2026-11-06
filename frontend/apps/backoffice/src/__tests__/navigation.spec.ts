import { describe, expect, it } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'
import { defineComponent } from 'vue'
import { Users, HandCoins, Settings } from '@lucide/vue'

import { buildMenu } from '../app/navigation'

const Empty = defineComponent({ render: () => null })

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    {
      path: '/prestamos',
      name: 'loans',
      component: Empty,
      meta: {
        permission: 'loans.read',
        menu: { label: 'Préstamos', icon: HandCoins, group: 'Operación', order: 3 },
      },
    },
    {
      path: '/clientes',
      name: 'customers',
      component: Empty,
      meta: {
        permission: 'customers.read',
        menu: { label: 'Clientes', icon: Users, group: 'Operación', order: 1 },
      },
    },
    {
      path: '/administracion',
      name: 'admin',
      component: Empty,
      meta: {
        permission: 'admin.manage',
        menu: { label: 'Administración', icon: Settings, group: 'Configuración', order: 1 },
      },
    },
    { path: '/sin-menu', name: 'hidden', component: Empty },
  ],
})

describe('buildMenu', () => {
  it('agrupa, ordena y oculta lo que el usuario no puede ver', () => {
    const menu = buildMenu(router.getRoutes(), (permission) => permission !== 'admin.manage')

    expect(menu).toHaveLength(1)
    expect(menu[0]?.group).toBe('Operación')
    expect(menu[0]?.items.map((item) => item.routeName)).toEqual(['customers', 'loans'])
  })
})
