/**
 * Permisos del backoffice usados por rutas y botones.
 *
 * PROVISIONAL: los nombres definitivos los define el backend (BE-SEC-05) y llegan en
 * GET /api/me. Cuando exista el contrato OpenAPI, ajustar estos valores a esa lista.
 */
export const PERMISSIONS = {
  dashboard: 'dashboard.view',
  customers: 'customers.read',
  applications: 'applications.read',
  loans: 'loans.read',
  payments: 'payments.read',
  paymentsReverse: 'payments.reverse',
  treasury: 'treasury.read',
  integrations: 'integrations.read',
  collections: 'collections.read',
  reports: 'reports.read',
  audit: 'audit.read',
  products: 'products.manage',
  admin: 'admin.manage',
} as const

export type Permission = (typeof PERMISSIONS)[keyof typeof PERMISSIONS]

export const ALL_PERMISSIONS: readonly Permission[] = Object.values(PERMISSIONS)
