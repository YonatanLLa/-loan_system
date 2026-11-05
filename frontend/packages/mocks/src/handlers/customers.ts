import { http, HttpResponse } from 'msw'
import type {
  CreateCustomerRequest,
  Customer,
  Customer360,
  CustomerBankAccount,
  KycStatus,
} from '@prestamos/api-client'

import { db, nextId, nowIso, recordAudit } from '../db'
import {
  activeInstallments,
  customerLoans,
  maxDaysLate,
  toApplicationSummary,
  toLoanSummary,
} from '../domain'
import { matches, notFound, paginate, problem, readJson, secured } from '../http-utils'
import { fromCents, sum } from '../money'

const DOC_PATTERNS: Record<Customer['docType'], RegExp> = {
  DNI: /^\d{8}$/,
  CE: /^\d{9}$/,
  RUC: /^\d{11}$/,
  PASAPORTE: /^[A-Z0-9]{6,12}$/,
}

function totalDebt(customerId: string): number {
  return sum(
    customerLoans(customerId)
      .filter((loan) => loan.status === 'VIGENTE' || loan.status === 'EN_MORA')
      .flatMap((loan) => activeInstallments(loan))
      .filter((inst) => inst.status !== 'PAID' && inst.status !== 'CANCELLED_BY_PREPAYMENT')
      .map(
        (inst) =>
          inst.totalDue + inst.lateFee - inst.paidPrincipal - inst.paidInterest - inst.paidFee - inst.paidLateFee,
      ),
  )
}

function to360(customer: Customer): Customer360 {
  const loans = customerLoans(customer.id)
  const days = Math.max(0, ...loans.map((loan) => maxDaysLate(loan)))
  return {
    ...customer,
    bankAccounts: db.customerBankAccounts.filter((a) => a.customerId === customer.id),
    loans: loans.map(toLoanSummary),
    applications: db.applications
      .filter((app) => app.customerId === customer.id)
      .map(toApplicationSummary),
    totalDebt: fromCents(totalDebt(customer.id)),
    maxDaysLate: days,
    hasActiveArrears: loans.some((loan) => loan.status === 'EN_MORA'),
  }
}

export const customerHandlers = [
  http.get('*/api/customers', secured('customers.read', ({ url }) => {
    const q = url.searchParams.get('q')
    const kyc = url.searchParams.get('kycStatus')
    const items = db.customers
      .filter((c) => matches(`${c.fullName} ${c.docNumber} ${c.email}`, q))
      .filter((c) => !kyc || c.kycStatus === kyc)
    return HttpResponse.json(paginate(items, url))
  })),

  http.get('*/api/customers/:id', secured<{ id: string }>('customers.read', ({ params }) => {
    const customer = db.customers.find((c) => c.id === params.id)
    return customer ? HttpResponse.json(to360(customer)) : notFound('El cliente')
  })),

  http.post('*/api/customers', secured('customers.read', async ({ request, user }) => {
    const body = await readJson<CreateCustomerRequest>(request)
    if (!body) return problem(400, 'Solicitud inválida', 'Cuerpo inválido.')
    const errors: Record<string, string[]> = {}
    if (!DOC_PATTERNS[body.docType]?.test(body.docNumber)) {
      errors.DocNumber = [`Formato inválido para ${body.docType}`]
    }
    if (!body.fullName?.trim()) errors.FullName = ['El nombre es obligatorio']
    if (!/^9\d{8}$/.test(body.phone ?? '')) errors.Phone = ['El celular tiene 9 dígitos y empieza con 9']
    if (Object.keys(errors).length > 0) return problem(400, 'Datos inválidos', 'Revisa los campos.', errors)
    if (db.customers.some((c) => c.docType === body.docType && c.docNumber === body.docNumber)) {
      return problem(409, 'Documento duplicado', `Ya existe un cliente con ${body.docType} ${body.docNumber}.`)
    }
    const customer: Customer = {
      id: nextId('cus'),
      personType: body.personType ?? 'NATURAL',
      docType: body.docType,
      docNumber: body.docNumber,
      fullName: body.fullName,
      email: body.email,
      phone: body.phone,
      address: body.address ?? '',
      district: body.district ?? '',
      birthDate: body.birthDate,
      monthlyNetIncome: body.monthlyNetIncome,
      occupation: body.occupation ?? '',
      employerName: body.employerName ?? '',
      workSeniorityMonths: body.workSeniorityMonths ?? 0,
      kycStatus: 'PENDING',
      creditBalance: '0.00',
      createdAt: nowIso(),
    }
    db.customers.unshift(customer)
    recordAudit({ entityName: 'customers', entityId: customer.id, action: 'CREATE', after: { docNumber: customer.docNumber }, userName: user.fullName })
    return HttpResponse.json(customer, { status: 201 })
  })),

  http.put('*/api/customers/:id', secured<{ id: string }>('customers.read', async ({ request, params, user }) => {
    const customer = db.customers.find((c) => c.id === params.id)
    if (!customer) return notFound('El cliente')
    const body = await readJson<Partial<CreateCustomerRequest>>(request)
    if (!body) return problem(400, 'Solicitud inválida', 'Cuerpo inválido.')
    const before = { ...customer }
    if (body.fullName !== undefined) customer.fullName = body.fullName
    if (body.email !== undefined) customer.email = body.email
    if (body.phone !== undefined) customer.phone = body.phone
    if (body.address !== undefined) customer.address = body.address
    if (body.district !== undefined) customer.district = body.district
    if (body.monthlyNetIncome !== undefined) customer.monthlyNetIncome = body.monthlyNetIncome
    if (body.occupation !== undefined) customer.occupation = body.occupation
    if (body.employerName !== undefined) customer.employerName = body.employerName
    if (body.workSeniorityMonths !== undefined) customer.workSeniorityMonths = body.workSeniorityMonths
    recordAudit({ entityName: 'customers', entityId: customer.id, action: 'UPDATE', before, after: { id: customer.id }, userName: user.fullName })
    return HttpResponse.json(customer)
  })),

  // Aprobar o rechazar el KYC (US-05). Endpoint propuesto: no figura aún en las historias.
  http.patch('*/api/customers/:id/kyc', secured<{ id: string }>('customers.read', async ({ request, params, user }) => {
    const customer = db.customers.find((c) => c.id === params.id)
    if (!customer) return notFound('El cliente')
    const body = await readJson<{ status: KycStatus; notes?: string }>(request)
    if (!body?.status) return problem(400, 'Solicitud inválida', 'Indica el estado del KYC.')
    if (body.status === 'REJECTED' && !body.notes?.trim()) {
      return problem(422, 'Falta la justificación', 'Para rechazar el KYC indica el motivo.')
    }
    const before = { kycStatus: customer.kycStatus }
    customer.kycStatus = body.status
    customer.kycNotes = body.notes
    customer.kycVerifiedAt = body.status === 'VERIFIED' ? nowIso() : undefined
    db.customerBankAccounts
      .filter((a) => a.customerId === customer.id && a.isPrimary)
      .forEach((a) => {
        a.isVerified = body.status === 'VERIFIED'
        a.status = body.status === 'VERIFIED' ? 'ACTIVE' : 'PENDING_VERIFICATION'
      })
    recordAudit({ entityName: 'customers', entityId: customer.id, action: 'APPROVE', before, after: { kycStatus: body.status }, userName: user.fullName })
    return HttpResponse.json(customer)
  })),

  http.get('*/api/customers/:id/bank-accounts', secured<{ id: string }>('customers.read', ({ params }) =>
    HttpResponse.json(db.customerBankAccounts.filter((a) => a.customerId === params.id)),
  )),

  http.post('*/api/customers/:id/bank-accounts', secured<{ id: string }>('customers.read', async ({ request, params }) => {
    const customer = db.customers.find((c) => c.id === params.id)
    if (!customer) return notFound('El cliente')
    const body = await readJson<Partial<CustomerBankAccount> & { accountNumber?: string }>(request)
    if (!body?.bankName) return problem(400, 'Datos inválidos', 'Indica el banco.', { BankName: ['El banco es obligatorio'] })
    if (body.cci && !/^\d{20}$/.test(body.cci)) {
      return problem(400, 'Datos inválidos', 'El CCI tiene 20 dígitos.', { Cci: ['El CCI tiene 20 dígitos'] })
    }
    const account: CustomerBankAccount = {
      id: nextId('cba'),
      customerId: customer.id,
      bankName: body.bankName,
      accountType: body.accountType ?? 'CUENTA_AHORRO',
      accountNumberMasked: `**** ${(body.cci ?? body.accountNumber ?? '0000').slice(-4)}`,
      cci: body.cci,
      phoneWallet: body.phoneWallet,
      isVerified: false,
      isPrimary: false,
      status: 'PENDING_VERIFICATION',
    }
    db.customerBankAccounts.push(account)
    return HttpResponse.json(account, { status: 201 })
  })),

  http.post('*/api/customer-bank-accounts/:id/verify', secured<{ id: string }>('customers.read', ({ params }) => {
    const account = db.customerBankAccounts.find((a) => a.id === params.id)
    if (!account) return notFound('La cuenta')
    account.isVerified = true
    account.status = 'ACTIVE'
    return HttpResponse.json(account)
  })),

  http.post('*/api/customer-bank-accounts/:id/make-primary', secured<{ id: string }>('customers.read', ({ params }) => {
    const account = db.customerBankAccounts.find((a) => a.id === params.id)
    if (!account) return notFound('La cuenta')
    db.customerBankAccounts
      .filter((a) => a.customerId === account.customerId)
      .forEach((a) => { a.isPrimary = a.id === account.id })
    return HttpResponse.json(account)
  })),
]
