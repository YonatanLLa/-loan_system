/**
 * MARCADOR DE POSICIÓN — se reemplaza al generar los tipos del backend.
 *
 * Cuando la API .NET exponga su documento OpenAPI, ejecutar desde frontend/:
 *   pnpm gen:api
 * (por defecto lee http://localhost:5000/openapi/v1.json; se cambia con OPENAPI_URL).
 *
 * Prohibido escribir DTOs a mano (FE-NF-03): todos los tipos de la API salen de aquí.
 */
export type paths = Record<string, never>
export type webhooks = Record<string, never>
export interface components {
  schemas: Record<string, never>
  responses: never
  parameters: never
  requestBodies: never
  headers: never
  pathItems: never
}
export type $defs = Record<string, never>
export type operations = Record<string, never>
