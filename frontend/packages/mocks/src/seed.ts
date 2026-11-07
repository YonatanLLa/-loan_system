import type {
  Customer,
  CustomerBankAccount,
  LoanApplication,
  LoanProduct,
  PaymentFrequency,
} from '@prestamos/api-client'

import { addDays, addMonths, today } from './dates'
import { db, createEmptyDb, nextId, nowIso, recordAudit, type LoanRec, type PaymentRec } from './db'
import {
  activateLoan,
  applyPaymentCascade,
  activeInstallments,
  customerName,
  evaluateDti,
  refreshLoan,
} from './domain'
import { fromCents, roundHalfUp, toCents } from './money'

/** Contraseña de todos los usuarios de la API simulada. */
export const DEMO_PASSWORD = 'Demo1234!'

const ALL_BACKOFFICE = [
  'dashboard.view',
  'customers.read',
  'applications.read',
  'loans.read',
  'payments.read',
  'payments.reverse',
  'treasury.read',
  'integrations.read',
  'collections.read',
  'reports.read',
  'audit.read',
  'products.manage',
  'admin.manage',
]

/** CCI de 20 dígitos. Si `fail`, termina en 0000: el banco simulado lo rechaza al desembolsar. */
const cci = (n: number, fail = false): string =>
  fail ? `0021900000000${String(n).padStart(3, '0')}0000` : `00219${String(n).padStart(15, '0')}`

function seedUsers(): void {
  const users = [
    ['usr-001', 'admin@sistema.local', 'Administrador del Sistema', 'ROLE_ADMIN', ALL_BACKOFFICE],
    ['usr-002', 'analista@sistema.local', 'Andrea Salazar (Analista)', 'ROLE_ANALYST',
      ['dashboard.view', 'customers.read', 'applications.read', 'loans.read', 'reports.read']],
    ['usr-003', 'operador@sistema.local', 'Omar Delgado (Operador)', 'ROLE_OPERATOR',
      ['dashboard.view', 'customers.read', 'applications.read', 'loans.read', 'payments.read',
        'treasury.read', 'integrations.read', 'reports.read']],
    ['usr-004', 'cobranza@sistema.local', 'Carla Núñez (Cobranza)', 'ROLE_COLLECTOR',
      ['dashboard.view', 'customers.read', 'loans.read', 'collections.read']],
    ['usr-005', 'auditor@sistema.local', 'Aldo Vargas (Auditor)', 'ROLE_AUDITOR',
      ['dashboard.view', 'audit.read', 'reports.read', 'customers.read', 'loans.read',
        'payments.read', 'treasury.read']],
  ] as const
  for (const [id, email, fullName, role, permissions] of users) {
    db.users.push({ id, email, password: DEMO_PASSWORD, fullName, role, permissions: [...permissions] })
  }
  db.users.push({
    id: 'usr-100',
    email: 'cliente@correo.pe',
    password: DEMO_PASSWORD,
    fullName: 'María Quispe Huamán',
    role: 'ROLE_CLIENT',
    permissions: ['client.portal'],
    customerId: 'cus-001',
  })
  db.users.push({
    id: 'usr-101',
    email: 'nuevo@correo.pe',
    password: DEMO_PASSWORD,
    fullName: 'Juan Pérez Alva',
    role: 'ROLE_CLIENT',
    permissions: ['client.portal'],
    customerId: 'cus-009',
  })
}

function seedCatalogs(): void {
  db.company = {
    legalName: 'FinCredit Perú S.A.C.',
    tradeName: 'FinCredit',
    taxId: '20601234567',
    legalAddress: 'Av. Javier Prado Este 1234, San Isidro, Lima',
    supportPhone: '016001234',
    supportEmail: 'soporte@fincredit.pe',
    legalRepName: 'Ricardo Salinas Vera',
    defaultCurrency: 'PEN',
  }
  db.holidays = [
    ['2026-01-01', 'Año Nuevo'], ['2026-04-02', 'Jueves Santo'], ['2026-04-03', 'Viernes Santo'],
    ['2026-05-01', 'Día del Trabajo'], ['2026-06-29', 'San Pedro y San Pablo'],
    ['2026-07-28', 'Fiestas Patrias — Independencia'], ['2026-07-29', 'Fiestas Patrias — Gran Parada'],
    ['2026-08-30', 'Santa Rosa de Lima'], ['2026-10-08', 'Combate de Angamos'],
    ['2026-11-01', 'Todos los Santos'], ['2026-12-08', 'Inmaculada Concepción'],
    ['2026-12-25', 'Navidad'],
  ].map(([date, name], index) => ({
    id: `hol-${String(index + 1).padStart(3, '0')}`,
    date: date as string,
    name: name as string,
    isRecurring: true,
  }))
  db.counters.set('hol', db.holidays.length)

  db.paymentMethods = [
    ['pm-001', 'Transferencia bancaria (CCI)', 'TRANSFERENCIA_BANCARIA', false, true],
    ['pm-002', 'Yape', 'YAPE', true, true],
    ['pm-003', 'Plin', 'PLIN', true, true],
    ['pm-004', 'Tarjeta de débito', 'TARJETA_DEBITO', true, true],
    ['pm-005', 'Tarjeta de crédito', 'TARJETA_CREDITO', true, false],
    ['pm-006', 'Pasarela de pago', 'PASARELA_PAGO', true, true],
  ].map(([id, name, methodType, webhookEnabled, isActive]) => ({
    id, name, methodType, webhookEnabled, isActive,
  })) as typeof db.paymentMethods

  db.companyAccounts = [
    { id: 'bca-001', bankName: 'BCP', accountNumberMasked: '**** 4410', cci: cci(9001), currency: 'PEN',
      isDisbursement: true, isCollection: true, isActive: true },
    { id: 'bca-002', bankName: 'Interbank', accountNumberMasked: '**** 7782', cci: cci(9002), currency: 'PEN',
      isDisbursement: false, isCollection: true, isActive: true },
    { id: 'bca-003', bankName: 'BBVA', accountNumberMasked: '**** 1093', cci: cci(9003), currency: 'USD',
      isDisbursement: false, isCollection: true, isActive: false },
  ]

  const base = {
    currency: 'PEN' as const,
    allowedFrequencies: ['MENSUAL'] as PaymentFrequency[],
    openingFeeRate: '0.000000',
    openingFeeMode: 'DEDUCTIVA' as const,
    periodicFee: '0.00',
    graceDays: 3,
    moraBase: 'SALDO_CAPITAL_VENCIDO' as const,
    prepaymentPenaltyRate: '0.000000',
    minAge: 18,
    maxAge: 70,
    minMonthlyIncome: '1000.00',
    maxDtiAuto: '0.350000',
    maxDtiManual: '0.500000',
    status: 'ACTIVO' as const,
  }
  const products: LoanProduct[] = [
    { ...base, id: 'prod-001', name: 'Crédito Personal Digital',
      description: 'Cuota nivelada para consumo, 100 % en línea.',
      minAmount: '500.00', maxAmount: '20000.00', minTerm: 6, maxTerm: 36,
      allowedFrequencies: ['MENSUAL', 'QUINCENAL'], amortizationMethod: 'FRANCES',
      annualRate: '0.350000', openingFeeRate: '0.010000', periodicFee: '2.50',
      moraRatePerDay: '0.001000', minMonthlyIncome: '1200.00' },
    { ...base, id: 'prod-002', name: 'Microcrédito Semanal',
      description: 'Interés simple (flat) con pagos semanales para pequeños negocios.',
      minAmount: '200.00', maxAmount: '5000.00', minTerm: 8, maxTerm: 24,
      allowedFrequencies: ['SEMANAL'], amortizationMethod: 'FLAT', annualRate: '0.600000',
      graceDays: 1, moraRatePerDay: '0.002000', minMonthlyIncome: '800.00' },
    { ...base, id: 'prod-003', name: 'Capital de Trabajo',
      description: 'Amortización alemana (capital constante) para empresas.',
      minAmount: '2000.00', maxAmount: '50000.00', minTerm: 6, maxTerm: 24,
      amortizationMethod: 'ALEMAN', annualRate: '0.280000', openingFeeRate: '0.015000',
      openingFeeMode: 'FINANCIADA', graceDays: 5, moraRatePerDay: '0.000800',
      minMonthlyIncome: '3000.00' },
    { ...base, id: 'prod-004', name: 'Crédito Vehicular (descontinuado)',
      description: 'Producto inactivo, solo para pruebas de listado.',
      minAmount: '10000.00', maxAmount: '60000.00', minTerm: 12, maxTerm: 48,
      amortizationMethod: 'FRANCES', annualRate: '0.220000', moraRatePerDay: '0.000700',
      status: 'INACTIVO' },
  ]
  db.products = products
}

interface CustomerSeed {
  name: string
  doc: string
  docType?: Customer['docType']
  birth: string
  income: string
  occupation: string
  employer: string
  district: string
  kyc: Customer['kycStatus']
  failAccount?: boolean
  juridical?: boolean
}

function seedCustomers(): void {
  const seeds: CustomerSeed[] = [
    { name: 'María Quispe Huamán', doc: '41234567', birth: '1990-03-12', income: '3200.00', occupation: 'Contadora', employer: 'Estudio Contable Lima', district: 'Miraflores', kyc: 'VERIFIED' },
    { name: 'Carlos Ramírez Soto', doc: '42345678', birth: '1985-07-25', income: '2600.00', occupation: 'Técnico electricista', employer: 'Independiente', district: 'San Juan de Lurigancho', kyc: 'VERIFIED' },
    { name: 'Lucía Fernández Paredes', doc: '43456789', birth: '1994-11-02', income: '1800.00', occupation: 'Comerciante', employer: 'Bodega Lucía', district: 'Comas', kyc: 'VERIFIED' },
    { name: 'José Mendoza Rojas', doc: '44567890', birth: '1988-01-30', income: '4100.00', occupation: 'Ingeniero civil', employer: 'Constructora Pacífico', district: 'Surco', kyc: 'VERIFIED', failAccount: true },
    { name: 'Ana Torres Vega', doc: '45678901', birth: '1996-05-18', income: '2100.00', occupation: 'Diseñadora', employer: 'Freelance', district: 'Jesús María', kyc: 'PENDING' },
    { name: 'Pedro Castillo León', doc: '46789012', birth: '1979-09-09', income: '1500.00', occupation: 'Taxista', employer: 'Independiente', district: 'Ate', kyc: 'REJECTED' },
    { name: 'Rosa Chávez Díaz', doc: '47890123', birth: '1982-12-21', income: '2900.00', occupation: 'Docente', employer: 'I.E. San Martín', district: 'Pueblo Libre', kyc: 'VERIFIED' },
    { name: 'Distribuidora Andina S.A.C.', doc: '20512345678', docType: 'RUC', birth: '2010-02-01', income: '18000.00', occupation: 'Distribución de abarrotes', employer: 'Distribuidora Andina S.A.C.', district: 'La Victoria', kyc: 'VERIFIED', juridical: true },
    { name: 'Juan Pérez Alva', doc: '48901234', birth: '1995-06-15', income: '2800.00', occupation: 'Analista comercial', employer: 'Inversiones Lima S.A.C.', district: 'San Borja', kyc: 'VERIFIED' },
  ]
  seeds.forEach((seed, index) => {
    const id = nextId('cus')
    const slug = seed.name.toLowerCase().normalize('NFD').replace(/[^a-z ]/g, '').split(' ')
    db.customers.push({
      id,
      personType: seed.juridical ? 'JURIDICA' : 'NATURAL',
      docType: seed.docType ?? 'DNI',
      docNumber: seed.doc,
      fullName: seed.name,
      email: index === 0 ? 'cliente@correo.pe' : seed.name === 'Juan Pérez Alva' ? 'nuevo@correo.pe' : `${slug[0]}.${slug[1]}@correo.pe`,
      phone: seed.name === 'Juan Pérez Alva' ? '987000101' : `9${String(87000000 + index * 1111111).slice(0, 8)}`,
      address: `Jr. Los Pinos ${100 + index * 17}`,
      district: seed.district,
      birthDate: seed.birth,
      monthlyNetIncome: seed.income,
      occupation: seed.occupation,
      employerName: seed.employer,
      workSeniorityMonths: 18 + index * 7,
      kycStatus: seed.kyc,
      kycVerifiedAt: seed.kyc === 'VERIFIED' ? addDays(today(), -90 + index) + 'T14:00:00Z' : undefined,
      kycNotes: seed.kyc === 'REJECTED' ? 'Documento ilegible; la selfie no coincide con el DNI.' : undefined,
      creditBalance: index === 0 ? '350.00' : '0.00',
      createdAt: addDays(today(), -120 + index * 3) + 'T10:00:00Z',
    })
    const account: CustomerBankAccount = {
      id: nextId('cba'),
      customerId: id,
      bankName: index % 2 === 0 ? 'BCP' : 'Interbank',
      accountType: 'CUENTA_AHORRO',
      accountNumberMasked: `**** ${String(4000 + index * 137).slice(-4)}`,
      cci: cci(index + 1, seed.failAccount),
      isVerified: seed.kyc === 'VERIFIED',
      isPrimary: true,
      status: seed.kyc === 'VERIFIED' ? 'ACTIVE' : 'PENDING_VERIFICATION',
    }
    db.customerBankAccounts.push(account)
    if (index === 0) {
      db.customerBankAccounts.push({
        id: nextId('cba'),
        customerId: id,
        bankName: 'Yape',
        accountType: 'BILLETERA_DIGITAL',
        accountNumberMasked: '**** 4567',
        phoneWallet: '987654321',
        isVerified: true,
        isPrimary: false,
        status: 'ACTIVE',
      })
    }
  })
}

function primaryAccount(customerId: string): string {
  return (
    db.customerBankAccounts.find((a) => a.customerId === customerId && a.isPrimary)?.id ?? ''
  )
}

interface LoanSeed {
  customerId: string
  productId: string
  amount: string
  term: number
  frequency: PaymentFrequency
  start: string
  paid: number
  originatedFrom?: 'BACKOFFICE' | 'PORTAL'
}

function seedApplication(
  customerId: string,
  productId: string,
  amount: string,
  term: number,
  frequency: PaymentFrequency,
  status: LoanApplication['status'],
  createdAt: string,
  originatedFrom: 'BACKOFFICE' | 'PORTAL' = 'BACKOFFICE',
): LoanApplication {
  const product = db.products.find((p) => p.id === productId)!
  const evaluation = evaluateDti(customerId, product, toCents(amount), term, frequency)
  const decided = ['APROBADO', 'APROBADO_CON_CONDICIONES', 'RECHAZADO', 'DESEMBOLSADO'].includes(status)
  const application: LoanApplication = {
    id: nextId('app'),
    customerId,
    customerName: customerName(customerId),
    productId,
    productName: product.name,
    requestedAmount: amount,
    approvedAmount: status === 'RECHAZADO' ? undefined : amount,
    term,
    frequency,
    destination: 'CONSUMO',
    status,
    dtiRatio: evaluation.dti,
    systemRecommendation: evaluation.recommendation,
    decisionNotes: decided
      ? status === 'RECHAZADO'
        ? 'KYC rechazado y capacidad de pago insuficiente.'
        : 'Cumple capacidad de pago y política de crédito.'
      : undefined,
    decidedBy: decided ? 'Andrea Salazar (Analista)' : undefined,
    decidedAt: decided ? createdAt : undefined,
    originatedFrom,
    createdAt,
  }
  db.applications.push(application)
  return application
}

export function createLoanRecord(application: LoanApplication, accountId: string): LoanRec {
  const product = db.products.find((p) => p.id === application.productId)!
  const principal = toCents(application.approvedAmount ?? application.requestedAmount)
  const openingFee = roundHalfUp(principal * Number(product.openingFeeRate))
  const loan: LoanRec = {
    id: nextId('loan'),
    applicationId: application.id,
    customerId: application.customerId,
    productId: product.id,
    principal: product.openingFeeMode === 'FINANCIADA' ? principal + openingFee : principal,
    disbursed: product.openingFeeMode === 'DEDUCTIVA' ? principal - openingFee : principal,
    annualRate: Number(product.annualRate),
    amortizationMethod: product.amortizationMethod,
    frequency: application.frequency,
    term: application.term,
    graceDays: product.graceDays,
    moraRatePerDay: Number(product.moraRatePerDay),
    moraBase: product.moraBase,
    periodicFee: toCents(product.periodicFee),
    prepaymentPenaltyRate: Number(product.prepaymentPenaltyRate),
    status: 'PENDIENTE_DESEMBOLSO',
    scheduleVersion: 1,
    customerBankAccountId: accountId,
    createdAt: nowIso(),
  }
  db.loans.push(loan)
  return loan
}

function seedLoan(seed: LoanSeed): LoanRec {
  const application = seedApplication(
    seed.customerId, seed.productId, seed.amount, seed.term, seed.frequency,
    'APROBADO', `${seed.start}T12:00:00Z`, seed.originatedFrom,
  )
  const loan = createLoanRecord(application, primaryAccount(seed.customerId))
  loan.createdAt = `${seed.start}T12:00:00Z`
  const disbursementId = nextId('dis')
  loan.disbursementId = disbursementId
  db.disbursements.push({
    id: disbursementId,
    loanId: loan.id,
    customerName: customerName(loan.customerId),
    amount: fromCents(loan.disbursed),
    channel: 'TRANSFERENCIA_BANCARIA',
    status: 'COMPLETED',
    companyBankAccountId: 'bca-001',
    customerBankAccountId: loan.customerBankAccountId,
    operationNumber: `OP-${700000 + db.disbursements.length * 137}`,
    initiatedAt: `${seed.start}T12:00:00Z`,
    confirmedAt: `${seed.start}T12:05:00Z`,
    createdMs: 0,
  })
  activateLoan(loan, seed.start)

  for (const inst of activeInstallments(loan).slice(0, seed.paid)) {
    const payment: PaymentRec = {
      id: nextId('pay'),
      loanId: loan.id,
      kind: 'NORMAL',
      amount: inst.totalDue,
      method: db.payments.length % 2 === 0 ? 'YAPE' : 'TRANSFERENCIA_BANCARIA',
      status: 'APPLIED',
      gatewayReference: `GW-${200000 + db.payments.length * 211}`,
      companyBankAccountId: 'bca-001',
      paidAt: `${inst.dueDate}T15:00:00Z`,
      createdAt: `${inst.dueDate}T15:00:00Z`,
      allocations: [],
      creditToCustomer: 0,
    }
    db.payments.push(payment)
    applyPaymentCascade(payment, inst.dueDate)
  }
  refreshLoan(loan)
  return loan
}

function seedFlow(): void {
  const t = today()
  seedLoan({ customerId: 'cus-001', productId: 'prod-001', amount: '6000.00', term: 12, frequency: 'MENSUAL', start: addDays(addMonths(t, -3), 10), paid: 2, originatedFrom: 'PORTAL' })
  const arrears = seedLoan({ customerId: 'cus-002', productId: 'prod-001', amount: '4000.00', term: 10, frequency: 'MENSUAL', start: addDays(addMonths(t, -2), -15), paid: 0 })
  arrears.assignedTo = 'Carla Núñez (Cobranza)'
  seedLoan({ customerId: 'cus-003', productId: 'prod-002', amount: '1500.00', term: 12, frequency: 'SEMANAL', start: addDays(t, -35), paid: 3 })
  seedLoan({ customerId: 'cus-007', productId: 'prod-001', amount: '2000.00', term: 6, frequency: 'MENSUAL', start: addMonths(t, -7), paid: 6 })
  seedLoan({ customerId: 'cus-008', productId: 'prod-003', amount: '30000.00', term: 12, frequency: 'MENSUAL', start: addDays(addMonths(t, -2), 5), paid: 1 })

  // Solicitudes en distintas etapas del embudo.
  seedApplication('cus-004', 'prod-001', '3000.00', 12, 'MENSUAL', 'APROBADO', `${addDays(t, -2)}T16:00:00Z`)
  seedApplication('cus-003', 'prod-001', '8000.00', 12, 'MENSUAL', 'SOLICITADO', `${addDays(t, -1)}T09:30:00Z`)
  seedApplication('cus-001', 'prod-001', '2500.00', 6, 'MENSUAL', 'EN_EVALUACION', `${t}T08:15:00Z`, 'PORTAL')
  seedApplication('cus-005', 'prod-001', '1500.00', 6, 'MENSUAL', 'SOLICITADO', `${addDays(t, -3)}T11:00:00Z`, 'PORTAL')
  seedApplication('cus-006', 'prod-001', '5000.00', 12, 'MENSUAL', 'RECHAZADO', `${addDays(t, -10)}T10:00:00Z`)

  // Préstamo cuyo desembolso fue rechazado por el banco (queda en la bandeja de tesorería).
  const failedApp = seedApplication('cus-003', 'prod-002', '800.00', 8, 'SEMANAL', 'APROBADO', `${addDays(t, -4)}T10:00:00Z`)
  const failedLoan = createLoanRecord(failedApp, primaryAccount('cus-003'))
  const failedId = nextId('dis')
  failedLoan.disbursementId = failedId
  db.disbursements.push({
    id: failedId,
    loanId: failedLoan.id,
    customerName: customerName('cus-003'),
    amount: fromCents(failedLoan.disbursed),
    channel: 'TRANSFERENCIA_BANCARIA',
    status: 'FAILED',
    companyBankAccountId: 'bca-001',
    customerBankAccountId: failedLoan.customerBankAccountId,
    bankResponseCode: '14',
    bankResponseMessage: 'Cuenta destino inválida o inexistente',
    initiatedAt: `${addDays(t, -4)}T10:10:00Z`,
    failedAt: `${addDays(t, -4)}T10:12:00Z`,
    createdMs: 0,
  })

  // Mora mayor a 180 días: único préstamo elegible para castigo contable (FE-BO-35).
  const severe = seedLoan({
    customerId: 'cus-007',
    productId: 'prod-001',
    amount: '3500.00',
    term: 12,
    frequency: 'MENSUAL',
    start: addMonths(t, -8),
    paid: 0,
  })
  severe.assignedTo = 'Carla Núñez (Cobranza)'

  // Pago reportado con voucher: queda "En validación" hasta conciliarlo con el extracto.
  const firstLoan = db.loans[0]!
  const nextInst = activeInstallments(firstLoan).find((i) => i.status !== 'PAID')!
  db.payments.unshift({
    id: nextId('pay'),
    loanId: firstLoan.id,
    kind: 'NORMAL',
    amount: nextInst.totalDue,
    method: 'TRANSFERENCIA_BANCARIA',
    status: 'PENDING_CONFIRMATION',
    externalReference: 'OP-445566',
    companyBankAccountId: 'bca-001',
    paidAt: nowIso(),
    createdAt: nowIso(),
    allocations: [],
    creditToCustomer: 0,
  })

  // Gestiones de cobranza sobre el préstamo en mora.
  db.actions.push(
    { id: nextId('act'), loanId: arrears.id, actionType: 'LLAMADA_TELEFONICA', note: 'No contesta. Se deja mensaje de voz.', status: 'CERRADA', createdBy: 'Carla Núñez (Cobranza)', createdAt: `${addDays(t, -6)}T10:00:00Z` },
    { id: nextId('act'), loanId: arrears.id, actionType: 'PROMESA_DE_PAGO', note: 'Prometió pagar el lunes pasado y no abonó.', status: 'PROMESA_INCUMPLIDA', promiseAmount: '500.00', promiseDate: addDays(t, -5), createdBy: 'Carla Núñez (Cobranza)', createdAt: `${addDays(t, -7)}T11:00:00Z` },
    { id: nextId('act'), loanId: arrears.id, actionType: 'WHATSAPP', note: 'Cliente responde que pagará el viernes.', status: 'ACTIVA', createdBy: 'Carla Núñez (Cobranza)', createdAt: `${addDays(t, -2)}T16:30:00Z` },
    { id: nextId('act'), loanId: arrears.id, actionType: 'PROMESA_DE_PAGO', note: 'Promete pagar la cuota vencida.', status: 'ACTIVA', promiseAmount: '520.00', promiseDate: addDays(t, 2), nextActionAt: `${addDays(t, 2)}T09:00:00Z`, createdBy: 'Carla Núñez (Cobranza)', createdAt: `${addDays(t, -2)}T16:40:00Z` },
  )

  // Extracto bancario: abonos conciliados, un abono sin identificar y el voucher pendiente.
  const applied = db.payments.filter((p) => p.status === 'APPLIED' && p.kind === 'NORMAL')
  applied.slice(0, 4).forEach((payment, index) => {
    db.statements.push({
      id: nextId('stm'),
      companyBankAccountId: 'bca-001',
      operationNumber: `EXT-${910000 + index}`,
      valueDate: payment.paidAt.slice(0, 10),
      amount: fromCents(payment.amount),
      description: `ABONO ${payment.gatewayReference ?? 'TRANSFERENCIA'}`,
      reconciliationStatus: 'CONCILIADO_AUTOMATICO',
      reconciledPaymentId: payment.id,
    })
  })
  db.statements.push(
    { id: nextId('stm'), companyBankAccountId: 'bca-001', operationNumber: 'EXT-920001', valueDate: addDays(t, -1), amount: fromCents(nextInst.totalDue), description: 'TRANSF OP-445566 MARIA QUISPE', reconciliationStatus: 'PENDIENTE_CONCILIACION' },
    { id: nextId('stm'), companyBankAccountId: 'bca-001', operationNumber: 'EXT-920002', valueDate: addDays(t, -1), amount: '150.00', description: 'ABONO NO IDENTIFICADO J. PEREZ', reconciliationStatus: 'PENDIENTE_CONCILIACION' },
    { id: nextId('stm'), companyBankAccountId: 'bca-001', operationNumber: 'EXT-920003', valueDate: addDays(t, -2), amount: '-320.50', description: 'COMISION BANCARIA MENSUAL', reconciliationStatus: 'DESCUADRADO' },
    { id: nextId('stm'), companyBankAccountId: 'bca-002', operationNumber: 'FND-880100', valueDate: addDays(t, -3), amount: '-15000.00', description: 'FONDEO INTERNO A CUENTA DESEMBOLSOS BCP', reconciliationStatus: 'CONCILIADO_AUTOMATICO' },
    { id: nextId('stm'), companyBankAccountId: 'bca-001', operationNumber: 'FND-880100', valueDate: addDays(t, -3), amount: '15000.00', description: 'FONDEO RECIBIDO DESDE RECAUDADORA INTERBANK', reconciliationStatus: 'CONCILIADO_AUTOMATICO' },
  )

  db.internalTransfers.push({
    id: nextId('itr'),
    sourceBankAccountId: 'bca-002',
    sourceBankName: 'Interbank (**** 7782)',
    destinationBankAccountId: 'bca-001',
    destinationBankName: 'BCP (**** 4410)',
    currency: 'PEN',
    amount: '15000.00',
    transferDate: addDays(t, -3),
    bankOperationNumber: 'FND-880100',
    notes: 'Barrido semanal de recaudación hacia cuenta pagadora de desembolsos',
    createdBy: 'Diego Vargas (Operaciones)',
    createdAt: `${addDays(t, -3)}T17:15:00Z`,
  })

  db.events.push(
    { id: nextId('evt'), provider: 'yape', eventType: 'payment.confirmed', externalId: 'YP-558812', status: 'PROCESSED', retryCount: 0, createdAt: `${addDays(t, -1)}T14:02:00Z` },
    { id: nextId('evt'), provider: 'niubiz', eventType: 'payment.confirmed', externalId: 'NB-7731', status: 'DEAD_LETTER', retryCount: 4, errorDetail: 'No se encontró el préstamo para la referencia NB-7731', createdAt: `${addDays(t, -1)}T18:40:00Z` },
    { id: nextId('evt'), provider: 'bcp', eventType: 'transfer.received', externalId: 'BCP-993001', status: 'FAILED', retryCount: 2, errorDetail: 'Timeout al consultar la base de datos', createdAt: `${t}T07:10:00Z` },
    { id: nextId('evt'), provider: 'plin', eventType: 'payment.confirmed', externalId: 'PL-120044', status: 'RECEIVED', retryCount: 0, createdAt: nowIso() },
  )

  db.notifications.push(
    {
      id: nextId('notif'),
      customerId: 'cus-001',
      title: 'Desembolso exitoso',
      message: 'Tu préstamo ha sido desembolsado exitosamente en tu cuenta BCP por S/ 3,000.00.',
      category: 'DISBURSEMENT',
      read: true,
      createdAt: `${addDays(t, -60)}T11:00:00Z`,
    },
    {
      id: nextId('notif'),
      customerId: 'cus-001',
      title: 'Pago recibido',
      message: 'Confirmamos la recepción de tu pago de S/ 250.00 mediante Yape. Tu cuota fue amortizada.',
      category: 'PAYMENT',
      read: false,
      createdAt: `${addDays(t, -5)}T15:30:00Z`,
    },
    {
      id: nextId('notif'),
      customerId: 'cus-001',
      title: 'Próximo vencimiento de cuota',
      message: 'Te recordamos que tu cuota N° 4 vence en los próximos días. Paga a tiempo para evitar mora.',
      category: 'LOAN',
      read: false,
      createdAt: `${t}T08:00:00Z`,
    },
  )

  db.notificationPreferences.set('cus-001', {
    emailEnabled: true,
    whatsappEnabled: true,
    smsEnabled: false,
    notifyDaysBeforeDue: 3,
  })
}

/** Vuelve a poblar la API simulada con los datos semilla (también la usan las pruebas). */
export function resetDb(): void {
  Object.assign(db, createEmptyDb())
  seedUsers()
  seedCatalogs()
  seedCustomers()
  seedFlow()
  recordAudit({ entityName: 'system', entityId: 'seed', action: 'CREATE', after: { note: 'Datos semilla cargados' } })
}
