import type {
  AmortizationMethod,
  AuditLogEntry,
  BankStatementLine,
  ClientNotification,
  CollectionAction,
  Company,
  CompanyBankAccount,
  ComplaintClaimRecord,
  Customer,
  CustomerBankAccount,
  Disbursement,
  GlobalParameters,
  Holiday,
  InstallmentStatus,
  IntegrationEvent,
  InternalAccountTransfer,
  LoanApplication,
  LoanProduct,
  LoanStatus,
  NotificationPreferences,
  PaymentFrequency,
  PaymentMethodConfig,
  PaymentMethodType,
  PaymentStatus,
  RefundRequest,
} from '@prestamos/api-client'

import type { Cents } from './money'

export interface UserRec {
  id: string
  email: string
  password: string
  fullName: string
  role: string
  permissions: string[]
  customerId?: string
}

/** Préstamo con importes en centavos. */
export interface LoanRec {
  id: string
  applicationId: string
  customerId: string
  productId: string
  principal: Cents
  disbursed: Cents
  annualRate: number
  amortizationMethod: AmortizationMethod
  frequency: PaymentFrequency
  term: number
  graceDays: number
  moraRatePerDay: number
  moraBase: 'SALDO_CAPITAL_VENCIDO' | 'CUOTA_TOTAL_VENCIDA'
  periodicFee: Cents
  prepaymentPenaltyRate: number
  disbursementDate?: string
  firstDueDate?: string
  status: LoanStatus
  scheduleVersion: number
  disbursementId?: string
  customerBankAccountId: string
  createdAt: string
  assignedTo?: string
}

export interface InstallmentRec {
  id: string
  loanId: string
  scheduleVersion: number
  number: number
  dueDate: string
  principal: Cents
  interest: Cents
  fee: Cents
  totalDue: Cents
  paidPrincipal: Cents
  paidInterest: Cents
  paidFee: Cents
  lateFee: Cents
  paidLateFee: Cents
  status: InstallmentStatus
}

export interface AllocRec {
  installmentId: string
  installmentNumber: number
  toMora: Cents
  toInterest: Cents
  toFee: Cents
  toPrincipal: Cents
}

export interface PaymentRec {
  id: string
  loanId: string
  kind: 'NORMAL' | 'REVERSAL'
  amount: Cents
  method: PaymentMethodType
  status: PaymentStatus
  gatewayReference?: string
  externalReference?: string
  receiptNumber?: string
  reversalOfId?: string
  reversalReason?: string
  companyBankAccountId?: string
  /** Si hay, el pago se confirma solo cuando llegue este instante (simula el webhook). */
  confirmAt?: number
  paidAt: string
  createdAt: string
  allocations: AllocRec[]
  creditToCustomer: Cents
}

export interface DisbursementRec extends Disbursement {
  /** Marca de tiempo de creación (ms) para simular el avance PENDING → PROCESSING → COMPLETED. */
  createdMs: number
}

export interface Db {
  users: UserRec[]
  tokens: Map<string, string>
  customers: Customer[]
  customerBankAccounts: CustomerBankAccount[]
  companyAccounts: CompanyBankAccount[]
  products: LoanProduct[]
  applications: LoanApplication[]
  loans: LoanRec[]
  installments: InstallmentRec[]
  payments: PaymentRec[]
  disbursements: DisbursementRec[]
  statements: BankStatementLine[]
  internalTransfers: InternalAccountTransfer[]
  actions: CollectionAction[]
  events: IntegrationEvent[]
  audit: AuditLogEntry[]
  holidays: Holiday[]
  paymentMethods: PaymentMethodConfig[]
  refundRequests: RefundRequest[]
  notifications: ClientNotification[]
  notificationPreferences: Map<string, NotificationPreferences>
  complaints: ComplaintClaimRecord[]
  company: Company
  parameters: GlobalParameters
  idempotency: Map<string, { status: number; body: unknown }>
  counters: Map<string, number>
}

export function createEmptyDb(): Db {
  return {
    users: [],
    tokens: new Map(),
    customers: [],
    customerBankAccounts: [],
    companyAccounts: [],
    products: [],
    applications: [],
    loans: [],
    installments: [],
    payments: [],
    disbursements: [],
    statements: [],
    internalTransfers: [],
    actions: [],
    events: [],
    audit: [],
    holidays: [],
    paymentMethods: [],
    refundRequests: [],
    notifications: [],
    notificationPreferences: new Map(),
    complaints: [],
    company: {
      legalName: '',
      tradeName: '',
      taxId: '',
      legalAddress: '',
      supportPhone: '',
      supportEmail: '',
      legalRepName: '',
      defaultCurrency: 'PEN',
    },
    parameters: {
      collectionAllowedHoursStart: '08:00',
      collectionAllowedHoursEnd: '20:00',
      bankAccountChangeHoldHours: 24,
      prepaymentQuoteValidityDays: 3,
      maxDtiRatioPercent: 50,
      alertOverdueDaysThreshold: 90,
    },
    idempotency: new Map(),
    counters: new Map(),
  }
}

/** Estado único de la API simulada. `resetDb()` (seed.ts) lo vuelve a poblar. */
export const db: Db = createEmptyDb()

/** Ids legibles y secuenciales: cus-001, loan-004… */
export function nextId(prefix: string): string {
  const next = (db.counters.get(prefix) ?? 0) + 1
  db.counters.set(prefix, next)
  return `${prefix}-${String(next).padStart(3, '0')}`
}

export function nowIso(): string {
  return new Date().toISOString()
}

export function recordAudit(entry: {
  entityName: string
  entityId: string
  action: string
  before?: Record<string, unknown> | null
  after?: Record<string, unknown> | null
  userName?: string
}): void {
  db.audit.unshift({
    id: (db.counters.get('audit') ?? 0) + 1,
    entityName: entry.entityName,
    entityId: entry.entityId,
    action: entry.action,
    stateBefore: entry.before ?? null,
    stateAfter: entry.after ?? null,
    userName: entry.userName ?? 'sistema',
    userIp: '127.0.0.1',
    createdAt: nowIso(),
  })
  db.counters.set('audit', (db.counters.get('audit') ?? 0) + 1)
}
