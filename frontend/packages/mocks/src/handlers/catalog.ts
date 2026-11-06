import { http, HttpResponse } from 'msw'
import type { Company, Holiday, LoanProduct, PaymentMethodConfig } from '@prestamos/api-client'

import { db, nextId, nowIso, recordAudit } from '../db'
import { matches, notFound, paginate, problem, readJson, secured } from '../http-utils'
import { toCents } from '../money'

export const catalogHandlers = [
  // Productos (US-07): los lee cualquier sesión, solo `products.manage` los crea.
  http.get(
    '*/api/loan-products',
    secured(null, ({ url }) => {
      const status = url.searchParams.get('status')
      return HttpResponse.json(db.products.filter((p) => !status || p.status === status))
    }),
  ),

  http.post(
    '*/api/loan-products',
    secured('products.manage', async ({ request, user }) => {
      const body = await readJson<Omit<LoanProduct, 'id' | 'status'>>(request)
      if (!body?.name?.trim())
        return problem(400, 'Datos inválidos', 'El nombre es obligatorio.', {
          Name: ['El nombre es obligatorio'],
        })
      const errors: Record<string, string[]> = {}
      if (toCents(body.minAmount) > toCents(body.maxAmount))
        errors.MinAmount = ['El monto mínimo no puede superar al máximo']
      if (body.minTerm > body.maxTerm)
        errors.MinTerm = ['El plazo mínimo no puede superar al máximo']
      if (Number(body.annualRate) <= 0) errors.AnnualRate = ['La tasa debe ser mayor a cero']
      if (Object.keys(errors).length > 0)
        return problem(400, 'Inconsistencia de rangos', 'Revisa los límites del producto.', errors)
      const product: LoanProduct = { ...body, id: nextId('prod'), status: 'ACTIVO' }
      db.products.push(product)
      recordAudit({
        entityName: 'loan_products',
        entityId: product.id,
        action: 'CREATE',
        after: { name: product.name },
        userName: user.fullName,
      })
      return HttpResponse.json(product, { status: 201 })
    }),
  ),

  http.put(
    '*/api/loan-products/:id',
    secured('products.manage', async ({ request, params, user }) => {
      const product = db.products.find((p) => p.id === params.id)
      if (!product) return notFound('El producto')
      const body = await readJson<Partial<LoanProduct>>(request)
      if (!body) return problem(400, 'Solicitud inválida', 'Cuerpo inválido.')
      const before = { ...product }
      Object.assign(product, body)
      recordAudit({
        entityName: 'loan_products',
        entityId: product.id,
        action: 'UPDATE',
        before,
        after: { name: product.name, status: product.status },
        userName: user.fullName,
      })
      return HttpResponse.json(product)
    }),
  ),

  // Administración (US-39 a US-42).
  http.get(
    '*/api/admin/company',
    secured('admin.manage', () => HttpResponse.json(db.company)),
  ),

  http.put(
    '*/api/admin/company',
    secured('admin.manage', async ({ request, user }) => {
      const body = await readJson<Company>(request)
      if (!body) return problem(400, 'Solicitud inválida', 'Cuerpo inválido.')
      const before = { ...db.company }
      db.company = { ...db.company, ...body }
      recordAudit({
        entityName: 'company',
        entityId: 'company',
        action: 'UPDATE',
        before,
        after: { ...db.company },
        userName: user.fullName,
      })
      return HttpResponse.json(db.company)
    }),
  ),

  http.get(
    '*/api/admin/holidays',
    secured(null, () =>
      HttpResponse.json([...db.holidays].sort((a, b) => a.date.localeCompare(b.date))),
    ),
  ),

  http.post(
    '*/api/admin/holidays',
    secured('admin.manage', async ({ request }) => {
      const body = await readJson<Omit<Holiday, 'id'>>(request)
      if (!body?.date || !body.name)
        return problem(400, 'Datos inválidos', 'Indica fecha y nombre.')
      if (db.holidays.some((h) => h.date === body.date))
        return problem(409, 'Fecha duplicada', 'Ya existe un feriado en esa fecha.')
      const holiday: Holiday = {
        id: nextId('hol'),
        date: body.date,
        name: body.name,
        isRecurring: !!body.isRecurring,
      }
      db.holidays.push(holiday)
      return HttpResponse.json(holiday, { status: 201 })
    }),
  ),

  http.delete(
    '*/api/admin/holidays/:id',
    secured<{ id: string }>('admin.manage', ({ params }) => {
      const index = db.holidays.findIndex((h) => h.id === params.id)
      if (index < 0) return notFound('El feriado')
      db.holidays.splice(index, 1)
      return new HttpResponse(null, { status: 204 })
    }),
  ),

  http.get(
    '*/api/admin/payment-methods',
    secured(null, () => HttpResponse.json(db.paymentMethods)),
  ),

  http.put(
    '*/api/admin/payment-methods/:id',
    secured<{ id: string }>('admin.manage', async ({ request, params, user }) => {
      const method = db.paymentMethods.find((m) => m.id === params.id)
      if (!method) return notFound('El método de pago')
      const body = await readJson<Partial<PaymentMethodConfig>>(request)
      const before = { isActive: method.isActive }
      if (body?.isActive !== undefined) method.isActive = body.isActive
      if (body?.name) method.name = body.name
      recordAudit({
        entityName: 'payment_methods',
        entityId: method.id,
        action: 'UPDATE',
        before,
        after: { isActive: method.isActive },
        userName: user.fullName,
      })
      return HttpResponse.json(method)
    }),
  ),

  // Usuarios internos del backoffice (FE-BO-41).
  http.get(
    '*/api/admin/users',
    secured('admin.manage', () => {
      const users = db.users
        .filter((u) => u.role !== 'ROLE_CLIENT')
        .map(({ password: _, ...rest }) => ({
          ...rest,
          createdAt: nowIso(),
        }))
      return HttpResponse.json(users)
    }),
  ),

  http.post(
    '*/api/admin/users',
    secured('admin.manage', async ({ request, user }) => {
      const body = await readJson<{ email: string; fullName: string; role: string; password?: string; permissions?: string[] }>(request)
      if (!body?.email?.trim() || !body?.fullName?.trim() || !body?.role) {
        return problem(400, 'Datos incompletos', 'Indica correo, nombre completo y rol.')
      }
      if (db.users.some((u) => u.email.toLowerCase() === body.email.trim().toLowerCase())) {
        return problem(409, 'Correo duplicado', 'Ya existe un usuario con ese correo electrónico.')
      }
      const newUser = {
        id: nextId('usr'),
        email: body.email.trim().toLowerCase(),
        fullName: body.fullName.trim(),
        role: body.role,
        password: body.password || 'Demo1234!',
        permissions: body.permissions || ['dashboard.view'],
      }
      db.users.push(newUser)
      recordAudit({
        entityName: 'users',
        entityId: newUser.id,
        action: 'CREATE',
        after: { email: newUser.email, role: newUser.role },
        userName: user.fullName,
      })
      const { password: _, ...result } = newUser
      return HttpResponse.json(result, { status: 201 })
    }),
  ),

  // Parámetros globales del negocio (FE-BO-42).
  http.get(
    '*/api/admin/parameters',
    secured('admin.manage', () => HttpResponse.json(db.parameters)),
  ),

  http.put(
    '*/api/admin/parameters',
    secured('admin.manage', async ({ request, user }) => {
      const body = await readJson<Partial<typeof db.parameters>>(request)
      if (!body) return problem(400, 'Solicitud inválida', 'Cuerpo inválido.')
      const before = { ...db.parameters }
      db.parameters = { ...db.parameters, ...body }
      recordAudit({
        entityName: 'parameters',
        entityId: 'global',
        action: 'UPDATE',
        before,
        after: { ...db.parameters },
        userName: user.fullName,
      })
      return HttpResponse.json(db.parameters)
    }),
  ),

  // Bitácora inmutable (US-03): solo lectura.
  http.get(
    '*/api/audit-logs',
    secured('audit.read', ({ url }) => {
      const entity = url.searchParams.get('entity')
      const action = url.searchParams.get('action')
      const q = url.searchParams.get('q')
      const items = db.audit
        .filter((e) => !entity || e.entityName === entity)
        .filter((e) => !action || e.action === action)
        .filter((e) => matches(`${e.userName} ${e.entityId}`, q))
      return HttpResponse.json(paginate(items, url))
    }),
  ),
]
