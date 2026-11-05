import 'vue-router'

import type { MenuMeta } from '../navigation'

export {}

declare module 'vue-router' {
  interface RouteMeta {
    /** Accesible sin sesión (login, página no encontrada). */
    public?: boolean
    /** Permiso requerido, p. ej. 'payments.reverse'. */
    permission?: string
    /** Título de la pestaña y de la barra superior. */
    title?: string
    /** Si se define, la ruta aparece en el menú lateral. */
    menu?: MenuMeta
  }
}
