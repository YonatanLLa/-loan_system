import { afterAll, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import type { ApiError, Loan, Page, Payment, Simulation } from '@prestamos/api-client'

import { configureMocks } from '../http-utils'
import { server } from '../node'
import { resetDb } from '../seed'
import { admin, client, loginAs, portalClient } from './helpers'

beforeAll(() => {
  configureMocks({ latencyMs: 0 })
  server.listen({ onUnhandledFrame: 'error' })
})
afterAll(() => server.close())
beforeEach(() => {
  vi.useRealTimers()
  resetDb()
})

async function failure(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise
  } catch (error) {
    return error as ApiError
  }
  throw new Error('Se esperaba un error')
}

describe('sesión', () => {
  it('rechaza una contraseña incorrecta con 401 y bloquea tras 5 intentos (423)', async () => {
    const attempt = () =>
      client().post(
        '/auth/login',
        { email: 'analista@sistema.local', password: 'mala' },
        { skipAuthRefresh: true },
      )
    for (let i = 0; i < 5; i += 1) expect((await failure(attempt())).status).toBe(401)
    expect((await failure(attempt())).status).toBe(423)
  })

  it('exige token y respeta los permisos del rol', async () => {
    expect((await failure(client().get('/me'))).status).toBe(401)
    const analyst = await loginAs('analista@sistema.local')
    expect((await analyst.get('/me')).data.role).toBe('ROLE_ANALYST')
    expect((await failure(analyst.get('/audit-logs'))).status).toBe(403)
  })
})

describe('simulador y solicitudes', () => {
  it('simula sin crear datos y el capital cuadra', async () => {
    const api = await admin()
    const before = (await api.get<Page<Loan>>('/loans')).data.total
    const { data } = await api.post<Simulation>('/loan-applications/simulate', {
      productId: 'prod-001',
      amount: '5000.00',
      term: 12,
      frequency: 'MENSUAL',
    })
    expect(data.installments).toHaveLength(12)
    const principal = data.installments.reduce(
      (total, row) => total + Math.round(Number(row.principal) * 100),
      0,
    )
    expect(principal).toBe(500_000)
    expect(Number(data.summary.tcea)).toBeGreaterThan(Number(data.summary.tea))
    expect((await api.get<Page<Loan>>('/loans')).data.total).toBe(before)
  })

  it('un monto fuera del rango del producto responde 400 con el campo', async () => {
    const api = await admin()
    const error = await failure(
      api.post('/loan-applications/simulate', {
        productId: 'prod-001',
        amount: '999999.00',
        term: 12,
        frequency: 'MENSUAL',
      }),
    )
    expect(error.status).toBe(400)
    expect(error.fieldErrors.Amount?.[0]).toContain('entre')
  })

  it('el dictamen exige justificación y no se puede repetir', async () => {
    const api = await admin()
    const empty = await failure(
      api.post('/loan-applications/app-007/decision', { decision: 'RECHAZADO', notes: '' }),
    )
    expect(empty.status).toBe(422)
    await api.post('/loan-applications/app-007/decision', {
      decision: 'APROBADO',
      notes: 'Cumple política',
    })
    const again = await failure(
      api.post('/loan-applications/app-007/decision', { decision: 'RECHAZADO', notes: 'x' }),
    )
    expect(again.status).toBe(409)
  })
})

describe('desembolso digital', () => {
  // c4 (José Mendoza) tiene un CCI terminado en 0000: el banco simulado lo rechaza.
  const failing = {
    applicationId: 'app-006',
    customerBankAccountId: 'cba-005',
    companyBankAccountId: 'bca-001',
  }

  it('bloquea el desembolso si el KYC no está verificado (422)', async () => {
    const api = await admin()
    const created = await api.post('/loan-applications', {
      customerId: 'cus-005',
      productId: 'prod-001',
      amount: '1500.00',
      term: 6,
      frequency: 'MENSUAL',
    })
    await api.post(`/loan-applications/${created.data.id}/decision`, {
      decision: 'APROBADO',
      notes: 'ok',
    })
    const error = await failure(
      api.post('/loans', {
        applicationId: created.data.id,
        customerBankAccountId: 'cba-009',
        companyBankAccountId: 'bca-001',
      }),
    )
    expect(error.status).toBe(422)
  })

  it('es idempotente: la misma clave no crea otro préstamo', async () => {
    const api = await admin()
    const headers = { 'Idempotency-Key': 'clave-1' }
    const first = await api.post('/loans', failing, { headers })
    const second = await api.post('/loans', failing, { headers })
    expect(second.data.loan.id).toBe(first.data.loan.id)
    const loans = (await api.get<Page<Loan>>('/loans')).data.items
    expect(loans.filter((loan) => loan.applicationId === 'app-006')).toHaveLength(1)
  })

  it('avanza PENDING → PROCESSING → FAILED y no genera cronograma si el banco rechaza', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    const api = await admin()
    const { data } = await api.post('/loans', failing)
    expect(data.disbursement.status).toBe('PENDING')

    vi.setSystemTime(Date.now() + 3_000)
    expect((await api.get(`/disbursements/${data.disbursement.id}`)).data.status).toBe('PROCESSING')

    vi.setSystemTime(Date.now() + 5_000)
    const done = (await api.get(`/disbursements/${data.disbursement.id}`)).data
    expect(done.status).toBe('FAILED')
    expect(done.bankResponseMessage).toContain('Cuenta destino')
    expect((await api.get(`/loans/${data.loan.id}/schedule`)).data.installments).toHaveLength(0)
  })

  it('con una cuenta válida se completa y genera el cronograma', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    const api = await admin()
    const app = await api.post('/loan-applications', {
      customerId: 'cus-001',
      productId: 'prod-001',
      amount: '3000.00',
      term: 6,
      frequency: 'MENSUAL',
    })
    await api.post(`/loan-applications/${app.data.id}/decision`, { decision: 'APROBADO', notes: 'ok' })
    const { data } = await api.post('/loans', {
      applicationId: app.data.id,
      customerBankAccountId: 'cba-001',
      companyBankAccountId: 'bca-001',
    })

    vi.setSystemTime(Date.now() + 7_000)
    expect((await api.get(`/disbursements/${data.disbursement.id}`)).data.status).toBe('COMPLETED')
    expect((await api.get<Loan>(`/loans/${data.loan.id}`)).data.status).toBe('VIGENTE')
    expect((await api.get(`/loans/${data.loan.id}/schedule`)).data.installments).toHaveLength(6)
    expect((await api.get(`/loan-applications/${app.data.id}`)).data.status).toBe('DESEMBOLSADO')
  })
})

describe('pagos', () => {
  it('aplica la prelación: primero mora e interés, después capital', async () => {
    const api = await admin()
    const first = (await api.get('/loans/loan-002/schedule')).data.installments[0]
    expect(Number(first.lateFee)).toBeGreaterThan(0)

    const { data } = await api.post<Payment>('/loans/loan-002/payments', {
      amount: '60.00',
      method: 'YAPE',
      externalReference: 'OP-1',
    })
    const [allocation] = data.allocations
    expect(Number(allocation?.toMora)).toBeGreaterThan(0)
    expect(Number(allocation?.toPrincipal)).toBe(0)
  })

  it('el reverso crea un contra-asiento y no se puede repetir (409)', async () => {
    const api = await admin()
    const paid = await api.post<Payment>('/loans/loan-002/payments', {
      amount: '100.00',
      method: 'YAPE',
      externalReference: 'OP-2',
    })
    const reversal = await api.post<Payment>(`/payments/${paid.data.id}/reverse`, {
      reason: 'Conciliado con el préstamo equivocado',
    })
    expect(reversal.data.kind).toBe('REVERSAL')
    expect((await api.get<Payment>(`/payments/${paid.data.id}`)).data.status).toBe('REVERSED')
    const again = await failure(api.post(`/payments/${paid.data.id}/reverse`, { reason: 'otra vez' }))
    expect(again.status).toBe(409)
  })

  it('un voucher queda en validación hasta conciliar con el extracto', async () => {
    const api = await admin()
    const pending = (await api.get<Page<Payment>>('/payments?status=PENDING_CONFIRMATION')).data.items
    expect(pending).toHaveLength(1)
    const imported = await api.post('/reconciliation/import', {
      lines: [
        {
          operationNumber: 'EXT-777',
          valueDate: '2026-11-05',
          amount: pending[0]!.amount,
          description: 'TRANSF OP-445566 JUAN',
        },
      ],
    })
    expect(imported.data.matched).toBe(1)
    expect((await api.get<Payment>(`/payments/${pending[0]!.id}`)).data.status).toBe('APPLIED')
  })

  it('el pago con QR del portal se confirma solo (webhook simulado)', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    const portal = await portalClient()
    const { data } = await portal.post('/client/payments', {
      loanId: 'loan-001',
      amount: '250.00',
      method: 'YAPE',
    })
    expect(data.status).toBe('PENDING_CONFIRMATION')
    expect(data.qrPayload).toBeTruthy()

    vi.setSystemTime(Date.now() + 6_000)
    const confirmed = (await portal.get(`/client/payments/${data.paymentId}`)).data
    expect(confirmed.status).toBe('APPLIED')
    expect(confirmed.receiptNumber).toMatch(/^REC-\d{8}-\d{6}$/)
  })
})

describe('portal del cliente', () => {
  it('un cliente no puede ver el préstamo de otro (404)', async () => {
    const portal = await portalClient()
    expect((await portal.get('/client/loans/loan-001')).status).toBe(200)
    expect((await failure(portal.get('/client/loans/loan-002'))).status).toBe(404)
  })

  it('el backoffice no está abierto al rol cliente (403)', async () => {
    const portal = await portalClient()
    expect((await failure(portal.get('/customers'))).status).toBe(403)
  })
})

describe('webhooks', () => {
  it('exige firma y deduplica por proveedor + id externo', async () => {
    const api = client()
    expect((await failure(api.post('/integrations/webhooks/yape', { externalId: 'A1' }))).status).toBe(401)
    const headers = { 'X-Signature': 'sha256=abc' }
    const first = await api.post('/integrations/webhooks/yape', { externalId: 'A1' }, { headers })
    const second = await api.post('/integrations/webhooks/yape', { externalId: 'A1' }, { headers })
    expect(first.data.duplicate).toBe(false)
    expect(second.data.duplicate).toBe(true)
  })
})
