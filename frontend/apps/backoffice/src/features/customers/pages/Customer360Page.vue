<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  ArrowLeft,
  Building2,
  Calendar,
  CheckCircle2,
  CreditCard,
  ExternalLink,
  FileBadge,
  FileText,
  History,
  Lock,
  Mail,
  MapPin,
  Pencil,
  Phone,
  RefreshCw,
  Send,
  ShieldAlert,
  ShieldCheck,
  Smartphone,
  Star,
  Trash2,
  Upload,
  UserCheck,
} from '@lucide/vue'
import { toast } from 'vue-sonner'

import type {
  Customer360,
  CustomerBankAccount,
  DocumentCategory,
  KycStatus,
  OtpChannel,
  OtpIssued,
} from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { MoneyText, StatusBadge, TableGenerator, type TableColumn } from '@prestamos/ui'
import { Alert, AlertDescription, AlertTitle } from '@prestamos/ui/components/ui/alert'
import { Badge } from '@prestamos/ui/components/ui/badge'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Input } from '@prestamos/ui/components/ui/input'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import { formatBusinessDate, formatInstant } from '@prestamos/utils'

import { useCustomers } from '../composables/useCustomers'
import CustomerFormSheet from '../components/CustomerFormSheet.vue'
import KycDecisionDialog from '../components/KycDecisionDialog.vue'
import AddBankAccountDialog from '../components/AddBankAccountDialog.vue'
import CreditBalanceModal from '../components/CreditBalanceModal.vue'

// FE-BO-08, FE-BO-09, FE-BO-10, FE-BO-24 + BE-CUS-01..12: Expediente KYC, documentos, cuentas bancarias, consentimientos LPDP, auditoría y Ficha 360 del cliente.

const route = useRoute()
const router = useRouter()
const customerId = computed(() => route.params.id as string)
const {
  getCustomer360,
  verifyBankAccount,
  makePrimaryBankAccount,
  confirmPrimaryBankAccount,
  deactivateBankAccount,
  uploadDocument,
  getDocumentSignedUrl,
  submitKycForReview,
  resubmitKyc,
  retryKycChecks,
  grantConsent,
  revokeConsent,
  startContactVerification,
  confirmContactVerification,
} = useCustomers()

const loading = ref(true)
const customer = ref<Customer360 | null>(null)
const activeTab = ref<'kyc' | 'accounts' | 'loans' | 'applications' | 'history'>('kyc')

// Modales
const isEditOpen = ref(false)
const isKycModalOpen = ref(false)
const isAddAccountOpen = ref(false)
const isCreditBalanceOpen = ref(false)

// Document upload state
const selectedDocCategory = ref<DocumentCategory>('DNI_FRONTAL')
const isUploadingDoc = ref(false)
const fileInputRef = ref<HTMLInputElement | null>(null)

// Primary account OTP state
const pendingPrimaryAccountId = ref<string | null>(null)
const pendingPrimaryOtp = ref<OtpIssued | null>(null)
const primaryOtpCode = ref('')
const isConfirmingPrimary = ref(false)

// Contact OTP state
const contactOtpIssued = ref<OtpIssued | null>(null)
const contactOtpCode = ref('')
const isConfirmingContact = ref(false)

const hasReviewOrErrorChecks = computed(() => {
  const checks = customer.value?.kycDossier?.verifications ?? []
  return checks.some((c) => c.result === 'REVIEW' || c.result === 'ERROR')
})

const hasErrorChecks = computed(() => {
  const checks = customer.value?.kycDossier?.verifications ?? []
  return checks.some((c) => c.result === 'ERROR')
})

const hasMarketingConsent = computed(() => {
  const consents = customer.value?.consents ?? []
  return consents.some((c) => c.purpose === 'MARKETING' && c.isActive)
})

async function loadCustomer() {
  loading.value = true
  try {
    customer.value = await getCustomer360(customerId.value)
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar ficha 360', { description: apiError.detail })
    void router.replace({ name: 'customers' })
  } finally {
    loading.value = false
  }
}

async function verifyAccount(account: CustomerBankAccount) {
  if (!customer.value) return
  try {
    const data = await verifyBankAccount(account.id, customer.value.id, customer.value.fullName)
    toast.success('Cuenta verificada', {
      description: `La cuenta ${data.bankName} (${data.accountNumberMasked}) fue verificada contra el titular.`,
    })
    void loadCustomer()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al verificar cuenta', { description: apiError.detail })
  }
}

async function makePrimaryAccount(account: CustomerBankAccount) {
  if (!customer.value) return
  try {
    const res = await makePrimaryBankAccount(account.id, customer.value.id)
    if (res.requiresOtp && res.otpChallenge) {
      pendingPrimaryAccountId.value = account.id
      pendingPrimaryOtp.value = res.otpChallenge
      primaryOtpCode.value = ''
      toast.info('Confirmación OTP requerida', {
        description: `Se envió un código OTP a ${res.otpChallenge.maskedDestination} para confirmar el cambio de cuenta principal.`,
      })
      return
    }
    toast.success('Cuenta principal actualizada', {
      description: `Los desembolsos se realizarán por defecto a ${account.bankName} (${account.accountNumberMasked}).`,
    })
    void loadCustomer()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al actualizar cuenta principal', { description: apiError.detail })
  }
}

async function handleConfirmPrimaryOtp() {
  if (!customer.value || !pendingPrimaryAccountId.value || !pendingPrimaryOtp.value) return
  if (primaryOtpCode.value.trim().length !== 6) return
  isConfirmingPrimary.value = true
  try {
    await confirmPrimaryBankAccount(customer.value.id, pendingPrimaryAccountId.value, {
      otpRequestId: pendingPrimaryOtp.value.otpRequestId,
      code: primaryOtpCode.value.trim(),
    })
    toast.success('Cuenta principal confirmada con OTP', {
      description: 'Se activó el periodo de enfriamiento antifraude de 24 horas.',
    })
    pendingPrimaryAccountId.value = null
    pendingPrimaryOtp.value = null
    primaryOtpCode.value = ''
    void loadCustomer()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al confirmar OTP', { description: apiError.detail })
  } finally {
    isConfirmingPrimary.value = false
  }
}

async function handleDeactivateAccount(account: CustomerBankAccount) {
  if (!customer.value) return
  try {
    await deactivateBankAccount(customer.value.id, account.id)
    toast.success('Cuenta desactivada')
    void loadCustomer()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('No se pudo desactivar la cuenta', { description: apiError.detail })
  }
}

function createSamplePngFile(name: string): File {
  const pngBytes = new Uint8Array([
    0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x00, 0x00, 0x0d, 0x49, 0x48, 0x44, 0x52,
    0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1f, 0x15, 0xc4,
    0x89, 0x00, 0x00, 0x00, 0x0a, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9c, 0x63, 0x00, 0x01, 0x00, 0x00,
    0x05, 0x00, 0x01, 0x0d, 0x0a, 0x2d, 0xb4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4e, 0x44, 0xae,
    0x42, 0x60, 0x82,
  ])
  return new File([pngBytes], name, { type: 'image/png' })
}

async function handleFileSelected(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file || !customer.value) return
  isUploadingDoc.value = true
  try {
    await uploadDocument(customer.value.id, selectedDocCategory.value, file)
    toast.success(`Documento ${selectedDocCategory.value} cargado`)
    void loadCustomer()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al subir documento', { description: apiError.detail })
  } finally {
    isUploadingDoc.value = false
    if (input) input.value = ''
  }
}

async function handleQuickCaptureUpload() {
  if (!customer.value) return
  isUploadingDoc.value = true
  try {
    const file = createSamplePngFile(`${selectedDocCategory.value.toLowerCase()}.png`)
    await uploadDocument(customer.value.id, selectedDocCategory.value, file)
    toast.success(`Captura ${selectedDocCategory.value} validada y almacenada`)
    void loadCustomer()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al subir captura', { description: apiError.detail })
  } finally {
    isUploadingDoc.value = false
  }
}

async function handleOpenSignedUrl(docId: string) {
  if (!customer.value) return
  try {
    const res = await getDocumentSignedUrl(customer.value.id, docId)
    window.open(res.signedUrl, '_blank', 'noopener,noreferrer')
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al generar URL firmada', { description: apiError.detail })
  }
}

async function handleSubmitOrResubmitKyc() {
  if (!customer.value) return
  try {
    if (customer.value.kycStatus === 'REJECTED' || customer.value.kycStatus === 'EXPIRED') {
      await resubmitKyc(customer.value.id)
      toast.success('Expediente KYC reenviado a revisión')
    } else {
      await submitKycForReview(customer.value.id)
      toast.success('Expediente KYC enviado a revisión y checks automáticos ejecutados')
    }
    void loadCustomer()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('No se pudo enviar a revisión KYC', { description: apiError.detail })
  }
}

async function handleRetryKycChecks() {
  if (!customer.value) return
  try {
    await retryKycChecks(customer.value.id)
    toast.success('Checks automáticos KYC reintentados')
    void loadCustomer()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al reintentar checks KYC', { description: apiError.detail })
  }
}

async function handleToggleMarketingConsent() {
  if (!customer.value) return
  try {
    if (hasMarketingConsent.value) {
      await revokeConsent(customer.value.id, 'MARKETING')
      toast.success('Consentimiento de marketing revocado')
    } else {
      await grantConsent(customer.value.id, {
        purpose: 'MARKETING',
        version: 'v1.0-2026',
      })
      toast.success('Consentimiento de marketing otorgado')
    }
    void loadCustomer()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al actualizar consentimiento', { description: apiError.detail })
  }
}

async function handleStartContactOtp(channel: OtpChannel) {
  if (!customer.value) return
  try {
    contactOtpIssued.value = await startContactVerification(customer.value.id, channel)
    contactOtpCode.value = ''
    toast.info(`Código OTP enviado a ${contactOtpIssued.value.maskedDestination}`)
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al enviar OTP', { description: apiError.detail })
  }
}

async function handleConfirmContactOtp() {
  if (!customer.value || !contactOtpIssued.value || contactOtpCode.value.trim().length !== 6) return
  isConfirmingContact.value = true
  try {
    await confirmContactVerification(customer.value.id, {
      otpRequestId: contactOtpIssued.value.otpRequestId,
      code: contactOtpCode.value.trim(),
    })
    toast.success('Canal de contacto verificado')
    contactOtpIssued.value = null
    contactOtpCode.value = ''
    void loadCustomer()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Código OTP inválido', { description: apiError.detail })
  } finally {
    isConfirmingContact.value = false
  }
}

function onKycDecided(newStatus: KycStatus) {
  if (customer.value) {
    customer.value.kycStatus = newStatus
  }
  void loadCustomer()
}

function onAccountAdded() {
  void loadCustomer()
}

const customerLoanColumns: TableColumn<Customer360['loans'][number]>[] = [
  { key: 'id', label: 'Código', kind: 'mono', cellClass: 'text-xs font-semibold' },
  { key: 'productName', label: 'Producto', cellClass: 'font-medium' },
  { key: 'principal', label: 'Monto Capital', kind: 'money', cellClass: 'text-xs' },
  {
    key: 'outstandingPrincipal',
    label: 'Saldo Insoluto',
    kind: 'money',
    cellClass: 'text-xs font-bold text-foreground',
  },
  { key: 'nextDueDate', label: 'Próximo Vencimiento', kind: 'date', cellClass: 'text-xs text-muted-foreground' },
  {
    key: 'daysLate',
    label: 'Días Atraso',
    value: (loan) => (loan.daysLate > 0 ? `${loan.daysLate} días` : 'Al día'),
    cellClass: (loan) =>
      loan.daysLate > 0 ? 'text-destructive font-bold text-xs' : 'text-muted-foreground text-xs font-semibold',
  },
  { key: 'status', label: 'Estado', kind: 'status', statusKind: 'loan' },
  { key: 'action', label: 'Ver', align: 'right' },
]

const customerApplicationColumns: TableColumn<Customer360['applications'][number]>[] = [
  { key: 'id', label: 'ID Solicitud', kind: 'mono', cellClass: 'text-xs font-medium' },
  { key: 'productName', label: 'Producto' },
  { key: 'requestedAmount', label: 'Monto Solicitado', kind: 'money', cellClass: 'text-xs font-semibold' },
  { key: 'createdAt', label: 'Fecha Solicitud', kind: 'datetime', cellClass: 'text-xs text-muted-foreground' },
  { key: 'status', label: 'Estado', kind: 'status', statusKind: 'application' },
]

onMounted(() => {
  void loadCustomer()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Botón volver -->
    <div class="flex items-center justify-between">
      <Button variant="ghost" size="sm" class="gap-1.5 -ml-2" as-child>
        <RouterLink :to="{ name: 'customers' }">
          <ArrowLeft class="size-4" />
          <span>Volver al directorio</span>
        </RouterLink>
      </Button>
    </div>

    <!-- Cargando -->
    <div v-if="loading" class="space-y-6">
      <Skeleton class="h-32 w-full rounded-xl" />
      <Skeleton class="h-10 w-96 rounded-lg" />
      <Skeleton class="h-72 w-full rounded-xl" />
    </div>

    <template v-else-if="customer">
      <!-- ── Cabecera Ficha 360 (FE-BO-10) ── -->
      <Card>
        <CardContent class="p-6">
          <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-6">
            <div class="space-y-2">
              <div class="flex flex-wrap items-center gap-2.5">
                <h1 class="text-xl font-bold tracking-tight text-foreground">
                  {{ customer.fullName }}
                </h1>
                <StatusBadge :status="customer.kycStatus" kind="kyc" />
                <Badge variant="outline" class="font-mono text-xs">
                  {{ customer.docType }} {{ customer.docNumber }}
                </Badge>
                <Badge
                  v-if="customer.amlRiskLevel"
                  :variant="
                    customer.amlRiskLevel === 'CRITICAL' || customer.amlRiskLevel === 'HIGH'
                      ? 'destructive'
                      : 'outline'
                  "
                  class="text-xs"
                >
                  AML: {{ customer.amlRiskLevel }}
                </Badge>
                <Badge v-if="customer.isPep" variant="destructive" class="text-xs">PEP</Badge>
              </div>

              <div class="flex flex-wrap items-center gap-x-5 gap-y-1.5 text-xs text-muted-foreground">
                <span class="flex items-center gap-1.5">
                  <Mail class="size-3.5" />
                  {{ customer.email }}
                </span>
                <span class="flex items-center gap-1.5">
                  <Phone class="size-3.5" />
                  {{ customer.phone }}
                </span>
                <span v-if="customer.address" class="flex items-center gap-1.5">
                  <MapPin class="size-3.5" />
                  {{ customer.address }}
                  <template v-if="customer.district">({{ customer.district }})</template>
                </span>
                <span class="flex items-center gap-1.5">
                  <Calendar class="size-3.5" />
                  Alta: {{ formatInstant(customer.createdAt) }}
                </span>
              </div>
            </div>

            <!-- KPIs rápidos y Acciones -->
            <div class="flex flex-wrap items-center gap-3">
              <div class="rounded-lg border bg-muted/30 px-3.5 py-2 text-right">
                <div class="text-[11px] text-muted-foreground">Ingreso Neto Mensual</div>
                <div class="text-sm font-bold">
                  <MoneyText :amount="customer.monthlyNetIncome" />
                </div>
              </div>

              <!-- Saldo a Favor del Cliente (FE-BO-24) -->
              <div
                class="rounded-lg border px-3.5 py-2 text-right"
                :class="
                  Number(customer.creditBalance) > 0
                    ? 'border-success/40 bg-success/5'
                    : 'bg-muted/30'
                "
              >
                <div class="text-[11px] text-muted-foreground">Saldo a Favor</div>
                <div class="flex items-center gap-2">
                  <span
                    class="text-sm font-bold"
                    :class="{ 'text-success': Number(customer.creditBalance) > 0 }"
                  >
                    <MoneyText :amount="customer.creditBalance" />
                  </span>
                  <Button
                    v-if="Number(customer.creditBalance) > 0"
                    size="sm"
                    variant="outline"
                    class="h-6 px-2 text-[11px]"
                    @click="isCreditBalanceOpen = true"
                  >
                    Gestionar
                  </Button>
                </div>
              </div>

              <Button variant="outline" size="sm" @click="isEditOpen = true">
                <Pencil class="size-3.5 mr-1.5" />
                Editar Datos
              </Button>

              <Button size="sm" @click="isKycModalOpen = true">
                <UserCheck class="size-3.5 mr-1.5" />
                Dictaminar KYC
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <!-- ── Navegación por Pestañas de la Vista 360 ── -->
      <div class="border-b flex items-center gap-1 overflow-x-auto">
        <button
          type="button"
          class="px-4 py-2.5 text-sm font-medium border-b-2 transition-colors flex items-center gap-2 whitespace-nowrap"
          :class="
            activeTab === 'kyc'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          "
          @click="activeTab = 'kyc'"
        >
          <FileBadge class="size-4" />
          Expediente KYC y Perfil
        </button>

        <button
          type="button"
          class="px-4 py-2.5 text-sm font-medium border-b-2 transition-colors flex items-center gap-2 whitespace-nowrap"
          :class="
            activeTab === 'accounts'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          "
          @click="activeTab = 'accounts'"
        >
          <CreditCard class="size-4" />
          Cuentas Bancarias
          <Badge variant="secondary" class="ml-1 text-[10px] px-1.5 py-0">
            {{ customer.bankAccounts.length }}
          </Badge>
        </button>

        <button
          type="button"
          class="px-4 py-2.5 text-sm font-medium border-b-2 transition-colors flex items-center gap-2 whitespace-nowrap"
          :class="
            activeTab === 'loans'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          "
          @click="activeTab = 'loans'"
        >
          <Building2 class="size-4" />
          Préstamos
          <Badge variant="secondary" class="ml-1 text-[10px] px-1.5 py-0">
            {{ customer.loans.length }}
          </Badge>
        </button>

        <button
          type="button"
          class="px-4 py-2.5 text-sm font-medium border-b-2 transition-colors flex items-center gap-2 whitespace-nowrap"
          :class="
            activeTab === 'applications'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          "
          @click="activeTab = 'applications'"
        >
          <FileText class="size-4" />
          Solicitudes
          <Badge variant="secondary" class="ml-1 text-[10px] px-1.5 py-0">
            {{ customer.applications.length }}
          </Badge>
        </button>

        <button
          type="button"
          class="px-4 py-2.5 text-sm font-medium border-b-2 transition-colors flex items-center gap-2 whitespace-nowrap"
          :class="
            activeTab === 'history'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          "
          @click="activeTab = 'history'"
        >
          <History class="size-4" />
          Auditoría & LPDP
        </button>
      </div>

      <!-- ── Contenido de Pestañas ── -->
      <div>
        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- PESTAÑA 1: EXPEDIENTE KYC Y DATOS DETALLADOS (FE-BO-08)    -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'kyc'" class="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <!-- Columna Izquierda: Estado KYC y Documentos -->
          <Card class="lg:col-span-2">
            <CardHeader>
              <div class="flex items-center justify-between flex-wrap gap-2">
                <div>
                  <CardTitle class="text-base">Validación de Identidad y Documentos (KYC)</CardTitle>
                  <CardDescription>
                    Revisión de documentos cargados, verificaciones automáticas RENIEC / OFAC /
                    Biometría y vigencia anual (FE-BO-08 / BE-CUS-04).
                  </CardDescription>
                </div>
                <div class="flex items-center gap-2">
                  <Button
                    v-if="hasErrorChecks"
                    variant="outline"
                    size="sm"
                    @click="handleRetryKycChecks"
                  >
                    <RefreshCw class="size-3.5 mr-1" />
                    Reintentar Checks
                  </Button>
                  <Button
                    v-if="
                      customer.kycStatus === 'DRAFT' ||
                      customer.kycStatus === 'PENDING_DOCUMENTS' ||
                      customer.kycStatus === 'REJECTED' ||
                      customer.kycStatus === 'EXPIRED'
                    "
                    size="sm"
                    variant="secondary"
                    @click="handleSubmitOrResubmitKyc"
                  >
                    <Send class="size-3.5 mr-1" />
                    {{
                      customer.kycStatus === 'REJECTED' || customer.kycStatus === 'EXPIRED'
                        ? 'Reenviar a Revisión'
                        : 'Enviar a Revisión'
                    }}
                  </Button>
                  <StatusBadge :status="customer.kycStatus" kind="kyc" />
                </div>
              </div>
            </CardHeader>

            <CardContent class="space-y-5">
              <!-- Alerta según el estado KYC -->
              <Alert v-if="customer.kycStatus === 'VERIFIED'" class="border-success/40 bg-success/5">
                <ShieldCheck class="size-4 text-success" />
                <AlertTitle>Identidad Verificada</AlertTitle>
                <AlertDescription>
                  El expediente KYC fue validado satisfactoriamente.
                  <span v-if="customer.kycExpiresAt">
                    Vigente hasta el <strong>{{ formatInstant(customer.kycExpiresAt) }}</strong>.
                  </span>
                </AlertDescription>
              </Alert>

              <Alert v-else-if="customer.kycStatus === 'REJECTED'" variant="destructive">
                <ShieldAlert class="size-4" />
                <AlertTitle>Expediente KYC Rechazado</AlertTitle>
                <AlertDescription>
                  El cliente no puede recibir desembolsos hasta subsanar las observaciones:
                  <strong>{{ customer.kycRejectionReason ?? 'Sin detalle' }}</strong>.
                </AlertDescription>
              </Alert>

              <Alert v-else>
                <ShieldAlert class="size-4" />
                <AlertTitle>Verificación Pendiente / En Revisión</AlertTitle>
                <AlertDescription>
                  Revisa los documentos adjuntos y emite el dictamen para habilitar el desembolso de
                  solicitudes aprobadas.
                </AlertDescription>
              </Alert>

              <!-- Subida de documentos al expediente (BE-CUS-03) -->
              <div class="rounded-lg border p-3.5 bg-muted/20 space-y-2.5">
                <div class="text-xs font-semibold">Adjuntar Documento al Expediente (BE-CUS-03)</div>
                <div class="flex flex-col sm:flex-row gap-2">
                  <select
                    v-model="selectedDocCategory"
                    class="flex h-9 flex-1 rounded-md border border-input bg-transparent px-3 py-1 text-xs shadow-xs"
                  >
                    <option value="DNI_FRONTAL">DNI Frontal</option>
                    <option value="DNI_REVERSO">DNI Reverso</option>
                    <option value="SELFIE">Selfie Biométrica</option>
                    <option value="BOLETA_PAGO">Boleta de Pago / Ingresos</option>
                    <option value="RECIBO_SERVICIOS">Recibo de Servicios (Domicilio)</option>
                    <option value="ESTADO_CUENTA_BANCO">Estado de Cuenta Bancario</option>
                    <option value="FICHA_RUC">Ficha RUC SUNAT</option>
                    <option value="OTRO">Otro Documento</option>
                  </select>
                  <input
                    ref="fileInputRef"
                    type="file"
                    accept=".pdf,.jpg,.jpeg,.png,.webp"
                    class="hidden"
                    @change="handleFileSelected"
                  />
                  <Button
                    variant="outline"
                    size="sm"
                    :disabled="isUploadingDoc"
                    @click="fileInputRef?.click()"
                  >
                    <Upload class="size-3.5 mr-1.5" />
                    Subir Archivo
                  </Button>
                  <Button
                    variant="secondary"
                    size="sm"
                    :disabled="isUploadingDoc"
                    @click="handleQuickCaptureUpload"
                  >
                    Subir Captura Validada
                  </Button>
                </div>
                <p
                  v-if="customer.kycDossier?.missingDocuments?.length"
                  class="text-[11px] text-amber-600 dark:text-amber-400"
                >
                  Documentos faltantes:
                  <strong>{{ customer.kycDossier.missingDocuments.join(', ') }}</strong>
                </p>
              </div>

              <!-- Documentos vigentes cargados -->
              <div class="space-y-2">
                <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Documentos del Expediente ({{ customer.kycDossier?.currentDocuments?.length ?? 0 }})
                </h4>
                <div
                  v-if="customer.kycDossier?.currentDocuments?.length"
                  class="grid grid-cols-1 sm:grid-cols-2 gap-3"
                >
                  <div
                    v-for="doc in customer.kycDossier.currentDocuments"
                    :key="doc.id"
                    class="rounded-lg border p-3 bg-muted/20 flex items-center justify-between gap-2"
                  >
                    <div>
                      <div class="text-xs font-semibold">
                        {{ doc.category }} <span class="text-muted-foreground">v{{ doc.version }}</span>
                      </div>
                      <div class="text-[11px] text-muted-foreground font-mono">
                        {{ doc.fileName }} · SHA256: {{ doc.sha256Hash.slice(0, 10) }}...
                      </div>
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      class="h-7 text-xs"
                      @click="handleOpenSignedUrl(doc.id)"
                    >
                      <ExternalLink class="size-3 mr-1" />
                      Ver
                    </Button>
                  </div>
                </div>
              </div>

              <!-- Verificaciones automáticas RENIEC / SUNAT / OFAC / Biometría -->
              <div class="space-y-2">
                <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Verificaciones Automáticas KYC / AML
                </h4>
                <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
                  <template v-if="customer.kycDossier?.verifications?.length">
                    <div
                      v-for="chk in customer.kycDossier.verifications"
                      :key="chk.id"
                      class="rounded-lg border p-3.5 bg-muted/20 space-y-1.5"
                    >
                      <div class="flex items-center justify-between">
                        <span class="text-xs font-semibold">{{ chk.checkType }}</span>
                        <Badge
                          :variant="
                            chk.result === 'PASS'
                              ? 'outline'
                              : chk.result === 'FAIL'
                                ? 'destructive'
                                : 'secondary'
                          "
                          class="text-[10px]"
                        >
                          {{ chk.result }}
                        </Badge>
                      </div>
                      <p class="text-[11px] text-muted-foreground">
                        Proveedor: {{ chk.provider }}
                        <span v-if="chk.biometricScore != null">
                          · Score: {{ Number(chk.biometricScore).toFixed(1) }}%
                        </span>
                      </p>
                      <p v-if="chk.failureReason" class="text-[11px] text-destructive">
                        {{ chk.failureReason }}
                      </p>
                    </div>
                  </template>
                  <template v-else>
                    <div class="rounded-lg border p-3.5 bg-muted/20 space-y-1">
                      <span class="text-xs font-semibold">Documento Frontal y Reverso</span>
                      <p class="text-[11px] text-muted-foreground">
                        {{ customer.docType }}: {{ customer.docNumber }} contrastado con padrón.
                      </p>
                    </div>
                    <div class="rounded-lg border p-3.5 bg-muted/20 space-y-1">
                      <span class="text-xs font-semibold">Prueba de Vida (Selfie)</span>
                      <p class="text-[11px] text-muted-foreground">
                        Biometría facial contra fotografía oficial del documento.
                      </p>
                    </div>
                    <div class="rounded-lg border p-3.5 bg-muted/20 space-y-1">
                      <span class="text-xs font-semibold">Listas PEP / OFAC / UIF</span>
                      <p class="text-[11px] text-muted-foreground">
                        PEP: {{ customer.isPep ? 'SÍ (Riesgo Alto)' : 'NO (Sin coincidencias)' }}.
                      </p>
                    </div>
                  </template>
                </div>
              </div>

              <div class="flex items-center justify-end border-t pt-4">
                <Button size="sm" @click="isKycModalOpen = true">
                  <UserCheck class="size-4 mr-1.5" />
                  Actualizar Dictamen KYC
                </Button>
              </div>
            </CardContent>
          </Card>

          <!-- Columna Derecha: Resumen Socioeconómico y Verificación OTP de Contacto -->
          <Card>
            <CardHeader>
              <CardTitle class="text-base">Perfil Socioeconómico & Contacto</CardTitle>
              <CardDescription>Información declarada y estado de canales OTP</CardDescription>
            </CardHeader>
            <CardContent class="space-y-3 text-sm">
              <div class="flex justify-between py-1.5 border-b">
                <span class="text-muted-foreground text-xs">Tipo de Persona</span>
                <span class="font-medium text-xs">
                  {{ customer.personType === 'NATURAL' ? 'Persona Natural' : 'Persona Jurídica' }}
                </span>
              </div>
              <div class="flex justify-between py-1.5 border-b">
                <span class="text-muted-foreground text-xs">Fecha Nacimiento</span>
                <span class="font-medium text-xs">{{ formatBusinessDate(customer.birthDate) }}</span>
              </div>
              <div class="flex justify-between items-center py-1.5 border-b">
                <span class="text-muted-foreground text-xs">Email Verificado</span>
                <div class="flex items-center gap-1.5">
                  <Badge :variant="customer.emailVerified ? 'default' : 'outline'" class="text-[10px]">
                    {{ customer.emailVerified ? 'Verificado' : 'Pendiente' }}
                  </Badge>
                  <Button
                    v-if="!customer.emailVerified"
                    variant="ghost"
                    size="sm"
                    class="h-6 text-[10px] px-1.5"
                    @click="handleStartContactOtp('EMAIL')"
                  >
                    Enviar OTP
                  </Button>
                </div>
              </div>
              <div class="flex justify-between items-center py-1.5 border-b">
                <span class="text-muted-foreground text-xs">Celular Verificado</span>
                <div class="flex items-center gap-1.5">
                  <Badge :variant="customer.phoneVerified ? 'default' : 'outline'" class="text-[10px]">
                    {{ customer.phoneVerified ? 'Verificado' : 'Pendiente' }}
                  </Badge>
                  <Button
                    v-if="!customer.phoneVerified"
                    variant="ghost"
                    size="sm"
                    class="h-6 text-[10px] px-1.5"
                    @click="handleStartContactOtp('SMS')"
                  >
                    Enviar OTP
                  </Button>
                </div>
              </div>
              <div class="flex justify-between py-1.5 border-b">
                <span class="text-muted-foreground text-xs">Ocupación / Giro</span>
                <span class="font-medium text-xs">{{ customer.occupation || 'No especificado' }}</span>
              </div>
              <div class="flex justify-between py-1.5 border-b">
                <span class="text-muted-foreground text-xs">Centro Laboral</span>
                <span class="font-medium text-xs">{{ customer.employerName || 'Independiente' }}</span>
              </div>
              <div class="flex justify-between py-1.5 border-b">
                <span class="text-muted-foreground text-xs">Antigüedad Laboral</span>
                <span class="font-medium text-xs">{{ customer.workSeniorityMonths ?? 0 }} meses</span>
              </div>
              <div class="flex justify-between py-1.5">
                <span class="text-muted-foreground text-xs">Ingreso Neto Mensual</span>
                <span class="font-bold text-xs">
                  <MoneyText :amount="customer.monthlyNetIncome" />
                </span>
              </div>

              <!-- Verificación OTP de Contacto -->
              <div
                v-if="contactOtpIssued"
                class="rounded-lg border p-3 bg-muted/40 space-y-2 mt-2"
              >
                <p class="text-xs font-medium">
                  Código OTP enviado a <span class="font-mono">{{ contactOtpIssued.maskedDestination }}</span>
                </p>
                <div class="flex items-center gap-2">
                  <Input
                    v-model="contactOtpCode"
                    placeholder="123456"
                    maxlength="6"
                    class="h-8 font-mono text-xs"
                  />
                  <Button
                    size="sm"
                    class="h-8 text-xs"
                    :disabled="isConfirmingContact || contactOtpCode.trim().length !== 6"
                    @click="handleConfirmContactOtp"
                  >
                    Confirmar
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- PESTAÑA 2: CUENTAS BANCARIAS Y BILLETERAS (FE-BO-09)       -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'accounts'" class="space-y-4">
          <div class="flex items-center justify-between">
            <div>
              <h3 class="text-base font-semibold">Cuentas Bancarias y Billeteras Digitales</h3>
              <p class="text-xs text-muted-foreground">
                Al menos una cuenta debe estar verificada y marcada como primaria para ejecutar desembolsos (FE-BO-09).
              </p>
            </div>
            <Button size="sm" @click="isAddAccountOpen = true">
              <CreditCard class="size-4 mr-1.5" />
              Registrar Cuenta
            </Button>
          </div>

          <!-- Confirmación OTP para cambio de cuenta principal (BE-CUS-11) -->
          <Card v-if="pendingPrimaryOtp && pendingPrimaryAccountId" class="border-primary bg-primary/5">
            <CardContent class="p-4 space-y-2.5">
              <div class="flex items-center gap-2 text-xs font-semibold text-primary">
                <Lock class="size-4" />
                <span>Confirmación OTP para Cambio de Cuenta Principal ({{ pendingPrimaryOtp.maskedDestination }})</span>
              </div>
              <div class="flex items-center gap-2">
                <Input
                  v-model="primaryOtpCode"
                  placeholder="Código OTP (6 dígitos)"
                  maxlength="6"
                  class="max-w-44 h-8 font-mono text-xs"
                />
                <Button
                  size="sm"
                  class="h-8 text-xs"
                  :disabled="isConfirmingPrimary || primaryOtpCode.trim().length !== 6"
                  @click="handleConfirmPrimaryOtp"
                >
                  Confirmar OTP
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  class="h-8 text-xs"
                  @click="
                    pendingPrimaryOtp = null;
                    pendingPrimaryAccountId = null
                  "
                >
                  Cancelar
                </Button>
              </div>
            </CardContent>
          </Card>

          <div v-if="customer.bankAccounts.length === 0" class="rounded-xl border p-8 text-center text-sm text-muted-foreground">
            El cliente no tiene cuentas bancarias registradas.
          </div>

          <div v-else class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Card
              v-for="acc in customer.bankAccounts"
              :key="acc.id"
              :class="{ 'border-primary/50 ring-1 ring-primary/20': acc.isPrimary }"
            >
              <CardContent class="p-5 space-y-4">
                <div class="flex items-start justify-between gap-2">
                  <div class="flex items-center gap-2.5">
                    <div class="size-9 rounded-lg bg-primary/10 text-primary flex items-center justify-center">
                      <Smartphone v-if="acc.accountType === 'BILLETERA_DIGITAL'" class="size-5" />
                      <CreditCard v-else class="size-5" />
                    </div>
                    <div>
                      <div class="font-semibold text-sm flex items-center gap-2">
                        {{ acc.bankName }}
                        <Badge v-if="acc.isPrimary" variant="default" class="text-[10px] px-1.5 py-0">
                          <Star class="size-2.5 mr-1 fill-current" />
                          Primaria
                        </Badge>
                      </div>
                      <div class="text-xs text-muted-foreground">
                        {{ acc.accountType.replace('_', ' ') }}
                      </div>
                    </div>
                  </div>

                  <Badge
                    :variant="acc.isVerified ? 'outline' : 'secondary'"
                    :class="acc.isVerified ? 'text-success border-success/40' : ''"
                  >
                    {{ acc.isVerified ? 'Verificada' : 'Sin verificar' }}
                  </Badge>
                </div>

                <div class="rounded-md bg-muted/40 p-3 font-mono text-xs space-y-1">
                  <div class="flex justify-between">
                    <span class="text-muted-foreground font-sans">Cuenta / Destino:</span>
                    <span class="font-semibold">
                      {{ acc.accountNumberMasked }}
                      <span v-if="acc.phoneWallet" class="text-xs font-normal text-muted-foreground">
                        ({{ acc.phoneWallet }})
                      </span>
                    </span>
                  </div>
                  <div v-if="acc.cci || acc.cciMasked" class="flex justify-between">
                    <span class="text-muted-foreground font-sans">CCI:</span>
                    <span>{{ acc.cciMasked || acc.cci }}</span>
                  </div>
                  <div v-if="acc.coolingOffUntil || acc.disbursementEnabledFrom" class="flex justify-between text-amber-600 dark:text-amber-400">
                    <span class="font-sans">Habilitada desde:</span>
                    <span>{{ formatInstant((acc.disbursementEnabledFrom || acc.coolingOffUntil)!) }}</span>
                  </div>
                </div>

                <div class="flex items-center gap-2 pt-1">
                  <!-- Botón verificar titularidad -->
                  <Button
                    v-if="!acc.isVerified"
                    size="sm"
                    variant="outline"
                    class="h-8 text-xs flex-1 text-success hover:text-success border-success/30"
                    @click="verifyAccount(acc)"
                  >
                    <CheckCircle2 class="size-3.5 mr-1" />
                    Verificar Titularidad
                  </Button>

                  <!-- Botón hacer primaria -->
                  <Button
                    v-if="!acc.isPrimary && acc.isVerified"
                    size="sm"
                    variant="outline"
                    class="h-8 text-xs flex-1"
                    @click="makePrimaryAccount(acc)"
                  >
                    <Star class="size-3.5 mr-1" />
                    Marcar como Primaria
                  </Button>

                  <!-- Botón desactivar -->
                  <Button
                    v-if="!acc.isPrimary"
                    size="icon"
                    variant="ghost"
                    class="size-8 text-destructive"
                    title="Desactivar cuenta"
                    @click="handleDeactivateAccount(acc)"
                  >
                    <Trash2 class="size-3.5" />
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- PESTAÑA 3: PRÉSTAMOS ACTIVOS E HISTÓRICOS (FE-BO-10)       -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'loans'" class="space-y-4">
          <Card>
            <CardHeader class="pb-3">
              <CardTitle class="text-base">Historial de Créditos y Cartera</CardTitle>
              <CardDescription>
                Todos los préstamos desembolsados, saldos pendientes y estado de mora.
              </CardDescription>
            </CardHeader>
            <CardContent class="p-0">
              <TableGenerator
                :columns="customerLoanColumns"
                :rows="customer.loans"
                row-key="id"
                empty-text="El cliente no cuenta con préstamos registrados."
              >
                <template #cell-action="{ row: loan }">
                  <Button variant="ghost" size="sm" class="h-8 text-xs" as-child>
                    <RouterLink :to="{ name: 'loans', query: { q: loan.id } }">
                      Detalle
                    </RouterLink>
                  </Button>
                </template>
              </TableGenerator>
            </CardContent>
          </Card>
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- PESTAÑA 4: SOLICITUDES PASADAS (FE-BO-10)                 -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'applications'" class="space-y-4">
          <Card>
            <CardHeader class="pb-3">
              <CardTitle class="text-base">Solicitudes de Crédito en el Embudo</CardTitle>
              <CardDescription>
                Historial de solicitudes originadas en backoffice o portal del cliente.
              </CardDescription>
            </CardHeader>
            <CardContent class="p-0">
              <TableGenerator
                :columns="customerApplicationColumns"
                :rows="customer.applications"
                row-key="id"
                empty-text="No hay solicitudes de crédito registradas para este cliente."
              />
            </CardContent>
          </Card>
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- PESTAÑA 5: AUDITORÍA FIELD-LEVEL Y CONSENTIMIENTOS LPDP    -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'history'" class="space-y-4">
          <Card>
            <CardHeader class="flex flex-row items-center justify-between">
              <div>
                <CardTitle class="text-base">Consentimientos Ley N° 29733 (LPDP)</CardTitle>
                <CardDescription>
                  Autorizaciones de tratamiento de datos personales, centrales de riesgo y marketing
                </CardDescription>
              </div>
              <Button variant="outline" size="sm" @click="handleToggleMarketingConsent">
                {{
                  hasMarketingConsent
                    ? 'Revocar Marketing'
                    : 'Otorgar Marketing'
                }}
              </Button>
            </CardHeader>
            <CardContent class="space-y-2">
              <div
                v-if="!customer.consents?.length"
                class="text-xs text-muted-foreground py-4 text-center"
              >
                Sin consentimientos registrados.
              </div>
              <div
                v-for="con in customer.consents ?? []"
                :key="con.id"
                class="flex items-center justify-between rounded-lg border p-3 text-xs"
              >
                <div>
                  <span class="font-semibold">{{ con.purpose }}</span>
                  <span class="text-muted-foreground ml-2 font-mono">{{ con.version }}</span>
                  <div class="text-[11px] text-muted-foreground">
                    Canal: {{ con.channel }} · IP: {{ con.ipAddress }} ·
                    {{ formatInstant(con.grantedAt) }}
                  </div>
                </div>
                <Badge :variant="con.isActive ? 'default' : 'secondary'">
                  {{ con.isActive ? 'Vigente' : 'Revocado' }}
                </Badge>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle class="text-base">Historial de Cambios del Cliente (BE-CUS-02)</CardTitle>
              <CardDescription>
                Registro inmutable de modificaciones de ficha, dictámenes KYC y cuentas bancarias
              </CardDescription>
            </CardHeader>
            <CardContent class="space-y-2">
              <div
                v-if="!customer.history?.length"
                class="text-xs text-muted-foreground py-4 text-center"
              >
                Sin eventos de auditoría registrados.
              </div>
              <div
                v-for="entry in customer.history ?? []"
                :key="entry.auditId"
                class="rounded-lg border p-3 text-xs space-y-1.5"
              >
                <div class="flex items-center justify-between">
                  <Badge variant="outline" class="font-mono text-[10px]">{{ entry.action }}</Badge>
                  <span class="text-muted-foreground">
                    {{ entry.changedByEmail }} · {{ formatInstant(entry.changedAt) }}
                  </span>
                </div>
                <div class="font-mono text-[11px] space-y-0.5">
                  <div v-for="(diff, field) in entry.changes" :key="field">
                    <span class="font-semibold">{{ field }}:</span>
                    <span class="text-muted-foreground ml-1">
                      {{ JSON.stringify(diff.before) }} → {{ JSON.stringify(diff.after) }}
                    </span>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>

      <!-- ── Modales ── -->
      <CustomerFormSheet
        v-model:open="isEditOpen"
        :customer-to-edit="customer"
        @saved="loadCustomer"
      />

      <KycDecisionDialog
        v-model:open="isKycModalOpen"
        :customer-id="customer.id"
        :customer-name="customer.fullName"
        :current-status="customer.kycStatus"
        :has-review-or-error-checks="hasReviewOrErrorChecks"
        @decided="onKycDecided"
      />

      <AddBankAccountDialog
        v-model:open="isAddAccountOpen"
        :customer-id="customer.id"
        :customer-name="customer.fullName"
        @added="onAccountAdded"
      />

      <CreditBalanceModal
        v-model:open="isCreditBalanceOpen"
        :customer="customer"
        @processed="loadCustomer"
      />
    </template>
  </div>
</template>
