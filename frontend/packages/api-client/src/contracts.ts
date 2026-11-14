/**
 * CONTRATOS PROVISIONALES de la API (camelCase, montos como string decimal).
 *
 * Reflejan docs/REQUERIMIENTOS_BACKEND.md y el esquema de database/. Los implementa la
 * API simulada de @prestamos/mocks. Cuando el backend publique su OpenAPI, estos tipos
 * se reemplazan por los generados (`pnpm gen:api`) y se borra este archivo (FE-NF-03).
 */

/** Monto decimal exacto como texto: "1250.50". El frontend nunca lo suma ni lo redondea. */
export type Money = string
/** Fecha de negocio AAAA-MM-DD (vencimientos, feriados). */
export type BusinessDate = string
/** Instante UTC en ISO 8601. */
export type Instant = string

export interface Page<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export type PersonType = 'NATURAL' | 'JURIDICA'
export type DocType = 'DNI' | 'CE' | 'RUC' | 'PASAPORTE'
export type KycStatus =
  | 'PENDING'
  | 'DRAFT'
  | 'PENDING_DOCUMENTS'
  | 'IN_REVIEW'
  | 'VERIFIED'
  | 'REJECTED'
  | 'EXPIRED'
export type PaymentFrequency = 'DIARIA' | 'SEMANAL' | 'QUINCENAL' | 'MENSUAL'
export type AmortizationMethod = 'FRANCES' | 'FLAT' | 'ALEMAN' | 'AMERICANO'
export type ApplicationStatus =
  | 'SOLICITADO'
  | 'EN_EVALUACION'
  | 'APROBADO'
  | 'APROBADO_CON_CONDICIONES'
  | 'RECHAZADO'
  | 'ANULADO'
  | 'DESEMBOLSADO'
export type LoanStatus =
  | 'PENDIENTE_DESEMBOLSO'
  | 'VIGENTE'
  | 'EN_MORA'
  | 'PAGADO'
  | 'CASTIGADO'
  | 'CANCELADO'
export type InstallmentStatus = 'PENDING' | 'PARTIAL' | 'PAID' | 'OVERDUE' | 'CANCELLED_BY_PREPAYMENT'
export type PaymentStatus = 'PENDING_CONFIRMATION' | 'APPLIED' | 'REVERSED'
export type PaymentMethodType =
  | 'TRANSFERENCIA_BANCARIA'
  | 'YAPE'
  | 'PLIN'
  | 'TARJETA_DEBITO'
  | 'TARJETA_CREDITO'
  | 'PASARELA_PAGO'
export type DisbursementStatus = 'PENDING' | 'PROCESSING' | 'COMPLETED' | 'FAILED' | 'REVERSED'

// ── Sesión y Seguridad (API_SECURITY.md) ─────────────────────────────────────
export type SecurityRole =
  | 'ROLE_ADMIN'
  | 'ROLE_ANALYST'
  | 'ROLE_OPERATOR'
  | 'ROLE_COLLECTOR'
  | 'ROLE_AUDITOR'
  | 'ROLE_CLIENT'
  | (string & {})

export type LoginStatus = 'AUTHENTICATED' | 'MFA_REQUIRED' | 'MFA_ENROLLMENT_REQUIRED'

export interface LoginRequest {
  email: string
  password: string
}

export interface AuthTokensResponse {
  accessToken: string
  refreshToken: string
  expiresInSeconds: number
  role: string
}

export interface LoginResponse {
  status: LoginStatus
  accessToken: string | null
  refreshToken: string | null
  expiresInSeconds: number | null
  role: string | null
  mfaToken: string | null
  mfaExpiresInSeconds: number | null
}

export interface MfaVerifyRequest {
  mfaToken: string
  code?: string | null
  recoveryCode?: string | null
}

export interface MfaEnrollRequest {
  mfaToken: string
}

export interface MfaEnrollResponse {
  secret: string
  otpauthUri: string
}

export interface MfaEnrollConfirmRequest {
  mfaToken: string
  code: string
}

export interface MfaEnrollConfirmResponse {
  tokens: AuthTokensResponse
  recoveryCodes: string[]
}

export interface RefreshRequest {
  refreshToken: string
}

export interface LogoutRequest {
  refreshToken?: string | null
}

/** Contrato mínimo de token mantenido por compatibilidad con mocks/mobile. */
export interface TokenResponse {
  accessToken: string
  refreshToken?: string
  expiresInSeconds?: number
  role?: string
}

export interface Me {
  id: string
  userId?: string
  fullName: string
  email: string
  role: string
  permissions: string[]
  mfaEnabled?: boolean
  /** Solo clientes del portal. */
  customerId?: string
  kycStatus?: KycStatus
}

export interface ChangeUserRoleRequest {
  newRole: string
  reason: string
}

export interface SecurityPingResponse {
  module: string
  status: string
}

// ── Clientes, KYC, Consentimientos, Cuentas y OTP (PLAN_CUSTOMERS_KYC_Y_CUENTAS.md) ──
export type AmlRiskLevel = 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL'

export type DocumentCategory =
  | 'DNI_FRONTAL'
  | 'DNI_REVERSO'
  | 'SELFIE'
  | 'BOLETA_PAGO'
  | 'RECIBO_SERVICIOS'
  | 'ESTADO_CUENTA_BANCO'
  | 'FICHA_RUC'
  | 'OTRO'

export type KycCheckType =
  | 'RENIEC'
  | 'SUNAT'
  | 'OFAC_UIF'
  | 'BIOMETRIC'
  | 'IDENTITY_REGISTRY'
  | 'BIOMETRIC_LIVENESS'
  | 'FACE_MATCH'
  | 'PEP_SCREENING'
  | 'AML_SANCTIONS'

export type KycCheckResult = 'PENDING' | 'PASS' | 'FAIL' | 'REVIEW' | 'ERROR'

export type ConsentPurpose = 'DATA_PROCESSING' | 'CREDIT_BUREAU_QUERY' | 'MARKETING'

export type OtpPurpose =
  | 'LOGIN'
  | 'CONTACT_VERIFICATION'
  | 'CONTRACT_SIGNATURE'
  | 'ACCOUNT_CHANGE'

export type OtpChannel = 'EMAIL' | 'SMS' | 'WHATSAPP' | 'PUSH'

export interface ConsentAcceptance {
  purpose: ConsentPurpose | string
  version: string
}

export interface CurrentConsentTextResponse {
  purpose: ConsentPurpose | string
  version: string
  title?: string
  legalText?: string
  body?: string
  sha256?: string
  isMandatory?: boolean
  publishedAt: Instant
}

export interface GrantConsentRequest {
  purpose: ConsentPurpose | string
  version: string
  channel?: string
}

export interface CustomerConsentResponse {
  id: string
  purpose: ConsentPurpose | string
  version: string
  isActive?: boolean
  ipAddress?: string
  grantedAt: Instant
  channel: string
  revokedAt?: Instant | null
}

export interface CustomerDocumentResponse {
  id: string
  customerId?: string
  category: DocumentCategory | string
  fileName?: string
  contentType?: string
  sha256?: string
  sha256Hash: string
  mimeType?: string
  sizeBytes: number
  originalName?: string | null
  version: number
  isCurrent: boolean
  uploadedByUserId?: string
  uploadedAt: Instant
  replacedAt?: Instant | null
}

export interface DocumentSignedUrlResponse {
  documentId?: string
  signedUrl: string
  url?: string
  expiresAt: Instant
}

export interface KycVerificationResponse {
  id: string
  checkType: KycCheckType | string
  provider: string
  result: KycCheckResult | string
  score?: number | null
  biometricScore?: number | null
  externalReference?: string | null
  errorMessage?: string | null
  failureReason?: string | null
  requestedAt?: Instant
  executedAt?: Instant
  completedAt?: Instant | null
}

export interface CustomerKycStatusResponse {
  customerId: string
  status: KycStatus
  kycStatus?: KycStatus
  isSubmittedForReview?: boolean
  kycSubmittedAt?: Instant | null
  verifiedAt?: Instant | null
  kycVerifiedAt?: Instant | null
  expiresAt?: Instant | null
  kycExpiresAt?: Instant | null
  rejectionReason?: string | null
  kycRejectedReason?: string | null
  kycNotes?: string | null
  isPep: boolean
  amlRiskLevel: AmlRiskLevel | string
  requiredDocuments?: string[]
  missingDocuments: string[]
  currentDocuments: CustomerDocumentResponse[]
  verifications: KycVerificationResponse[]
}

export interface ApproveKycRequest {
  notes: string
}

export interface RejectKycRequest {
  reason: string
}

export interface OtpIssued {
  otpRequestId: string
  channel: OtpChannel | string
  maskedDestination: string
  maskedTarget?: string
  expiresAt: Instant
  resendAvailableAt?: Instant
}

export interface StartContactVerificationRequest {
  channel: 'EMAIL' | 'SMS' | string
}

export interface ConfirmContactVerificationRequest {
  otpRequestId: string
  code: string
}

export interface RegisterBankAccountRequest {
  bankName: string
  accountType: CustomerBankAccount['accountType']
  cci?: string | null
  phoneWallet?: string | null
  gatewayToken?: string | null
  holderName?: string | null
}

export interface VerifyBankAccountRequest {
  holderName: string
  method?: string
  notes?: string | null
}

export interface ConfirmPrimaryBankAccountRequest {
  otpRequestId: string
  code: string
}

export interface BankAccountResponse {
  id: string
  bankName: string
  bankCode: string
  accountType: CustomerBankAccount['accountType']
  currency: string
  accountNumberMasked: string
  cciMasked: string
  holderName?: string
  last4?: string
  phoneWalletMasked?: string | null
  isVerified: boolean
  verificationMethod?: string | null
  verifiedAt?: Instant | null
  isPrimary: boolean
  isActive?: boolean
  status?: 'ACTIVE' | 'INACTIVE' | 'PENDING_VERIFICATION' | 'PENDING_COOLING'
  disbursementEnabledFrom?: Instant | null
  isInCoolingPeriod?: boolean
}

export interface SetPrimaryBankAccountResult {
  requiresOtp: boolean
  account?: BankAccountResponse | null
  otpChallenge?: OtpIssued | null
}

export interface CustomerBankAccount {
  id: string
  customerId: string
  bankName: string
  bankCode?: string | null
  accountType:
    | 'CUENTA_CORRIENTE'
    | 'CUENTA_AHORRO'
    | 'TARJETA_DEBITO'
    | 'BILLETERA_DIGITAL'
    | 'SAVINGS'
    | 'CHECKING'
  currency?: string
  accountNumber?: string
  /** Enmascarada: "**** 4821". */
  accountNumberMasked: string
  last4?: string
  cci?: string
  cciMasked?: string
  holderName?: string
  phoneWallet?: string
  phoneWalletMasked?: string | null
  isVerified: boolean
  verificationMethod?: string | null
  verifiedAt?: Instant | null
  isPrimary: boolean
  isActive?: boolean
  status?: 'ACTIVE' | 'INACTIVE' | 'PENDING_VERIFICATION' | 'PENDING_COOLING'
  /** Periodo de enfriamiento por seguridad tras cambio con OTP (FE-CP-18 / ARQUITECTURA_GENERAL §9). */
  coolingOffUntil?: Instant | null
  coolingPeriodEndsAt?: Instant | null
  disbursementEnabledFrom?: Instant | null
  isInCoolingPeriod?: boolean
}

export interface Customer {
  id: string
  personType: PersonType
  docType: DocType
  docNumber: string
  fullName: string
  firstName?: string | null
  lastName?: string | null
  businessName?: string | null
  email: string
  emailVerified?: boolean
  phone: string
  phonePrimary?: string | null
  phoneVerified?: boolean
  phoneSecondary?: string | null
  address: string
  district: string
  province?: string
  department?: string
  ubigeo?: string
  birthDate: BusinessDate
  monthlyNetIncome: Money
  monthlyIncome?: Money
  occupation: string
  employerName: string
  workSeniorityMonths: number
  kycStatus: KycStatus
  kycVerifiedAt?: Instant | null
  kycExpiresAt?: Instant | null
  kycRejectedReason?: string | null
  kycRejectionReason?: string | null
  kycNotes?: string
  isPep?: boolean
  amlRiskLevel?: AmlRiskLevel | string
  creditBalance: Money
  consents?: CustomerConsentResponse[]
  version?: number
  createdAt: Instant
  updatedAt?: Instant
}

export interface CreateCustomerRequest {
  personType?: Customer['personType']
  docType: Customer['docType']
  docNumber: string
  fullName: string
  firstName?: string
  lastName?: string
  businessName?: string
  email: string
  phone: string
  phonePrimary?: string
  phoneSecondary?: string | null
  address?: string
  district?: string
  province?: string
  department?: string
  ubigeo?: string
  birthDate: BusinessDate
  monthlyNetIncome: Money
  occupation?: string
  employerName?: string
  workSeniorityMonths?: number
  isPep?: boolean
  consents?: ConsentAcceptance[]
  expectedVersion?: number | null
}

export interface RegisterCustomerBackendRequest {
  personType: Customer['personType']
  docType: Customer['docType']
  docNumber: string
  fullName: string
  birthDate?: BusinessDate | null
  email: string
  phonePrimary: string
  phoneSecondary?: string | null
  address: string
  district: string
  monthlyNetIncome: number
  occupation?: string | null
  employerName?: string | null
  workSeniorityMonths: number
  consents: ConsentAcceptance[]
}

export interface ContactPatch {
  email?: string | null
  phonePrimary?: string | null
  phoneSecondary?: string | null
}

export interface AddressPatch {
  streetLine?: string | null
  address?: string | null
  district?: string | null
  province?: string | null
  department?: string | null
  ubigeo?: string | null
}

export interface SocioeconomicProfilePatch {
  monthlyNetIncome?: number | null
  occupation?: string | null
  employerName?: string | null
  workSeniorityMonths?: number | null
  isPep?: boolean | null
}

export interface UpdateCustomerRequest {
  contact?: ContactPatch | null
  address?: AddressPatch | null
  profile?: SocioeconomicProfilePatch | null
  expectedVersion?: number | null
}

export interface CustomerListItem {
  id: string
  personType: Customer['personType']
  docType: Customer['docType']
  docNumber: string
  fullName: string
  kycStatus: KycStatus
  amlRiskLevel: AmlRiskLevel | string
  isPep: boolean
  createdAt: Instant
}

export interface CustomerResponse {
  id: string
  personType: Customer['personType']
  docType: Customer['docType']
  docNumber: string
  fullName: string
  birthDate?: BusinessDate | null
  email: string
  emailVerified: boolean
  phonePrimary: string
  phoneVerified: boolean
  phoneSecondary?: string | null
  address: string
  district?: string | null
  monthlyNetIncome: number | string
  occupation: string
  employerName?: string | null
  workSeniorityMonths: number
  kycStatus: KycStatus
  kycExpiresAt?: Instant | null
  isPep: boolean
  amlRiskLevel: AmlRiskLevel | string
  creditBalance: number | string
  version: number
  createdAt: Instant
}

export interface FieldChange {
  field: string
  oldValue?: string | null
  newValue?: string | null
}

export interface CustomerChangeEntry {
  auditId?: string
  changedAt: Instant
  changedBy?: string | null
  changedByUserId?: string | null
  changedByEmail?: string | null
  action: string
  changes: Record<string, { before: unknown; after: unknown }>
  ip?: string | null
  userAgent?: string | null
}

export interface CustomerLoanSummaryDto {
  loanId: string
  loanNumber: string
  productName: string
  status: LoanStatus | string
  currency: string
  principalBalance: number | string
  interestBalance: number | string
  lateFeeBalance: number | string
  daysPastDue: number
  maxDaysPastDue: number
  nextDueDate?: BusinessDate | null
  disbursedAt: Instant
}

export interface CustomerApplicationSummaryDto {
  applicationId: string
  applicationNumber: string
  productName: string
  requestedAmount: number | string
  currency: string
  termMonths: number
  status: ApplicationStatus | string
  submittedAt: Instant
}

export interface MoneyValueDto {
  amount: number | string
  currency: string
}

export interface ConsolidatedDebtDto {
  principal: MoneyValueDto | number | string
  interest: MoneyValueDto | number | string
  lateFees: MoneyValueDto | number | string
  total: MoneyValueDto | number | string
}

export interface CustomerOverviewResponse {
  customerId?: string
  customer?: CustomerResponse
  activeLoans?: CustomerLoanSummaryDto[]
  closedLoans?: CustomerLoanSummaryDto[]
  debt?: ConsolidatedDebtDto
  activeLoansCount?: number
  totalActiveDebt?: MoneyValueDto | number | string
  totalActiveDebtAmount?: string
  availableCreditBalance?: MoneyValueDto | number | string
  availableCreditBalanceAmount?: string
  currentMaxDaysPastDue?: number
  historicalMaxDaysPastDue?: number
  maxDaysPastDue?: number
  hasActiveDelinquency?: boolean
  applications?: CustomerApplicationSummaryDto[]
  creditBalance?: number | string
}

export interface Customer360 extends Customer {
  bankAccounts: CustomerBankAccount[]
  loans: LoanSummary[]
  applications: ApplicationSummary[]
  /** Capital + interés + mora pendientes de todos los préstamos. */
  totalDebt: Money
  maxDaysLate: number
  historicalMaxDaysLate?: number
  hasActiveArrears: boolean
  kycDossier?: CustomerKycStatusResponse | null
  consents?: CustomerConsentResponse[]
  overview?: CustomerOverviewResponse | null
  history?: CustomerChangeEntry[]
}

// ── Productos y simulación ───────────────────────────────────────────────────
export interface LoanProduct {
  id: string
  name: string
  description: string
  currency: 'PEN' | 'USD'
  minAmount: Money
  maxAmount: Money
  minTerm: number
  maxTerm: number
  allowedFrequencies: PaymentFrequency[]
  amortizationMethod: AmortizationMethod
  /** TEA como decimal: "0.300000" = 30 %. */
  annualRate: string
  openingFeeRate: string
  openingFeeMode: 'DEDUCTIVA' | 'FINANCIADA'
  periodicFee: Money
  graceDays: number
  moraRatePerDay: string
  moraBase: 'SALDO_CAPITAL_VENCIDO' | 'CUOTA_TOTAL_VENCIDA'
  prepaymentPenaltyRate: string
  minAge: number
  maxAge: number
  minMonthlyIncome: Money
  maxDtiAuto: string
  maxDtiManual: string
  /** Día fijo de pago mensual (1-28) o según fecha de desembolso (FE-BO-12). */
  paymentDay?: number
  /** Capitalización de intereses vencidos (FE-BO-11). */
  allowAnatocism?: boolean
  /** Regla de traslado si el vencimiento cae en feriado o fin de semana (FE-BO-11). */
  holidayAdjustment?: 'NEXT_BUSINESS_DAY' | 'PREVIOUS_BUSINESS_DAY' | 'NONE'
  status: 'ACTIVO' | 'INACTIVO'
}

export interface ScheduleRow {
  number: number
  dueDate: BusinessDate
  principal: Money
  interest: Money
  fee: Money
  totalDue: Money
  /** Saldo de capital después de la cuota. */
  balance: Money
}

export interface SimulateRequest {
  productId: string
  amount: Money
  term: number
  frequency?: PaymentFrequency
  firstDueDate?: BusinessDate
}

export interface Simulation {
  productId: string
  summary: {
    tea: string
    tem: string
    /** Tasa de costo efectivo anual (incluye comisiones y cargos). */
    tcea: string
    totalPrincipal: Money
    totalInterest: Money
    totalFees: Money
    totalCost: Money
    installmentAmount: Money
  }
  installments: ScheduleRow[]
}

// ── Solicitudes ──────────────────────────────────────────────────────────────
export interface ApplicationSummary {
  id: string
  productName: string
  requestedAmount: Money
  status: ApplicationStatus
  createdAt: Instant
}

export interface LoanApplication {
  id: string
  customerId: string
  customerName: string
  productId: string
  productName: string
  requestedAmount: Money
  approvedAmount?: Money
  term: number
  frequency: PaymentFrequency
  destination: string
  status: ApplicationStatus
  /** DTI en %, p. ej. "32.5000". */
  dtiRatio?: string
  systemRecommendation?: 'APROBABLE' | 'REVISION_MANUAL' | 'RECHAZO_SUGERIDO'
  decisionNotes?: string
  decidedBy?: string
  decidedAt?: Instant
  originatedFrom: 'BACKOFFICE' | 'PORTAL'
  createdAt: Instant
}

export interface CreateApplicationRequest {
  customerId?: string
  productId: string
  amount: Money
  term: number
  frequency: PaymentFrequency
  destination?: string
  employerName?: string
  occupation?: string
  monthlyNetIncome?: Money
  incomeProofUrl?: string
}

export interface DecisionRequest {
  decision: 'APROBADO' | 'APROBADO_CON_CONDICIONES' | 'RECHAZADO'
  /** Obligatoria (422 si falta). */
  notes: string
  approvedAmount?: Money
}

// ── Préstamos ────────────────────────────────────────────────────────────────
export interface LoanSummary {
  id: string
  productName: string
  principal: Money
  outstandingPrincipal: Money
  status: LoanStatus
  nextDueDate?: BusinessDate
  daysLate: number
}

export interface Installment extends ScheduleRow {
  paidPrincipal: Money
  paidInterest: Money
  paidFee: Money
  lateFee: Money
  paidLateFee: Money
  status: InstallmentStatus
  daysLate: number
}

export interface Loan {
  id: string
  applicationId: string
  customerId: string
  customerName: string
  productId: string
  productName: string
  principal: Money
  disbursedAmount: Money
  annualRate: string
  amortizationMethod: AmortizationMethod
  frequency: PaymentFrequency
  term: number
  graceDays: number
  moraRatePerDay: string
  disbursementDate?: BusinessDate
  firstDueDate?: BusinessDate
  status: LoanStatus
  outstandingPrincipal: Money
  totalPaid: Money
  scheduleVersion: number
  disbursementId?: string
  createdAt: Instant
  assignedTo?: string
}

export interface Schedule {
  loanId: string
  version: number
  installments: Installment[]
}

export interface CreateLoanRequest {
  applicationId: string
  customerBankAccountId: string
  companyBankAccountId: string
}

export interface PrepaymentQuote {
  loanId: string
  asOfDate: BusinessDate
  outstandingPrincipal: Money
  accruedInterest: Money
  accruedMora: Money
  prepaymentPenalty: Money
  totalToPay: Money
  validUntil: BusinessDate
}

/** Simulación de abono extraordinario a capital (prepago parcial - FE-CP-17). */
export interface PartialPrepaymentSimulation {
  loanId: string
  currentOutstandingPrincipal: Money
  prepaymentAmount: Money
  newOutstandingPrincipal: Money
  option: 'REDUCE_TERM' | 'REDUCE_FEE'
  currentTermRemaining: number
  newTermRemaining: number
  currentInstallmentAmount: Money
  newInstallmentAmount: Money
  totalInterestSaved: Money
}

// ── Pagos ────────────────────────────────────────────────────────────────────
export interface PaymentAllocation {
  installmentId: string
  installmentNumber: number
  toMora: Money
  toInterest: Money
  toFee: Money
  toPrincipal: Money
}

export interface Payment {
  id: string
  loanId: string
  customerName: string
  kind: 'NORMAL' | 'REVERSAL'
  amount: Money
  method: PaymentMethodType
  status: PaymentStatus
  gatewayReference?: string
  externalReference?: string
  receiptNumber?: string
  reversalOfId?: string
  reversalReason?: string
  paidAt: Instant
  allocations: PaymentAllocation[]
  createdAt: Instant
}

export interface RegisterPaymentRequest {
  amount: Money
  method: PaymentMethodType
  companyBankAccountId?: string
  externalReference?: string
  paidAt?: Instant
}

export interface ReversePaymentRequest {
  reason: string
}

/** Intención de pago creada desde el portal (RF-29.1). */
export interface PaymentIntent {
  paymentId: string
  loanId: string
  amount: Money
  method: PaymentMethodType
  status: PaymentStatus
  /** Texto del QR dinámico (Yape/Plin) o URL del checkout alojado de la pasarela. */
  qrPayload?: string
  checkoutUrl?: string
  expiresAt: Instant
}

export interface ClientPaymentRequest {
  loanId: string
  amount: Money
  method: PaymentMethodType
  /** Transferencia con voucher: nombre del archivo subido. */
  voucherFileName?: string
}

// ── Tesorería ────────────────────────────────────────────────────────────────
export interface CompanyBankAccount {
  id: string
  bankName: string
  accountNumberMasked: string
  cci?: string
  currency: 'PEN' | 'USD'
  isDisbursement: boolean
  isCollection: boolean
  isActive: boolean
}

export interface Disbursement {
  id: string
  loanId: string
  customerName: string
  amount: Money
  channel: 'TRANSFERENCIA_BANCARIA' | 'YAPE' | 'PLIN' | 'DEPOSITO_CCI'
  status: DisbursementStatus
  companyBankAccountId: string
  customerBankAccountId: string
  operationNumber?: string
  bankResponseCode?: string
  bankResponseMessage?: string
  initiatedAt: Instant
  confirmedAt?: Instant
  failedAt?: Instant
}

export interface BankStatementLine {
  id: string
  companyBankAccountId: string
  operationNumber: string
  valueDate: BusinessDate
  /** Positivo = abono, negativo = cargo. */
  amount: Money
  description: string
  reconciliationStatus: 'CONCILIADO_AUTOMATICO' | 'PENDIENTE_CONCILIACION' | 'DESCUADRADO'
  reconciledPaymentId?: string
  reconciledDisbursementId?: string
}

export interface DailyBalance {
  bankAccountId: string
  date: BusinessDate
  expectedBalance: Money
  statementBalance: Money
  difference: Money
  result: 'CUADRADO' | 'DESCUADRADO'
  issues: string[]
  /** Total de fondeos recibidos desde otras cuentas propias en la fecha (RF-24.6). */
  internalTransfersIn?: Money
  /** Total de fondeos enviados hacia otras cuentas propias en la fecha (RF-24.6). */
  internalTransfersOut?: Money
}

/** Transferencia interna entre cuentas bancarias propias de la empresa (Fondeo / Barrido de Tesorería — RF-24.5). */
export interface InternalAccountTransfer {
  id: string
  sourceBankAccountId: string
  sourceBankName: string
  destinationBankAccountId: string
  destinationBankName: string
  currency: 'PEN' | 'USD'
  amount: Money
  transferDate: BusinessDate
  bankOperationNumber: string
  notes?: string
  createdBy: string
  createdAt: Instant
}

export interface CreateInternalTransferRequest {
  sourceBankAccountId: string
  destinationBankAccountId: string
  amount: Money
  transferDate: BusinessDate
  bankOperationNumber: string
  notes?: string
}


export interface IntegrationEvent {
  id: string
  provider: string
  eventType: string
  externalId: string
  status: 'RECEIVED' | 'PROCESSED' | 'FAILED' | 'DEAD_LETTER' | 'FAILED_NEEDS_REVIEW'
  retryCount: number
  errorDetail?: string
  createdAt: Instant
}

// ── Cobranza ─────────────────────────────────────────────────────────────────
export type AgingBucket = 'PREVENTIVO' | 'TRAMO_1' | 'TRAMO_2' | 'TRAMO_3' | 'TRAMO_4'

export interface AgingRow {
  loanId: string
  customerId: string
  customerName: string
  phone: string
  productName: string
  bucket: AgingBucket
  daysLate: number
  overdueInstallments: number
  overduePrincipal: Money
  totalDue: Money
  assignedTo?: string
  /** Hay al menos una promesa de pago vencida sin cumplimiento. */
  hasBrokenPromise?: boolean
}

export interface ReceivableItem {
  loanId: string
  customerId: string
  customerName: string
  phone: string
  productName: string
  installmentNumber: number
  dueDate: BusinessDate
  daysLate: number
  amount: Money
  principalDue: Money
  status: 'PENDIENTE' | 'VENCIDA'
  outstandingPrincipal: Money
}

export interface CollectionAction {
  id: string
  loanId: string
  actionType:
    | 'LLAMADA_TELEFONICA'
    | 'WHATSAPP'
    | 'EMAIL'
    | 'SMS'
    | 'NOTIFICACION_PUSH'
    | 'PROMESA_DE_PAGO'
    | 'ACUERDO_DE_PAGO_DIGITAL'
  note: string
  status: 'ACTIVA' | 'CUMPLIDA' | 'PROMESA_INCUMPLIDA' | 'CERRADA'
  nextActionAt?: Instant
  promiseAmount?: Money
  promiseDate?: BusinessDate
  createdBy: string
  createdAt: Instant
}

// ── Reportes ─────────────────────────────────────────────────────────────────
export interface DashboardKpis {
  portfolio: { total: Money; vigente: Money; enMora: Money; castigada: Money }
  /** Porcentaje: "5.00". */
  delinquencyRate: string
  collections: { expectedToday: Money; collectedToday: Money }
  disbursementsToday: { amount: Money; count: number }
  bankBalances: { total: Money; inTransit: Money }
  approvalRate: string
  agingChart: { bucket: AgingBucket; amount: Money }[]
  monthly: { month: string; disbursed: Money; collected: Money }[]
}

export interface CashflowReport {
  from: BusinessDate
  to: BusinessDate
  inflows: { principal: Money; interest: Money; mora: Money; fees: Money }
  outflows: { disbursements: Money; bankFees: Money }
  net: Money
}

export interface AccountStatement {
  loanId: string
  customerName: string
  principal: Money
  disbursementDate?: BusinessDate
  annualRate: string
  term: number
  totalPaid: Money
  outstandingPrincipal: Money
  nextDueDate?: BusinessDate
  movements: { date: Instant; type: 'DESEMBOLSO' | 'PAGO' | 'REVERSO'; amount: Money; receiptNumber?: string }[]
}

export interface ProductivityReport {
  analysts: {
    userId: string
    fullName: string
    role: string
    decidedCount: number
    approvedCount: number
    rejectedCount: number
  }[]
  collectors: {
    userId: string
    fullName: string
    role: string
    actionsCount: number
    promisesCount: number
  }[]
}

// ── Auditoría y administración ───────────────────────────────────────────────
export interface AuditLogEntry {
  id: number
  entityName: string
  entityId: string
  action: string
  stateBefore: Record<string, unknown> | null
  stateAfter: Record<string, unknown> | null
  userId?: string | null
  userName: string | null
  userEmail?: string | null
  userIp: string | null
  userAgent?: string | null
  createdAt: Instant
  approvalRequestId?: string | null
}

export interface AuditLogQuery {
  entity?: string
  entityName?: string
  entityId?: string
  action?: string
  userId?: string
  user?: string
  from?: string
  to?: string
  page?: number
  pageSize?: number
  approvalRequestId?: string
}

// ── Aprobaciones de cuatro ojos ──────────────────────────────────────────────
export type ApprovalOperationType =
  | 'PAYMENT_REVERSAL'
  | 'LOAN_WRITE_OFF'
  | 'MANUAL_ADJUSTMENT'
  | 'PRODUCT_RATE_CHANGE'
  | 'LOAN_RESTRUCTURE'
  | 'USER_ROLE_CHANGE'
  | (string & {})

export type ApprovalStatus = 'PENDING' | 'APPROVED' | 'REJECTED' | 'CANCELLED' | 'EXPIRED'

export type ApprovalScope = 'toDecide' | 'mine' | 'all'

export interface ApprovalUserRef {
  id: string
  fullName: string
  email: string
}

export interface ApprovalRequestSummary {
  id: string
  operationType: ApprovalOperationType
  targetEntity: string
  targetId: string
  status: ApprovalStatus
  reason: string
  evidenceRef: string | null
  requestedBy: ApprovalUserRef
  requestedAt: Instant
  expiresAt: Instant
  decidedBy: ApprovalUserRef | null
  decidedAt: Instant | null
}

export interface ApprovalRequestDto extends ApprovalRequestSummary {
  payload: Record<string, unknown>
  targetSnapshot: Record<string, unknown>
  currentTargetState: Record<string, unknown> | null
  decisionComment: string | null
}

export interface ApprovalQuery {
  status?: ApprovalStatus | string
  operationType?: ApprovalOperationType | string
  targetId?: string
  requestedBy?: string
  scope?: ApprovalScope
  page?: number
  pageSize?: number
}

export interface ApproveApprovalRequest {
  comment?: string | null
  mfaCode?: string | null
}

export interface RejectApprovalRequest {
  comment: string
}

export interface ApprovalOutcome {
  resultEntity: string
  resultId: string
}

export interface ApprovalDecisionResponse {
  id: string
  status: ApprovalStatus | string
  result?: ApprovalOutcome | null
}

export interface Company {
  legalName: string
  tradeName: string
  taxId: string
  legalAddress: string
  supportPhone: string
  supportEmail: string
  legalRepName: string
  defaultCurrency: 'PEN' | 'USD'
}

export interface Holiday {
  id: string
  date: BusinessDate
  name: string
  isRecurring: boolean
}

export interface PaymentMethodConfig {
  id: string
  name: string
  methodType: PaymentMethodType
  webhookEnabled: boolean
  isActive: boolean
}

export interface GlobalParameters {
  collectionAllowedHoursStart: string
  collectionAllowedHoursEnd: string
  bankAccountChangeHoldHours: number
  prepaymentQuoteValidityDays: number
  maxDtiRatioPercent: number
  alertOverdueDaysThreshold: number
}

export interface AdminUserDto {
  id: string
  email: string
  fullName: string
  role: string
  permissions: string[]
  createdAt?: Instant
}

// ── Portal del Cliente Onboarding (FE-CP-01 a FE-CP-05) ───────────────────────
export interface ClientRegisterRequest {
  docType: 'DNI' | 'CE' | 'PASAPORTE'
  docNumber: string
  fullName: string
  email: string
  phone: string
  password: string
}

export interface ClientRegisterResponse {
  customerId: string
  kycStatus: KycStatus
  accessToken?: string
}

export interface OtpSendRequest {
  phone: string
  purpose: 'REGISTER' | 'CONTRACT_SIGN' | 'BANK_CHANGE'
}

export interface OtpSendResponse {
  message: string
  expiresInSeconds: number
  demoCode?: string
}

export interface OtpVerifyRequest {
  phone: string
  code: string
  purpose: 'REGISTER' | 'CONTRACT_SIGN' | 'BANK_CHANGE'
}

export interface OtpVerifyResponse {
  verified: boolean
  message?: string
}

export interface KycSubmitRequest {
  dniFrontUrl?: string
  dniBackUrl?: string
  selfieUrl?: string
}

export interface ConsentsSubmitRequest {
  dataProtection: boolean
  creditBureau: boolean
  marketing: boolean
}

export interface CreateClientBankAccountRequest {
  bankName: string
  accountType: 'CUENTA_CORRIENTE' | 'CUENTA_AHORRO' | 'BILLETERA_DIGITAL'
  cci?: string
  phoneWallet?: string
}

// ── Contrato y Firma Digital (FE-CP-09) ──────────────────────────────────────
export interface LoanContractDocument {
  applicationId: string
  loanId?: string
  customerName: string
  docNumber: string
  productName: string
  principal: Money
  term: number
  frequency: PaymentFrequency
  tea: string
  tcea: string
  monthlyInstallment: Money
  totalInterest: Money
  totalFees: Money
  totalCost: Money
  disbursementAccount: string
  disbursementBank: string
  clauses: { title: string; body: string }[]
  schedule: ScheduleRow[]
  isSigned: boolean
  signatureEvidence?: {
    signedAt: Instant
    signedIp: string
    otpCode: string
    verified: boolean
  }
}

// ── Perfil y Cuentas Bancarias (FE-CP-18 y FE-CP-19) ──────────────────────────
export interface ChangePrimaryBankAccountRequest {
  accountId?: string
  bankAccountId?: string
  newAccount?: {
    bankName: string
    accountType?: 'CUENTA_AHORRO' | 'CUENTA_CORRIENTE' | 'BILLETERA_DIGITAL' | CustomerBankAccount['accountType']
    cci?: string
    phoneWallet?: string
  }
  otpRequestId?: string
  otpCode: string
}

export interface RefundRequest {
  id: string
  customerId: string
  amount: Money
  customerBankAccountId: string
  status: 'PENDING_APPROVAL' | 'APPROVED' | 'DISBURSED' | 'REJECTED'
  requestedAt: Instant
  bankName: string
  accountNumberMasked: string
}

// ── Centro de Notificaciones (FE-CP-20) ──────────────────────────────────────
export interface ClientNotification {
  id: string
  customerId: string
  title: string
  message: string
  category: 'PAYMENT' | 'LOAN' | 'DISBURSEMENT' | 'SECURITY' | 'SYSTEM'
  read: boolean
  createdAt: Instant
}

export interface NotificationPreferences {
  emailEnabled: boolean
  whatsappEnabled: boolean
  smsEnabled: boolean
  notifyDaysBeforeDue: number
}

// ── Libro de Reclamaciones Virtual (FE-CP-21 / Ley 29571) ─────────────────────
export interface ComplaintClaimRequest {
  claimType: 'RECLAMO' | 'QUEJA'
  consumerFullName: string
  docType: 'DNI' | 'CE' | 'PASAPORTE' | 'RUC'
  docNumber: string
  email: string
  phone: string
  address: string
  contractedGood: 'PRODUCTO' | 'SERVICIO'
  amountClaimed?: string
  description: string
  consumerOrder: string
}

export interface ComplaintClaimRecord extends ComplaintClaimRequest {
  id: string
  correlativeNumber: string
  status: 'REGISTRADO' | 'EN_EVALUACION' | 'ATENDIDO'
  createdAt: Instant
  responseDeadline: Instant
}


export interface SignContractRequest {
  applicationId: string
  otpCode: string
}

export interface SignContractResponse {
  success: boolean
  loanId: string
  signedAt: Instant
  message: string
}


