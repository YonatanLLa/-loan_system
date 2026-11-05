import { describe, expect, it } from 'vitest'
import type { RouteMeta } from 'vue-router'

import { resolveAccess, safeRedirect } from '../app/router/guards'

const route = (meta: RouteMeta, fullPath = '/clientes') => ({ meta, fullPath })

describe('resolveAccess', () => {
  it('deja pasar las rutas públicas sin sesión', () => {
    expect(resolveAccess(route({ public: true }), { isAuthenticated: false, can: () => false })).toBe(
      true,
    )
  })

  it('manda al login recordando a dónde quería ir', () => {
    expect(resolveAccess(route({}), { isAuthenticated: false, can: () => true })).toEqual({
      name: 'login',
      query: { redirect: '/clientes' },
    })
  })

  it('bloquea la ruta si falta el permiso', () => {
    expect(
      resolveAccess(route({ permission: 'payments.reverse' }), {
        isAuthenticated: true,
        can: () => false,
      }),
    ).toEqual({ name: 'forbidden' })
  })

  it('deja pasar con sesión y permiso', () => {
    expect(
      resolveAccess(route({ permission: 'customers.read' }), {
        isAuthenticated: true,
        can: (permission) => permission === 'customers.read',
      }),
    ).toBe(true)
  })
})

describe('safeRedirect', () => {
  it('acepta rutas internas', () => {
    expect(safeRedirect('/prestamos?id=1')).toBe('/prestamos?id=1')
  })

  it('rechaza redirecciones a otros sitios', () => {
    expect(safeRedirect('https://malicioso.example')).toBe('/')
    expect(safeRedirect('//malicioso.example')).toBe('/')
    expect(safeRedirect(undefined, '/dashboard')).toBe('/dashboard')
  })
})
