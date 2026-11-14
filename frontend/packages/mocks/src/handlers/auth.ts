import { http, HttpResponse } from 'msw'
import type { AuthTokensResponse, LoginRequest, LoginResponse, Me } from '@prestamos/api-client'

import { db, nextId, nowIso, recordAudit, type UserRec } from '../db'
import { problem, readJson, secured, wait } from '../http-utils'

const MAX_FAILED = 5
/** Intentos fallidos por usuario; vive en `db.counters` para que `resetDb()` lo reinicie. */
const failureKey = (userId: string): string => `login-failures:${userId}`
const failures = (userId: string): number => db.counters.get(failureKey(userId)) ?? 0
const STORAGE_KEY = 'prestamos-mock-session'
const refreshTokenOwners = new Map<string, string>()

function storage(): Storage | null {
  try {
    return typeof localStorage === 'undefined' ? null : localStorage
  } catch {
    return null
  }
}

/** La sesión simulada sobrevive a recargas de página (solo en el navegador). */
export function restoreSession(): void {
  const raw = storage()?.getItem(STORAGE_KEY)
  if (!raw) return
  try {
    const { refreshUserId } = JSON.parse(raw) as { refreshUserId: string }
    if (db.users.some((user) => user.id === refreshUserId)) refreshSession.userId = refreshUserId
  } catch {
    storage()?.removeItem(STORAGE_KEY)
  }
}

const refreshSession: { userId: string | null } = { userId: null }

function remember(userId: string | null): void {
  refreshSession.userId = userId
  if (userId) storage()?.setItem(STORAGE_KEY, JSON.stringify({ refreshUserId: userId }))
  else storage()?.removeItem(STORAGE_KEY)
}

function issueTokens(user: UserRec): AuthTokensResponse {
  const accessToken = `tok-${crypto.randomUUID()}`
  const refreshToken = `rtok-${crypto.randomUUID()}`
  db.tokens.set(accessToken, user.id)
  refreshTokenOwners.set(refreshToken, user.id)
  return {
    accessToken,
    refreshToken,
    expiresInSeconds: 1800,
    role: user.role,
  }
}

export function toMe(user: UserRec): Me {
  const customer = user.customerId ? db.customers.find((c) => c.id === user.customerId) : undefined
  return {
    id: user.id,
    userId: user.id,
    fullName: user.fullName,
    email: user.email,
    role: user.role,
    permissions: user.permissions,
    mfaEnabled: false,
    customerId: user.customerId,
    kycStatus: customer?.kycStatus,
  }
}

async function login(request: Request, clientOnly: boolean): Promise<Response> {
  await wait()
  const body = await readJson<LoginRequest & { identifier?: string }>(request)
  const identifier = (body?.email ?? body?.identifier ?? '').trim().toLowerCase()
  const user = db.users.find((candidate) => {
    if (clientOnly && candidate.role !== 'ROLE_CLIENT') return false
    if (candidate.email.toLowerCase() === identifier) return true
    const customer = candidate.customerId
      ? db.customers.find((c) => c.id === candidate.customerId)
      : undefined
    return (
      (clientOnly || candidate.role === 'ROLE_CLIENT') &&
      (customer?.phone === identifier || customer?.docNumber.toLowerCase() === identifier)
    )
  })

  if (user && failures(user.id) >= MAX_FAILED) {
    return problem(
      423,
      'Cuenta bloqueada',
      'Demasiados intentos fallidos. Inténtalo en 15 minutos.',
    )
  }
  if (!user || user.password !== body?.password) {
    if (user) db.counters.set(failureKey(user.id), failures(user.id) + 1)
    return problem(401, 'Credenciales inválidas', 'Credenciales inválidas')
  }
  db.counters.delete(failureKey(user.id))
  remember(user.id)
  recordAudit({ entityName: 'users', entityId: user.id, action: 'LOGIN', userName: user.fullName })
  const tokens = issueTokens(user)
  if (clientOnly) {
    return HttpResponse.json(tokens)
  }
  const response: LoginResponse = {
    status: 'AUTHENTICATED',
    accessToken: tokens.accessToken,
    refreshToken: tokens.refreshToken,
    expiresInSeconds: tokens.expiresInSeconds,
    role: tokens.role,
    mfaToken: null,
    mfaExpiresInSeconds: null,
  }
  return HttpResponse.json(response)
}

interface RegisterBody {
  docType: 'DNI' | 'CE' | 'PASAPORTE'
  docNumber: string
  fullName: string
  email: string
  phone: string
  password: string
}

export const authHandlers = [
  http.post('*/api/auth/login', ({ request }) => login(request, false)),
  http.post('*/api/client/login', ({ request }) => login(request, true)),

  // Envío y verificación de OTP (FE-CP-01, FE-CP-09, FE-CP-18).
  http.post('*/api/auth/otp/send', async ({ request }) => {
    await wait()
    const body = await readJson<{ phone: string; purpose: string }>(request)
    if (!body?.phone || !/^9\d{8}$/.test(body.phone)) {
      return problem(
        400,
        'Celular inválido',
        'Indica un celular peruano de 9 dígitos que empiece con 9.',
      )
    }
    return HttpResponse.json({
      message: 'Código de verificación enviado exitosamente por SMS/WhatsApp.',
      expiresInSeconds: 120,
      demoCode: '123456',
    })
  }),

  http.post('*/api/auth/otp/verify', async ({ request }) => {
    await wait()
    const body = await readJson<{ phone: string; code: string; purpose: string }>(request)
    if (!body?.code?.trim() || !body?.phone) {
      return problem(400, 'Código requerido', 'Ingresa el código OTP recibido.')
    }
    if (body.code.trim() !== '123456') {
      return problem(
        400,
        'Código incorrecto',
        'El código OTP es inválido o expiró. Usa 123456 en entorno demo.',
      )
    }
    return HttpResponse.json({ verified: true, message: 'Identidad confirmada exitosamente.' })
  }),

  http.post('*/api/auth/refresh', async ({ request }) => {
    await wait()
    const body = await readJson<{ refreshToken?: string }>(request)
    const tokenOwnerId = body?.refreshToken ? refreshTokenOwners.get(body.refreshToken) : undefined
    if (body?.refreshToken) refreshTokenOwners.delete(body.refreshToken)
    const user =
      (tokenOwnerId ? db.users.find((u) => u.id === tokenOwnerId) : undefined) ??
      db.users.find((u) => u.id === refreshSession.userId) ??
      (body?.refreshToken ? db.users.find((u) => u.id === 'usr-100') : undefined)
    return user
      ? HttpResponse.json(issueTokens(user))
      : problem(401, 'Sesión no válida', 'Inicia sesión.')
  }),

  http.post('*/api/auth/logout', async ({ request }) => {
    await wait()
    const body = await readJson<{ refreshToken?: string | null }>(request)
    if (body?.refreshToken) refreshTokenOwners.delete(body.refreshToken)
    remember(null)
    return new HttpResponse(null, { status: 204 })
  }),

  http.post('*/api/client/logout', async () => {
    await wait()
    remember(null)
    return new HttpResponse(null, { status: 204 })
  }),

  http.post('*/api/client/verify-otp', async ({ request }) => {
    await wait()
    const body = await readJson<{ target?: string; code?: string }>(request)
    if (!body?.code || !/^\d{6}$/.test(body.code)) {
      return problem(400, 'Código OTP inválido', 'Ingresa el código de 6 dígitos.')
    }
    const target = (body.target ?? '').trim().toLowerCase()
    const user =
      db.users.find((u) => {
        if (u.role !== 'ROLE_CLIENT') return false
        if (u.email.toLowerCase() === target) return true
        const cust = u.customerId ? db.customers.find((c) => c.id === u.customerId) : undefined
        return cust?.phone === target || cust?.docNumber.toLowerCase() === target
      }) ?? db.users.find((u) => u.id === 'usr-100')!

    const customer = db.customers.find((c) => c.id === user.customerId) ?? db.customers[0]!
    const { accessToken, refreshToken } = issueTokens(user)
    remember(user.id)
    return HttpResponse.json({
      accessToken,
      refreshToken,
      user: {
        id: user.id,
        customerId: customer.id,
        fullName: customer.fullName,
        email: customer.email,
        docType: customer.docType === 'RUC' ? 'DNI' : customer.docType,
        docNumber: customer.docNumber,
        phone: customer.phone,
        kycStatus: customer.kycStatus,
      },
    })
  }),

  http.get(
    '*/api/me',
    secured(null, ({ user }) => HttpResponse.json(toMe(user))),
  ),

  http.post('*/api/client/register', async ({ request }) => {
    await wait()
    const body = await readJson<RegisterBody>(request)
    if (!body) return problem(400, 'Solicitud inválida', 'Cuerpo inválido.')
    const errors: Record<string, string[]> = {}
    if (!/^\d{8}$/.test(body.docNumber) && body.docType === 'DNI')
      errors.DocNumber = ['El DNI tiene 8 dígitos']
    if (!/^9\d{8}$/.test(body.phone)) errors.Phone = ['El celular tiene 9 dígitos y empieza con 9']
    if ((body.password ?? '').length < 8)
      errors.Password = ['La contraseña debe tener al menos 8 caracteres']
    if (Object.keys(errors).length > 0)
      return problem(400, 'Datos inválidos', 'Revisa los campos.', errors)
    if (db.customers.some((c) => c.docType === body.docType && c.docNumber === body.docNumber)) {
      return problem(409, 'Documento duplicado', 'Ya existe un cliente con ese documento.')
    }
    const customerId = nextId('cus')
    db.customers.push({
      id: customerId,
      personType: 'NATURAL',
      docType: body.docType,
      docNumber: body.docNumber,
      fullName: body.fullName,
      email: body.email,
      phone: body.phone,
      address: '',
      district: '',
      birthDate: '1990-01-01',
      monthlyNetIncome: '0.00',
      occupation: '',
      employerName: '',
      workSeniorityMonths: 0,
      kycStatus: 'PENDING',
      creditBalance: '0.00',
      createdAt: nowIso(),
    })
    const newUser = {
      id: nextId('usr'),
      email: body.email,
      password: body.password,
      fullName: body.fullName,
      role: 'ROLE_CLIENT',
      permissions: ['client.portal'],
      customerId,
    }
    db.users.push(newUser)
    const token = issueTokens(newUser)
    remember(newUser.id)
    recordAudit({
      entityName: 'customers',
      entityId: customerId,
      action: 'CREATE',
      after: { docNumber: body.docNumber },
    })
    return HttpResponse.json(
      { customerId, kycStatus: 'PENDING', accessToken: token.accessToken },
      { status: 201 },
    )
  }),
]
