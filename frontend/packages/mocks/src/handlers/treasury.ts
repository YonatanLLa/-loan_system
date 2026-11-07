import { http, HttpResponse } from 'msw'
import type {
  BankStatementLine,
  CompanyBankAccount,
  CreateInternalTransferRequest,
  DailyBalance,
  InternalAccountTransfer,
} from '@prestamos/api-client'

import { today } from '../dates'
import { db, nextId, nowIso, recordAudit } from '../db'
import { applyPaymentCascade } from '../domain'
import { notFound, paginate, problem, readJson, secured, wait } from '../http-utils'
import { fromCents, sum, toCents } from '../money'

export const treasuryHandlers = [
  http.get('*/api/treasury/bank-accounts', secured('treasury.read', () => HttpResponse.json(db.companyAccounts))),

  http.post('*/api/treasury/bank-accounts', secured('admin.manage', async ({ request, user }) => {
    const body = await readJson<Omit<CompanyBankAccount, 'id' | 'isActive'>>(request)
    if (!body?.bankName?.trim()) return problem(400, 'Datos inválidos', 'Indica el banco.', { BankName: ['El banco es obligatorio'] })
    if (body.cci && !/^\d{20}$/.test(body.cci)) return problem(400, 'Datos inválidos', 'El CCI tiene 20 dígitos.', { Cci: ['El CCI tiene 20 dígitos'] })
    const account: CompanyBankAccount = { ...body, id: nextId('bca'), isActive: true }
    db.companyAccounts.push(account)
    recordAudit({ entityName: 'company_bank_accounts', entityId: account.id, action: 'CREATE', after: { bank: account.bankName }, userName: user.fullName })
    return HttpResponse.json(account, { status: 201 })
  })),

  http.patch('*/api/treasury/bank-accounts/:id', secured<{ id: string }>('admin.manage', async ({ request, params }) => {
    const account = db.companyAccounts.find((a) => a.id === params.id)
    if (!account) return notFound('La cuenta')
    const body = await readJson<Partial<CompanyBankAccount>>(request)
    if (body?.isActive !== undefined) account.isActive = body.isActive
    if (body?.isDisbursement !== undefined) account.isDisbursement = body.isDisbursement
    if (body?.isCollection !== undefined) account.isCollection = body.isCollection
    return HttpResponse.json(account)
  })),

  http.get('*/api/treasury/disbursements', secured('treasury.read', ({ url }) => {
    const status = url.searchParams.get('status')
    const items = db.disbursements
      .filter((d) => !status || status.split(',').includes(d.status))
      .sort((a, b) => b.initiatedAt.localeCompare(a.initiatedAt))
      .map(({ createdMs: _createdMs, ...dto }) => dto)
    return HttpResponse.json(paginate(items, url))
  })),

  http.get('*/api/disbursements/:id', secured<{ id: string }>('treasury.read', ({ params }) => {
    const found = db.disbursements.find((d) => d.id === params.id)
    if (!found) return notFound('El desembolso')
    const { createdMs: _createdMs, ...dto } = found
    return HttpResponse.json(dto)
  })),

  // Cuadre diario (US-24): libro mayor vs. extracto bancario de la cuenta.
  http.get('*/api/treasury/daily-balance', secured('treasury.read', ({ url }) => {
    const bankAccountId = url.searchParams.get('bankAccountId') ?? 'bca-001'
    const date = url.searchParams.get('date') ?? today()
    if (!db.companyAccounts.some((a) => a.id === bankAccountId)) return notFound('La cuenta')

    const collected = sum(
      db.payments
        .filter((p) => p.companyBankAccountId === bankAccountId && p.kind === 'NORMAL' && p.status === 'APPLIED' && p.paidAt.startsWith(date))
        .map((p) => p.amount),
    )
    const disbursed = sum(
      db.disbursements
        .filter((d) => d.companyBankAccountId === bankAccountId && d.status === 'COMPLETED' && d.confirmedAt?.startsWith(date))
        .map((d) => toCents(d.amount)),
    )
    const transfersIn = sum(
      db.internalTransfers
        .filter((t) => t.destinationBankAccountId === bankAccountId && t.transferDate === date)
        .map((t) => toCents(t.amount)),
    )
    const transfersOut = sum(
      db.internalTransfers
        .filter((t) => t.sourceBankAccountId === bankAccountId && t.transferDate === date)
        .map((t) => toCents(t.amount)),
    )
    const lines = db.statements.filter((s) => s.companyBankAccountId === bankAccountId && s.valueDate === date)
    const statement = sum(lines.map((l) => toCents(l.amount)))
    const expected = collected - disbursed + transfersIn - transfersOut
    const issues = lines
      .filter((l) => l.reconciliationStatus !== 'CONCILIADO_AUTOMATICO')
      .map((l) => `${l.operationNumber}: ${l.description}`)
    const result: DailyBalance = {
      bankAccountId,
      date,
      expectedBalance: fromCents(expected),
      statementBalance: fromCents(statement),
      difference: fromCents(statement - expected),
      result: statement === expected && issues.length === 0 ? 'CUADRADO' : 'DESCUADRADO',
      issues,
      internalTransfersIn: fromCents(transfersIn),
      internalTransfersOut: fromCents(transfersOut),
    }
    return HttpResponse.json(result)
  })),

  // Transferencias internas entre cuentas propias de la empresa (Fondeo / Barrido de Tesorería — RF-24.5, RF-24.6).
  http.get('*/api/treasury/internal-transfers', secured('treasury.read', ({ url }) => {
    const items = [...db.internalTransfers].sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    return HttpResponse.json(paginate(items, url))
  })),

  http.post('*/api/treasury/internal-transfers', secured('treasury.read', async ({ request, user }) => {
    const body = await readJson<CreateInternalTransferRequest>(request)
    if (!body?.sourceBankAccountId || !body?.destinationBankAccountId) {
      return problem(400, 'Datos inválidos', 'Debes seleccionar la cuenta de origen y la cuenta de destino.')
    }
    if (body.sourceBankAccountId === body.destinationBankAccountId) {
      return problem(422, 'Cuentas idénticas', 'La cuenta de origen y la cuenta de destino deben ser distintas.')
    }
    const source = db.companyAccounts.find((a) => a.id === body.sourceBankAccountId)
    const destination = db.companyAccounts.find((a) => a.id === body.destinationBankAccountId)
    if (!source || !destination) return notFound('Una de las cuentas corporativas')
    if (!source.isActive || !destination.isActive) {
      return problem(422, 'Cuenta inactiva', 'Ambas cuentas corporativas deben estar activas para transferir fondos.')
    }
    if (source.currency !== destination.currency) {
      return problem(422, 'Monedas distintas', 'Las transferencias internas directas requieren cuentas en la misma moneda.')
    }
    const cents = toCents(body.amount)
    if (cents <= 0) {
      return problem(400, 'Monto inválido', 'El monto de la transferencia debe ser mayor a cero.')
    }
    if (!body.bankOperationNumber?.trim()) {
      return problem(400, 'Falta número de operación', 'Ingresa el número de operación bancaria del fondeo.')
    }

    const opNum = body.bankOperationNumber.trim()
    const transferDate = body.transferDate || today()
    const transfer: InternalAccountTransfer = {
      id: nextId('itr'),
      sourceBankAccountId: source.id,
      sourceBankName: `${source.bankName} (${source.accountNumberMasked})`,
      destinationBankAccountId: destination.id,
      destinationBankName: `${destination.bankName} (${destination.accountNumberMasked})`,
      currency: source.currency,
      amount: fromCents(cents),
      transferDate,
      bankOperationNumber: opNum,
      notes: body.notes?.trim() || undefined,
      createdBy: user.fullName,
      createdAt: nowIso(),
    }
    db.internalTransfers.unshift(transfer)

    // Asientos simétricos en extractos para mantener el cuadre diario consistente (RF-24.6)
    db.statements.unshift(
      {
        id: nextId('stm'),
        companyBankAccountId: source.id,
        operationNumber: opNum,
        valueDate: transferDate,
        amount: `-${fromCents(cents)}`,
        description: `FONDEO ENVIADO A ${destination.bankName.toUpperCase()} (${destination.accountNumberMasked})`,
        reconciliationStatus: 'CONCILIADO_AUTOMATICO',
      },
      {
        id: nextId('stm'),
        companyBankAccountId: destination.id,
        operationNumber: opNum,
        valueDate: transferDate,
        amount: fromCents(cents),
        description: `FONDEO RECIBIDO DESDE ${source.bankName.toUpperCase()} (${source.accountNumberMasked})`,
        reconciliationStatus: 'CONCILIADO_AUTOMATICO',
      },
    )

    recordAudit({
      entityName: 'internal_account_transfers',
      entityId: transfer.id,
      action: 'CREATE',
      after: {
        source: transfer.sourceBankName,
        destination: transfer.destinationBankName,
        amount: transfer.amount,
        operationNumber: transfer.bankOperationNumber,
      },
      userName: user.fullName,
    })

    return HttpResponse.json(transfer, { status: 201 })
  })),

  http.get('*/api/reconciliation', secured('treasury.read', ({ url }) => {
    const status = url.searchParams.get('status')
    const items = db.statements
      .filter((s) => !status || s.reconciliationStatus === status)
      .sort((a, b) => b.valueDate.localeCompare(a.valueDate))
    return HttpResponse.json(paginate(items, url))
  })),

  // Importar extracto (US-37): empareja por referencia + monto con los pagos pendientes.
  http.post('*/api/reconciliation/import', secured('treasury.read', async ({ request }) => {
    const body = await readJson<{
      bankAccountId?: string
      lines?: Pick<BankStatementLine, 'operationNumber' | 'valueDate' | 'amount' | 'description'>[]
    }>(request)
    const accountId = body?.bankAccountId ?? 'bca-001'
    const lines = body?.lines ?? [
      { operationNumber: `EXT-${Date.now() % 1_000_000}`, valueDate: today(), amount: '100.00', description: 'ABONO SIN REFERENCIA' },
    ]
    let matched = 0
    let pending = 0
    let duplicated = 0
    for (const line of lines) {
      if (db.statements.some((s) => s.companyBankAccountId === accountId && s.operationNumber === line.operationNumber)) {
        duplicated += 1
        continue
      }
      const candidate = db.payments.find(
        (p) =>
          p.status === 'PENDING_CONFIRMATION' &&
          p.externalReference &&
          line.description.includes(p.externalReference) &&
          p.amount === toCents(line.amount),
      )
      const record: BankStatementLine = {
        id: nextId('stm'),
        companyBankAccountId: accountId,
        ...line,
        reconciliationStatus: candidate ? 'CONCILIADO_AUTOMATICO' : 'PENDIENTE_CONCILIACION',
        reconciledPaymentId: candidate?.id,
      }
      db.statements.unshift(record)
      if (candidate) {
        applyPaymentCascade(candidate)
        matched += 1
      } else {
        pending += 1
      }
    }
    await wait()
    return HttpResponse.json({ imported: lines.length - duplicated, matched, pending, duplicated }, { status: 201 })
  })),

  // Conciliación manual de un abono no identificado con un pago pendiente.
  http.post('*/api/reconciliation/:id/match', secured<{ id: string }>('treasury.read', async ({ request, params }) => {
    const line = db.statements.find((s) => s.id === params.id)
    if (!line) return notFound('El movimiento')
    const body = await readJson<{ paymentId: string }>(request)
    const payment = db.payments.find((p) => p.id === body?.paymentId)
    if (!payment) return notFound('El pago')
    if (payment.status !== 'PENDING_CONFIRMATION') {
      return problem(409, 'Estado inválido', 'El pago no está pendiente de confirmación.')
    }
    if (payment.amount !== toCents(line.amount)) {
      line.reconciliationStatus = 'DESCUADRADO'
      return problem(422, 'Montos distintos', 'El monto del movimiento no coincide con el del pago.')
    }
    applyPaymentCascade(payment)
    line.reconciliationStatus = 'CONCILIADO_AUTOMATICO'
    line.reconciledPaymentId = payment.id
    return HttpResponse.json(line)
  })),

  // Integraciones (US-36, US-38).
  http.get('*/api/integrations/events', secured('integrations.read', ({ url }) => {
    const status = url.searchParams.get('status')
    const items = db.events
      .filter((e) => !status || status.split(',').includes(e.status))
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    return HttpResponse.json(paginate(items, url))
  })),

  http.post('*/api/integrations/events/:id/retry', secured<{ id: string }>('integrations.read', ({ params }) => {
    const event = db.events.find((e) => e.id === params.id)
    if (!event) return notFound('El evento')
    if (event.status !== 'DEAD_LETTER' && event.status !== 'FAILED' && event.status !== 'FAILED_NEEDS_REVIEW') {
      return problem(409, 'Estado inválido', 'Solo se reintentan eventos fallidos.')
    }
    event.status = 'PROCESSED'
    event.errorDetail = undefined
    event.retryCount += 1
    return HttpResponse.json(event)
  })),

  // Receptor de webhooks: exige firma, deduplica por (provider, externalId) y responde 200.
  http.post('*/api/integrations/webhooks/:provider', async ({ request, params }) => {
    await wait()
    if (!request.headers.get('X-Signature')) {
      return problem(401, 'Firma inválida', 'Falta la cabecera X-Signature.')
    }
    const body = await readJson<{ externalId?: string; eventType?: string }>(request)
    const provider = String(params.provider)
    const externalId = body?.externalId ?? `EXT-${Date.now()}`
    const duplicate = db.events.some((e) => e.provider === provider && e.externalId === externalId)
    if (!duplicate) {
      db.events.unshift({
        id: nextId('evt'),
        provider,
        eventType: body?.eventType ?? 'payment.confirmed',
        externalId,
        status: 'RECEIVED',
        retryCount: 0,
        createdAt: nowIso(),
      })
    }
    return HttpResponse.json({ status: 'ACK', duplicate })
  }),
]
