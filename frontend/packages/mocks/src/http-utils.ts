import { delay, HttpResponse } from 'msw'
import type { Page } from '@prestamos/api-client'

import { db, type UserRec } from './db'
import { tickAll } from './domain'

let latencyMs = 200

/** Latencia simulada de cada respuesta (0 en pruebas). */
export function configureMocks(options: { latencyMs?: number }): void {
  if (options.latencyMs !== undefined) latencyMs = options.latencyMs
}

export const wait = (): Promise<unknown> => (latencyMs > 0 ? delay(latencyMs) : Promise.resolve())

/** Error en formato ProblemDetails (RFC 7807), igual que el backend real. */
export function problem(
  status: number,
  title: string,
  detail: string,
  errors?: Record<string, string[]>,
): Response {
  return HttpResponse.json(
    { type: `https://docs.prestamos.com/errors/${status}`, title, status, detail, errors },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  )
}

export const notFound = (what: string): Response =>
  problem(404, 'No encontrado', `${what} no existe.`)

export const forbidden = (): Response =>
  problem(403, 'Sin permiso', 'Tu rol no tiene permiso para esta operación.')

export const unauthorized = (): Response =>
  problem(401, 'Sesión no válida', 'Inicia sesión para continuar.')

export function currentUser(request: Request): UserRec | null {
  const header = request.headers.get('Authorization')
  const token = header?.startsWith('Bearer ') ? header.slice(7) : null
  if (!token) return null
  // `demo` es el token del ingreso demo del backoffice: se comporta como administrador.
  if (token === 'demo') return db.users.find((user) => user.role === 'ROLE_ADMIN') ?? null
  const userId = db.tokens.get(token)
  return db.users.find((user) => user.id === userId) ?? null
}

type Handler<P> = (ctx: { request: Request; params: P; user: UserRec; url: URL }) => Response | Promise<Response>

/**
 * Envuelve un handler: aplica latencia, avanza los procesos simulados (banco, webhooks),
 * exige sesión y, si se indica, un permiso. Devuelve 401/403 como el backend real.
 */
export function secured<P extends Record<string, string> = Record<string, string>>(
  permission: string | null,
  handler: Handler<P>,
) {
  return async ({ request, params }: { request: Request; params: Record<string, unknown> }) => {
    await wait()
    const user = currentUser(request)
    if (!user) return unauthorized()
    if (permission && !user.permissions.includes(permission)) return forbidden()
    tickAll()
    return handler({ request, params: params as P, user, url: new URL(request.url) })
  }
}

export function paginate<T>(items: T[], url: URL): Page<T> {
  const page = Math.max(1, Number(url.searchParams.get('page') ?? 1))
  const pageSize = Math.min(100, Math.max(1, Number(url.searchParams.get('pageSize') ?? 20)))
  return {
    items: items.slice((page - 1) * pageSize, page * pageSize),
    total: items.length,
    page,
    pageSize,
  }
}

export interface Created {
  status: number
  body: unknown
}

export const matches = (text: string, query: string | null): boolean =>
  !query || text.toLowerCase().includes(query.toLowerCase())

/**
 * Idempotencia (RF-13.2, RF-16.2): la misma `Idempotency-Key` devuelve la respuesta original
 * sin volver a ejecutar la operación.
 */
export async function idempotent(
  request: Request,
  produce: () => Promise<Response | Created> | Response | Created,
): Promise<Response> {
  const key = request.headers.get('Idempotency-Key')
  if (key) {
    const previous = db.idempotency.get(key)
    if (previous) return HttpResponse.json(previous.body as never, { status: previous.status })
  }
  const result = await produce()
  if (result instanceof Response) return result
  if (key) db.idempotency.set(key, result)
  return HttpResponse.json(result.body as never, { status: result.status })
}

export async function readJson<T>(request: Request): Promise<T | null> {
  try {
    return (await request.json()) as T
  } catch {
    return null
  }
}

/** PDF mínimo válido, para los endpoints de contrato y recibo. */
export function pdfResponse(title: string): Response {
  const text = `%PDF-1.1
1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj
2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj
3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 300 120]/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>endobj
4 0 obj<</Length 60>>stream
BT /F1 12 Tf 20 60 Td (${title.replace(/[()\\]/g, '')} - documento simulado) Tj ET
endstream
endobj
5 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj
trailer<</Root 1 0 R/Size 6>>
%%EOF`
  return new HttpResponse(text, { headers: { 'Content-Type': 'application/pdf' } })
}
