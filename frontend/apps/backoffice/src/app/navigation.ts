import type { Component } from 'vue'
import type { RouteRecordNormalized } from 'vue-router'

/** Grupos del menú lateral, en el orden en que se muestran. */
export const MENU_GROUPS = [
  'Principal',
  'Operación',
  'Tesorería',
  'Cobranza',
  'Control',
  'Configuración',
] as const

export type MenuGroup = (typeof MENU_GROUPS)[number]

export interface MenuMeta {
  label: string
  icon: Component
  group: MenuGroup
  order: number
}

export interface MenuItem extends MenuMeta {
  routeName: string
}

export interface MenuSection {
  group: MenuGroup
  items: MenuItem[]
}

/**
 * Arma el menú desde las rutas que declaran `meta.menu`, filtrando por permiso.
 * Ocultar es solo comodidad: el backend vuelve a validar cada petición (FE-BO-03).
 */
export function buildMenu(
  routes: RouteRecordNormalized[],
  can: (permission: string) => boolean,
): MenuSection[] {
  const items: MenuItem[] = routes
    .filter((route) => route.meta.menu && typeof route.name === 'string')
    .filter((route) => !route.meta.permission || can(route.meta.permission))
    .map((route) => ({ ...(route.meta.menu as MenuMeta), routeName: route.name as string }))

  return MENU_GROUPS.map((group) => ({
    group,
    items: items.filter((item) => item.group === group).sort((a, b) => a.order - b.order),
  })).filter((section) => section.items.length > 0)
}
