import type { RouteLocationNormalized, RouteLocationRaw, Router } from 'vue-router'

import { useSessionStore } from '../stores/session'

export interface AccessState {
  isAuthenticated: boolean
  can: (permission: string) => boolean
}

/** Decide si se puede entrar a una ruta (función pura, probada en __tests__). */
export function resolveAccess(
  to: Pick<RouteLocationNormalized, 'meta' | 'fullPath'>,
  state: AccessState,
): true | RouteLocationRaw {
  if (to.meta.public) return true
  if (!state.isAuthenticated) return { name: 'login', query: { redirect: to.fullPath } }
  if (to.meta.permission && !state.can(to.meta.permission)) return { name: 'forbidden' }
  return true
}

/** Solo se acepta volver a rutas internas (evita redirecciones abiertas a otros sitios). */
export function safeRedirect(value: unknown, fallback = '/'): string {
  return typeof value === 'string' && value.startsWith('/') && !value.startsWith('//')
    ? value
    : fallback
}

export function installGuards(router: Router) {
  router.beforeEach(async (to) => {
    const session = useSessionStore()
    if (to.name === 'login' && session.isAuthenticated) return { name: 'dashboard' }
    if (to.meta.public) return true

    const isAuthenticated = await session.ensure()
    return resolveAccess(to, { isAuthenticated, can: session.can })
  })

  router.afterEach((to) => {
    document.title = to.meta.title
      ? `${to.meta.title} · Backoffice`
      : 'Backoffice · Sistema de Préstamos'
  })
}
