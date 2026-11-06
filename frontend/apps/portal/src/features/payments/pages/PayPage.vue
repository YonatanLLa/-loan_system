<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  AlertCircle,
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  ChevronRight,
  Clock,
  Coins,
  CreditCard,
  Download,
  ExternalLink,
  FileCheck2,
  FileText,
  HelpCircle,
  Info,
  LoaderCircle,
  QrCode,
  Receipt,
  RotateCcw,
  ShieldCheck,
  Sparkles,
  TrendingDown,
  UploadCloud,
  Wallet,
} from '@lucide/vue'

import type { PaymentMethodType } from '@prestamos/api-client'
import { MoneyText, StatusBadge } from '@prestamos/ui'
import { Badge } from '@prestamos/ui/components/ui/badge'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import { formatBusinessDate, formatInstant } from '@prestamos/utils'

import { useClientLoans } from '@/features/loans/composables/useClientLoans'
import { useLoanSchedule } from '@/features/loans/composables/useLoanSchedule'
import {
  usePaymentGateway,
  usePaymentHistory,
  usePrepayment,
} from '../composables'

// Requerimientos FE-CP-12 a FE-CP-17
const route = useRoute()
const router = useRouter()

const activeTab = ref<'PAY' | 'PREPAY_TOTAL' | 'PREPAY_PARTIAL' | 'HISTORY'>('PAY')

const {
  loans,
  selectedLoanId,
  activeLoan,
  hasLoans,
  isLoading: loansLoading,
  loadLoans,
} = useClientLoans()

const {
  schedule,
  nextInstallment: nextPendingInstallment,
  loadSchedule,
  isLoading: scheduleLoading,
} = useLoanSchedule()

const selectLoan = (id: string) => {
  selectedLoanId.value = id
  if (id) loadSchedule(id)
}

watch(selectedLoanId, (newId) => {
  if (newId) loadSchedule(newId)
})

const {
  loanId: gatewayLoanId,
  amount: gatewayAmount,
  installmentNumber: gatewayInstallmentNumber,
  concept: gatewayConcept,
  selectedMethod,
  voucherFileName,
  step: gatewayStep,
  isSubmitting: gatewaySubmitting,
  isPolling,
  errorMessage: gatewayError,
  activeIntent,
  confirmedPayment,
  isQrMethod,
  isCardMethod,
  isVoucherMethod,
  handleVoucherSelected,
  initiatePayment,
  downloadReceipt: downloadGatewayReceipt,
  reset: resetGateway,
} = usePaymentGateway()

const {
  payments,
  filteredPayments,
  isLoading: historyLoading,
  errorMessage: historyError,
  statusFilter,
  pendingCount,
  appliedCount,
  fetchHistory,
  downloadReceipt: downloadHistoryReceipt,
} = usePaymentHistory()

const {
  quote,
  isQuoting,
  quoteError,
  partialSimulation,
  isSimulatingPartial,
  partialError,
  partialAmount,
  partialOption,
  loadTotalPrepaymentQuote,
  simulatePartialPrepayment,
  resetQuote,
  resetPartial,
} = usePrepayment()

const fileInputRef = ref<HTMLInputElement | null>(null)

const handleSimulatePartial = () => {
  if (selectedLoanId.value) {
    simulatePartialPrepayment(selectedLoanId.value)
  }
}

// Sincronizar préstamo seleccionado con el gateway
watch(
  [selectedLoanId, nextPendingInstallment],
  ([loanId, nextInst]) => {
    if (loanId) {
      gatewayLoanId.value = loanId
      if (nextInst && !gatewayAmount.value) {
        gatewayAmount.value = nextInst.totalDue
        gatewayInstallmentNumber.value = nextInst.number
        gatewayConcept.value = `Cuota N° ${nextInst.number} (${activeLoan.value?.productName ?? 'Préstamo'})`
      }
    }
  },
  { immediate: true },
)

// Soporte de query params: ?tab=prepago|abono|historial & ?loanId=...
onMounted(async () => {
  await loadLoans()

  if (route.query.loanId && typeof route.query.loanId === 'string') {
    selectLoan(route.query.loanId)
  }

  if (route.query.tab === 'prepago') {
    activeTab.value = 'PREPAY_TOTAL'
    handleLoadQuote()
  } else if (route.query.tab === 'abono') {
    activeTab.value = 'PREPAY_PARTIAL'
  } else if (route.query.tab === 'historial') {
    activeTab.value = 'HISTORY'
  }
})

// Cargar cotización cuando se cambia a la pestaña de Prepago Total
const handleLoadQuote = async () => {
  if (selectedLoanId.value) {
    await loadTotalPrepaymentQuote(selectedLoanId.value)
  }
}

watch(activeTab, (tab) => {
  if (tab === 'PREPAY_TOTAL' && !quote.value && selectedLoanId.value) {
    handleLoadQuote()
  }
})

// Preparar pago desde Prepago Total
const proceedFromTotalQuote = () => {
  if (!quote.value) return
  gatewayAmount.value = quote.value.totalToPay
  gatewayConcept.value = `Cancelación anticipada total (${activeLoan.value?.productName ?? 'Préstamo'})`
  activeTab.value = 'PAY'
  resetGateway()
}

// Preparar pago desde Abono Extraordinario
const proceedFromPartialSimulation = () => {
  if (!partialSimulation.value) return
  gatewayAmount.value = partialSimulation.value.prepaymentAmount
  gatewayConcept.value = `Abono extraordinario a capital - ${
    partialSimulation.value.option === 'REDUCE_TERM' ? 'Reducción de plazo' : 'Reducción de cuota'
  }`
  activeTab.value = 'PAY'
  resetGateway()
}

const handleFileChange = (event: Event) => {
  const target = event.target as HTMLInputElement
  if (target.files && target.files[0]) {
    handleVoucherSelected(target.files[0])
  }
}

const triggerFileInput = () => {
  fileInputRef.value?.click()
}

const paymentMethods: {
  id: PaymentMethodType
  name: string
  tagline: string
  badge?: string
}[] = [
  {
    id: 'YAPE',
    name: 'Yape',
    tagline: 'QR dinámico instantáneo con confirmación automática',
    badge: 'Más rápido',
  },
  {
    id: 'PLIN',
    name: 'Plin',
    tagline: 'QR dinámico con acreditación inmediata',
  },
  {
    id: 'TARJETA_DEBITO',
    name: 'Tarjeta de Débito / Crédito',
    tagline: 'Visa, Mastercard, Amex vía pasarela segura PCI-DSS',
  },
  {
    id: 'TRANSFERENCIA_BANCARIA',
    name: 'Transferencia con Voucher',
    tagline: 'Abono en BCP / BBVA / Interbank (queda En validación)',
  },
]
</script>

<template>
  <div class="space-y-6">
    <!-- Header principal -->
    <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">
          Pagos y Amortizaciones
        </h1>
        <p class="text-sm text-slate-600 sm:text-base">
          Paga tus cuotas en línea, realiza abonos a capital o cancela tu crédito sin penalidades.
        </p>
      </div>

      <!-- Selector de crédito si tiene múltiples -->
      <div v-if="loans.length > 1" class="flex items-center gap-2">
        <label for="loan-select" class="text-xs font-semibold uppercase tracking-wider text-slate-500">
          Préstamo:
        </label>
        <select
          id="loan-select"
          :value="selectedLoanId"
          class="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-800 shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
          @change="selectLoan(($event.target as HTMLSelectElement).value)"
        >
          <option v-for="l in loans" :key="l.id" :value="l.id">
            {{ l.productName }} · S/ {{ l.principal }} ({{ l.id }})
          </option>
        </select>
      </div>
    </div>

    <!-- Navegación por pestañas -->
    <div class="border-b border-slate-200">
      <nav class="-mb-px flex flex-wrap gap-2 sm:gap-6" aria-label="Tabs">
        <button
          type="button"
          :class="[
            activeTab === 'PAY'
              ? 'border-brand-600 text-brand-600'
              : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700',
            'flex items-center gap-2 border-b-2 py-3 px-1 text-sm font-medium transition-colors',
          ]"
          @click="activeTab = 'PAY'"
        >
          <Wallet class="h-4 w-4" />
          <span>Pagar Cuota</span>
        </button>

        <button
          type="button"
          :class="[
            activeTab === 'PREPAY_TOTAL'
              ? 'border-brand-600 text-brand-600'
              : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700',
            'flex items-center gap-2 border-b-2 py-3 px-1 text-sm font-medium transition-colors',
          ]"
          @click="activeTab = 'PREPAY_TOTAL'"
        >
          <Sparkles class="h-4 w-4" />
          <span>Cancelación Anticipada (Total)</span>
          <Badge variant="outline" class="border-emerald-200 bg-emerald-50 text-[11px] text-emerald-700">
            0% penalidad
          </Badge>
        </button>

        <button
          type="button"
          :class="[
            activeTab === 'PREPAY_PARTIAL'
              ? 'border-brand-600 text-brand-600'
              : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700',
            'flex items-center gap-2 border-b-2 py-3 px-1 text-sm font-medium transition-colors',
          ]"
          @click="activeTab = 'PREPAY_PARTIAL'"
        >
          <TrendingDown class="h-4 w-4" />
          <span>Abono Extraordinario (Parcial)</span>
        </button>

        <button
          type="button"
          :class="[
            activeTab === 'HISTORY'
              ? 'border-brand-600 text-brand-600'
              : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700',
            'flex items-center gap-2 border-b-2 py-3 px-1 text-sm font-medium transition-colors',
          ]"
          @click="activeTab = 'HISTORY'"
        >
          <Receipt class="h-4 w-4" />
          <span>Historial de Pagos</span>
          <Badge v-if="pendingCount > 0" class="bg-amber-100 text-amber-800 text-[11px]">
            {{ pendingCount }} en validación
          </Badge>
        </button>
      </nav>
    </div>

    <!-- PESTAÑA 1: PAGAR CUOTA (FE-CP-12, FE-CP-13, FE-CP-14) -->
    <div v-if="activeTab === 'PAY'" class="space-y-6">
      <!-- PASO A: SELECCIÓN DE MÉTODO Y MONTO -->
      <div v-if="gatewayStep === 'SELECT_METHOD'" class="grid gap-6 lg:grid-cols-3">
        <!-- Formulario de Pago -->
        <Card class="lg:col-span-2">
          <CardHeader>
            <CardTitle class="flex items-center gap-2 text-lg">
              <CreditCard class="h-5 w-5 text-brand-600" />
              Selecciona tu medio de pago
            </CardTitle>
            <CardDescription>
              Elige cómo deseas abonar. Los pagos con QR y tarjeta se acreditan en segundos.
            </CardDescription>
          </CardHeader>

          <CardContent class="space-y-6">
            <!-- Selector de Métodos -->
            <div class="grid gap-3 sm:grid-cols-2">
              <button
                v-for="m in paymentMethods"
                :key="m.id"
                type="button"
                :class="[
                  selectedMethod === m.id
                    ? 'border-brand-600 bg-brand-50/50 ring-2 ring-brand-500'
                    : 'border-slate-200 bg-white hover:border-slate-300 hover:bg-slate-50',
                  'relative flex flex-col items-start rounded-xl border p-4 text-left transition-all',
                ]"
                @click="selectedMethod = m.id"
              >
                <div class="flex w-full items-center justify-between">
                  <span class="font-semibold text-slate-900">{{ m.name }}</span>
                  <Badge v-if="m.badge" class="bg-brand-100 text-brand-700 text-[10px]">
                    {{ m.badge }}
                  </Badge>
                </div>
                <span class="mt-1 text-xs text-slate-500 leading-relaxed">{{ m.tagline }}</span>
              </button>
            </div>

            <!-- Campo de Monto -->
            <div class="space-y-2 rounded-xl border border-slate-200 bg-slate-50 p-4">
              <label for="amount-input" class="text-xs font-semibold uppercase tracking-wider text-slate-600">
                Monto a pagar (S/)
              </label>
              <div class="relative rounded-lg shadow-sm">
                <span class="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3 font-semibold text-slate-500">
                  S/
                </span>
                <input
                  id="amount-input"
                  v-model="gatewayAmount"
                  type="number"
                  step="0.01"
                  min="1"
                  placeholder="0.00"
                  class="w-full rounded-lg border border-slate-300 bg-white py-2.5 pl-9 pr-3 text-lg font-bold text-slate-900 focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/20"
                />
              </div>

              <!-- Atajos de monto -->
              <div class="flex flex-wrap items-center gap-2 pt-2">
                <span class="text-xs text-slate-500">Sugerencias:</span>
                <button
                  v-if="nextPendingInstallment"
                  type="button"
                  class="rounded-full border border-slate-200 bg-white px-2.5 py-1 text-xs font-medium text-slate-700 hover:border-brand-400 hover:text-brand-600"
                  @click="gatewayAmount = nextPendingInstallment.totalDue; gatewayConcept = `Cuota N° ${nextPendingInstallment.number}`"
                >
                  Próxima cuota (S/ {{ nextPendingInstallment.totalDue }})
                </button>
                <button
                  v-if="activeLoan?.outstandingPrincipal"
                  type="button"
                  class="rounded-full border border-slate-200 bg-white px-2.5 py-1 text-xs font-medium text-slate-700 hover:border-brand-400 hover:text-brand-600"
                  @click="gatewayAmount = activeLoan.outstandingPrincipal; gatewayConcept = 'Saldo capital restante'"
                >
                  Saldo total (S/ {{ activeLoan.outstandingPrincipal }})
                </button>
              </div>
            </div>

            <!-- Panel de Transferencia con Voucher (FE-CP-13) -->
            <div
              v-if="isVoucherMethod"
              class="space-y-4 rounded-xl border border-amber-200 bg-amber-50/70 p-4 text-amber-900"
            >
              <div class="flex items-start gap-3">
                <Info class="h-5 w-5 shrink-0 text-amber-600 mt-0.5" />
                <div class="text-xs leading-relaxed space-y-1">
                  <p class="font-semibold text-amber-900">
                    Cuentas Recaudadoras de la Empresa:
                  </p>
                  <p>• <strong>BCP:</strong> Cta Cte 191-98765432-0-12 · CCI: 002-191-009876543200-12</p>
                  <p>• <strong>BBVA:</strong> Cta Cte 0011-0123-0100098765 · CCI: 011-123-000100098765-44</p>
                  <p class="text-[11px] text-amber-800 pt-1">
                    Nota: El pago quedará en estado <strong>"En validación"</strong> hasta que sea conciliado con el extracto bancario.
                  </p>
                </div>
              </div>

              <!-- Subida de archivo -->
              <div class="space-y-2">
                <input
                  ref="fileInputRef"
                  type="file"
                  accept="image/png,image/jpeg,application/pdf"
                  class="hidden"
                  @change="handleFileChange"
                />
                <div
                  class="flex cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed border-amber-300 bg-white p-4 text-center hover:bg-amber-50/40"
                  @click="triggerFileInput"
                >
                  <UploadCloud class="h-6 w-6 text-amber-600 mb-1" />
                  <span class="text-xs font-medium text-slate-800">
                    {{ voucherFileName ? `Archivo: ${voucherFileName}` : 'Adjuntar voucher o comprobante de pago' }}
                  </span>
                  <span class="text-[11px] text-slate-400">Formatos permitidos: JPG, PNG o PDF</span>
                </div>
              </div>
            </div>

            <!-- Error banner -->
            <div
              v-if="gatewayError"
              class="flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 p-3 text-xs text-rose-800"
            >
              <AlertCircle class="h-4 w-4 shrink-0 text-rose-600" />
              <span>{{ gatewayError }}</span>
            </div>
          </CardContent>

          <CardFooter class="flex flex-col gap-3 sm:flex-row sm:justify-between">
            <span class="text-xs text-slate-500">
              Concepto: <strong>{{ gatewayConcept }}</strong>
            </span>
            <Button
              :disabled="gatewaySubmitting || !parseFloat(gatewayAmount)"
              class="w-full sm:w-auto"
              @click="initiatePayment"
            >
              <LoaderCircle v-if="gatewaySubmitting" class="mr-2 h-4 w-4 animate-spin" />
              <span>Continuar al Pago</span>
              <ArrowRight v-if="!gatewaySubmitting" class="ml-2 h-4 w-4" />
            </Button>
          </CardFooter>
        </Card>

        <!-- Resumen lateral -->
        <div class="space-y-4">
          <Card>
            <CardHeader class="pb-3">
              <CardTitle class="text-base">Resumen de tu Préstamo</CardTitle>
            </CardHeader>
            <CardContent class="space-y-3 text-sm">
              <div class="flex justify-between">
                <span class="text-slate-500">Préstamo</span>
                <span class="font-medium text-slate-900">{{ activeLoan?.productName ?? 'Cargando...' }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-500">Código</span>
                <span class="font-mono text-xs text-slate-700">{{ activeLoan?.id }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-500">Saldo Capital</span>
                <span class="font-medium text-slate-900">S/ {{ activeLoan?.outstandingPrincipal }}</span>
              </div>
              <div v-if="nextPendingInstallment" class="flex justify-between border-t border-slate-100 pt-2">
                <span class="text-slate-500">Próximo Vencimiento</span>
                <span class="font-medium text-slate-900">{{ formatBusinessDate(nextPendingInstallment.dueDate) }}</span>
              </div>
            </CardContent>
          </Card>

          <Card class="bg-brand-50/50 border-brand-100">
            <CardContent class="p-4 space-y-2 text-xs text-brand-900">
              <div class="flex items-center gap-2 font-semibold">
                <ShieldCheck class="h-4 w-4 text-brand-600" />
                <span>Pago 100% Protegido</span>
              </div>
              <p class="leading-relaxed text-brand-800/90">
                Tus pagos son procesados con encriptación de nivel bancario. Los comprobantes oficiales se generan de forma instantánea con validez tributaria.
              </p>
            </CardContent>
          </Card>
        </div>
      </div>

      <!-- PASO B: ESPERANDO PAGO CON QR DINÁMICO O TARJETA (FE-CP-12, FE-CP-14) -->
      <div v-else-if="gatewayStep === 'AWAITING_PAYMENT'" class="mx-auto max-w-xl">
        <Card class="border-brand-200 shadow-md">
          <CardHeader class="text-center pb-2">
            <div class="mx-auto mb-2 flex h-12 w-12 items-center justify-center rounded-full bg-brand-100 text-brand-600">
              <QrCode v-if="isQrMethod" class="h-6 w-6" />
              <CreditCard v-else class="h-6 w-6" />
            </div>
            <CardTitle class="text-xl">
              {{ isQrMethod ? `Escanea y Paga con ${selectedMethod}` : 'Completa el Pago con Tarjeta' }}
            </CardTitle>
            <CardDescription>
              Orden #{{ activeIntent?.paymentId }} · Monto exacto:
              <strong class="text-slate-900 font-bold text-base">S/ {{ gatewayAmount }}</strong>
            </CardDescription>
          </CardHeader>

          <CardContent class="space-y-6 pt-2">
            <!-- Simulación Visual de QR Dinámico -->
            <div v-if="isQrMethod" class="flex flex-col items-center justify-center space-y-3">
              <div class="relative flex h-56 w-56 items-center justify-center rounded-2xl border-4 border-slate-900 bg-white p-4 shadow-inner">
                <!-- SVG Mockup de Código QR Dinámico -->
                <svg viewBox="0 0 100 100" class="h-full w-full">
                  <path fill="#0f172a" d="M0,0 h30 v30 h-30 z M10,10 h10 v10 h-10 z M70,0 h30 v30 h-30 z M80,10 h10 v10 h-10 z M0,70 h30 v30 h-30 z M10,80 h10 v10 h-10 z" />
                  <path fill="#0f172a" d="M40,10 h10 v10 h-10 z M50,20 h10 v10 h-10 z M35,40 h15 v15 h-15 z M60,40 h20 v10 h-20 z M40,65 h10 v15 h-10 z M65,65 h15 v15 h-15 z M80,80 h10 v10 h-10 z" />
                  <rect x="42" y="42" width="16" height="16" rx="4" fill="#0284c7" />
                  <circle cx="50" cy="50" r="4" fill="white" />
                </svg>

                <!-- Badge flotante de pasarela -->
                <div class="absolute -bottom-3 rounded-full bg-slate-900 px-3 py-0.5 text-[10px] font-bold text-white uppercase tracking-wider">
                  {{ selectedMethod }} QR
                </div>
              </div>

              <div class="text-center space-y-1">
                <p class="text-xs text-slate-500">
                  Abre tu app de {{ selectedMethod }} y escanea este código.
                </p>
                <p class="font-mono text-[11px] text-slate-400">
                  Payload: {{ activeIntent?.qrPayload }}
                </p>
              </div>
            </div>

            <!-- Formulario Alojado de Tarjeta (Hosted Fields Mock) -->
            <div v-else-if="isCardMethod" class="space-y-4 rounded-xl border border-slate-200 bg-slate-50 p-4">
              <div class="flex items-center justify-between border-b border-slate-200 pb-2">
                <span class="text-xs font-semibold text-slate-600">Checkout Seguro Niubiz / Izipay</span>
                <span class="text-[10px] font-bold uppercase text-emerald-600 bg-emerald-50 px-2 py-0.5 rounded">PCI-DSS Level 1</span>
              </div>
              <div class="space-y-3 text-xs">
                <div>
                  <label class="block text-slate-500 mb-1">Número de Tarjeta</label>
                  <input
                    type="text"
                    disabled
                    value="•••• •••• •••• 4242"
                    class="w-full rounded border border-slate-200 bg-white px-3 py-2 text-slate-700"
                  />
                </div>
                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-slate-500 mb-1">Vencimiento</label>
                    <input
                      type="text"
                      disabled
                      value="12/28"
                      class="w-full rounded border border-slate-200 bg-white px-3 py-2 text-slate-700"
                    />
                  </div>
                  <div>
                    <label class="block text-slate-500 mb-1">CVV</label>
                    <input
                      type="password"
                      disabled
                      value="•••"
                      class="w-full rounded border border-slate-200 bg-white px-3 py-2 text-slate-700"
                    />
                  </div>
                </div>
              </div>
              <p class="text-[11px] text-slate-400 text-center">
                (Entorno de prueba simulado: los datos no tocan servidores de aplicación)
              </p>
            </div>

            <!-- Indicador de polling en tiempo real (FE-CP-14) -->
            <div class="flex items-center justify-center gap-2 rounded-lg bg-amber-50 p-3 text-xs text-amber-800">
              <LoaderCircle class="h-4 w-4 animate-spin text-amber-600" />
              <span>Esperando confirmación del webhook de la pasarela en tiempo real...</span>
            </div>
          </CardContent>

          <CardFooter class="flex justify-between border-t border-slate-100 pt-4">
            <Button variant="ghost" size="sm" @click="resetGateway">
              Cancelar
            </Button>
            <span class="text-xs text-slate-400">Expira en 30 minutos</span>
          </CardFooter>
        </Card>
      </div>

      <!-- PASO C: CONFIRMACIÓN INSTANTÁNEA (FE-CP-14) -->
      <div v-else-if="gatewayStep === 'CONFIRMED'" class="mx-auto max-w-xl">
        <Card class="border-emerald-200 bg-gradient-to-b from-emerald-50/40 to-white shadow-lg">
          <CardHeader class="text-center pb-3">
            <div class="mx-auto mb-2 flex h-16 w-16 items-center justify-center rounded-full bg-emerald-100 text-emerald-600">
              <CheckCircle2 class="h-10 w-10" />
            </div>
            <CardTitle class="text-2xl text-emerald-900">¡Pago Confirmado con Éxito!</CardTitle>
            <CardDescription class="text-emerald-700">
              Tu cuota ha sido amortizada y registrada en el sistema.
            </CardDescription>
          </CardHeader>

          <CardContent class="space-y-4">
            <div class="rounded-xl border border-emerald-100 bg-white p-4 space-y-2.5 text-sm">
              <div class="flex justify-between">
                <span class="text-slate-500">N° de Recibo</span>
                <span class="font-mono font-bold text-slate-900">
                  {{ confirmedPayment?.receiptNumber ?? 'REC-20261106-0001' }}
                </span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-500">Monto Abonado</span>
                <span class="font-bold text-emerald-600">S/ {{ confirmedPayment?.amount ?? gatewayAmount }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-500">Medio de Pago</span>
                <span class="font-medium text-slate-800">{{ confirmedPayment?.method ?? selectedMethod }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-500">Fecha y Hora</span>
                <span class="text-slate-700">{{ confirmedPayment?.paidAt ? formatInstant(confirmedPayment.paidAt) : 'Hoy' }}</span>
              </div>
              <div class="flex justify-between border-t border-slate-100 pt-2">
                <span class="text-slate-500">Estado</span>
                <Badge class="bg-emerald-100 text-emerald-800">APLICADO</Badge>
              </div>
            </div>

            <!-- Botón de descarga de comprobante PDF oficial -->
            <Button class="w-full bg-emerald-600 hover:bg-emerald-700" size="lg" @click="downloadGatewayReceipt">
              <Download class="mr-2 h-5 w-5" />
              <span>Descargar Recibo Oficial en PDF</span>
            </Button>
          </CardContent>

          <CardFooter class="flex justify-center border-t border-slate-100 pt-4">
            <Button variant="outline" size="sm" @click="resetGateway">
              Realizar otro pago
            </Button>
          </CardFooter>
        </Card>
      </div>

      <!-- PASO D: PAGO EN VALIDACIÓN (FE-CP-13) -->
      <div v-else-if="gatewayStep === 'IN_VALIDATION'" class="mx-auto max-w-xl">
        <Card class="border-amber-200 bg-amber-50/30 shadow-md">
          <CardHeader class="text-center pb-2">
            <div class="mx-auto mb-2 flex h-14 w-14 items-center justify-center rounded-full bg-amber-100 text-amber-600">
              <Clock class="h-8 w-8" />
            </div>
            <CardTitle class="text-xl text-amber-900">Comprobante Recibido</CardTitle>
            <CardDescription class="text-amber-700">
              Estado: <strong class="uppercase">En validación</strong> (FE-CP-13)
            </CardDescription>
          </CardHeader>

          <CardContent class="space-y-4 text-xs text-slate-700">
            <div class="rounded-xl border border-amber-200 bg-white p-4 space-y-2">
              <p class="leading-relaxed">
                Hemos registrado tu reporte de transferencia por <strong>S/ {{ gatewayAmount }}</strong> con el comprobante <strong>{{ voucherFileName }}</strong>.
              </p>
              <p class="leading-relaxed text-slate-500">
                Por políticas de seguridad y prevención de fraudes, el abono se aplicará a tu cronograma automáticamente tan pronto concilie con nuestro extracto bancario.
              </p>
            </div>

            <div class="rounded-lg bg-amber-100/60 p-3 text-amber-900 flex items-center gap-2">
              <Info class="h-4 w-4 shrink-0 text-amber-700" />
              <span>Puedes consultar el avance en la pestaña "Historial de Pagos".</span>
            </div>
          </CardContent>

          <CardFooter class="flex justify-between border-t border-slate-100 pt-4">
            <Button variant="outline" size="sm" @click="activeTab = 'HISTORY'">
              Ver en Historial
            </Button>
            <Button variant="default" size="sm" @click="resetGateway">
              Aceptar
            </Button>
          </CardFooter>
        </Card>
      </div>
    </div>

    <!-- PESTAÑA 2: CANCELACIÓN ANTICIPADA TOTAL (FE-CP-16 / US-30) -->
    <div v-else-if="activeTab === 'PREPAY_TOTAL'" class="space-y-6">
      <Card>
        <CardHeader>
          <div class="flex items-center justify-between">
            <CardTitle class="flex items-center gap-2 text-lg">
              <Sparkles class="h-5 w-5 text-emerald-600" />
              Liquidación por Cancelación Anticipada (Prepago Total)
            </CardTitle>
            <Badge class="bg-emerald-100 text-emerald-800">Ley 29571 / SBS</Badge>
          </div>
          <CardDescription>
            Calcula la deuda exacta a la fecha de hoy, ahorrando los intereses futuros no devengados.
          </CardDescription>
        </CardHeader>

        <CardContent class="space-y-6">
          <!-- Loading state -->
          <div v-if="isQuoting" class="py-12 text-center space-y-2">
            <LoaderCircle class="mx-auto h-8 w-8 animate-spin text-brand-600" />
            <p class="text-sm text-slate-500">Calculando cotización al día de hoy...</p>
          </div>

          <!-- Error state -->
          <div v-else-if="quoteError" class="rounded-lg border border-rose-200 bg-rose-50 p-4 text-xs text-rose-800">
            <p class="font-semibold">No se pudo cotizar:</p>
            <p>{{ quoteError }}</p>
          </div>

          <!-- Cotización calculada -->
          <div v-else-if="quote" class="grid gap-6 md:grid-cols-2">
            <!-- Desglose Financiero -->
            <div class="space-y-3 rounded-xl border border-slate-200 bg-slate-50 p-5 text-sm">
              <h3 class="font-semibold text-slate-900 border-b border-slate-200 pb-2">
                Desglose de Liquidación al {{ formatBusinessDate(quote.asOfDate) }}
              </h3>
              <div class="flex justify-between">
                <span class="text-slate-600">Saldo Capital Insoluto</span>
                <span class="font-medium text-slate-900">S/ {{ quote.outstandingPrincipal }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-600">Interés Devengado a la Fecha</span>
                <span class="font-medium text-slate-900">S/ {{ quote.accruedInterest }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-600">Mora Acumulada</span>
                <span class="font-medium text-slate-900">S/ {{ quote.accruedMora }}</span>
              </div>
              <div class="flex justify-between text-emerald-700">
                <span class="font-medium">Comisión / Penalidad por Prepago</span>
                <span class="font-bold">S/ {{ quote.prepaymentPenalty }} (0.00%)</span>
              </div>
              <div class="flex justify-between border-t-2 border-slate-300 pt-3 text-base">
                <span class="font-bold text-slate-900">Total a Liquidar Hoy</span>
                <span class="font-extrabold text-brand-700 text-lg">S/ {{ quote.totalToPay }}</span>
              </div>
            </div>

            <!-- Beneficios y Vigencia -->
            <div class="space-y-4">
              <div class="rounded-xl border border-emerald-200 bg-emerald-50/70 p-4 text-xs text-emerald-900 space-y-2">
                <div class="flex items-center gap-2 font-bold text-sm text-emerald-950">
                  <CheckCircle2 class="h-4 w-4 text-emerald-600" />
                  <span>Beneficio SBS: Cero Penalidades</span>
                </div>
                <p class="leading-relaxed">
                  Conforme al Código de Protección al Consumidor y normas de la SBS, tienes derecho a cancelar tu crédito en cualquier momento sin comisión de prepago y con descuento íntegro de cuotas e intereses futuros.
                </p>
                <p class="font-medium text-emerald-800">
                  Vigencia de esta cotización: <strong>Hoy hasta las 23:59 hrs</strong>.
                </p>
              </div>

              <!-- CTA de pago -->
              <Button class="w-full bg-emerald-600 hover:bg-emerald-700" size="lg" @click="proceedFromTotalQuote">
                <Wallet class="mr-2 h-5 w-5" />
                <span>Pagar Liquidación Total (S/ {{ quote.totalToPay }})</span>
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- PESTAÑA 3: ABONO EXTRAORDINARIO / PREPAGO PARCIAL (FE-CP-17) -->
    <div v-else-if="activeTab === 'PREPAY_PARTIAL'" class="space-y-6">
      <Card>
        <CardHeader>
          <div class="flex items-center justify-between">
            <CardTitle class="flex items-center gap-2 text-lg">
              <TrendingDown class="h-5 w-5 text-brand-600" />
              Simulador de Abono a Capital (Prepago Parcial)
            </CardTitle>
            <Badge class="bg-brand-100 text-brand-800">Ley 29571 Art. 85</Badge>
          </div>
          <CardDescription>
            Amortiza un monto extraordinario a tu capital adeudado y elige cómo deseas beneficiarte.
          </CardDescription>
        </CardHeader>

        <CardContent class="space-y-6">
          <div class="grid gap-6 md:grid-cols-2">
            <!-- Controles del Simulador -->
            <div class="space-y-4">
              <div class="space-y-1.5">
                <label for="partial-amount" class="text-xs font-semibold uppercase tracking-wider text-slate-700">
                  Monto extraordinario a amortizar (S/)
                </label>
                <div class="relative">
                  <span class="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3 font-semibold text-slate-500">
                    S/
                  </span>
                  <input
                    id="partial-amount"
                    v-model="partialAmount"
                    type="number"
                    step="50"
                    placeholder="Ej. 1000.00"
                    class="w-full rounded-lg border border-slate-300 bg-white py-2.5 pl-9 pr-3 text-base font-bold text-slate-900 focus:border-brand-500 focus:ring-1 focus:ring-brand-500"
                  />
                </div>
                <p class="text-[11px] text-slate-500">
                  Saldo capital disponible: S/ {{ activeLoan?.outstandingPrincipal }}
                </p>
              </div>

              <!-- Elección de modalidad (Ley 29571) -->
              <div class="space-y-2">
                <label class="text-xs font-semibold uppercase tracking-wider text-slate-700">
                  Modalidad de Amortización:
                </label>
                <div class="grid grid-cols-2 gap-2">
                  <button
                    type="button"
                    :class="[
                      partialOption === 'REDUCE_TERM'
                        ? 'border-brand-600 bg-brand-50/60 font-semibold text-brand-900 ring-2 ring-brand-500'
                        : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                      'rounded-lg border p-3 text-left text-xs transition-all',
                    ]"
                    @click="partialOption = 'REDUCE_TERM'; handleSimulatePartial()"
                  >
                    <p class="font-bold">Reducir Plazo</p>
                    <span class="text-[11px] text-slate-500">Pagas antes, ahorras más intereses</span>
                  </button>

                  <button
                    type="button"
                    :class="[
                      partialOption === 'REDUCE_FEE'
                        ? 'border-brand-600 bg-brand-50/60 font-semibold text-brand-900 ring-2 ring-brand-500'
                        : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                      'rounded-lg border p-3 text-left text-xs transition-all',
                    ]"
                    @click="partialOption = 'REDUCE_FEE'; handleSimulatePartial()"
                  >
                    <p class="font-bold">Reducir Cuota</p>
                    <span class="text-[11px] text-slate-500">Mismo plazo, cuotas más cómodas</span>
                  </button>
                </div>
              </div>

              <Button
                :disabled="isSimulatingPartial || !parseFloat(partialAmount) || !selectedLoanId"
                class="w-full"
                @click="handleSimulatePartial"
              >
                <LoaderCircle v-if="isSimulatingPartial" class="mr-2 h-4 w-4 animate-spin" />
                <span>Simular Nuevo Cronograma</span>
              </Button>

              <div v-if="partialError" class="rounded-lg border border-rose-200 bg-rose-50 p-3 text-xs text-rose-800">
                {{ partialError }}
              </div>
            </div>

            <!-- Resultado de la Simulación -->
            <div class="rounded-xl border border-slate-200 bg-slate-50 p-5 space-y-4">
              <h3 class="font-semibold text-slate-900 border-b border-slate-200 pb-2 text-sm">
                Comparativo de Impacto
              </h3>

              <div v-if="partialSimulation" class="space-y-3 text-xs">
                <div class="grid grid-cols-2 gap-3 rounded-lg bg-white p-3 border border-slate-200">
                  <div>
                    <span class="text-slate-500 block">Cuotas restantes</span>
                    <span class="text-sm font-bold text-slate-900">
                      {{ partialSimulation.newTermRemaining }} cuotas
                    </span>
                    <span class="text-[10px] text-slate-400 block line-through">
                      Antes: {{ partialSimulation.currentTermRemaining }}
                    </span>
                  </div>
                  <div>
                    <span class="text-slate-500 block">Valor por cuota</span>
                    <span class="text-sm font-bold text-slate-900">
                      S/ {{ partialSimulation.newInstallmentAmount }}
                    </span>
                    <span class="text-[10px] text-slate-400 block line-through">
                      Antes: S/ {{ partialSimulation.currentInstallmentAmount }}
                    </span>
                  </div>
                </div>

                <div class="rounded-lg bg-emerald-50 border border-emerald-200 p-3 text-emerald-900">
                  <span class="text-[11px] font-semibold block text-emerald-800 uppercase">
                    Ahorro Total Proyectado de Intereses
                  </span>
                  <span class="text-xl font-extrabold text-emerald-700">
                    S/ {{ partialSimulation.totalInterestSaved }}
                  </span>
                </div>

                <Button class="w-full bg-brand-600 hover:bg-brand-700 mt-2" @click="proceedFromPartialSimulation">
                  <ArrowRight class="mr-2 h-4 w-4" />
                  <span>Abonar S/ {{ partialSimulation.prepaymentAmount }}</span>
                </Button>
              </div>

              <div v-else class="py-8 text-center text-xs text-slate-400">
                Ingresa un monto y presiona "Simular" para proyectar tu ahorro.
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- PESTAÑA 4: HISTORIAL DE PAGOS (FE-CP-13, FE-CP-15) -->
    <div v-else-if="activeTab === 'HISTORY'" class="space-y-6">
      <Card>
        <CardHeader class="pb-3">
          <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <CardTitle class="text-lg">Historial de Pagos Realizados</CardTitle>
              <CardDescription>
                Comprobantes oficiales de tus cuotas y reportes de transferencias.
              </CardDescription>
            </div>

            <!-- Filtros de Estado -->
            <div class="flex items-center gap-1.5 text-xs">
              <button
                type="button"
                :class="[
                  statusFilter === 'ALL'
                    ? 'bg-slate-900 text-white'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200',
                  'rounded-lg px-2.5 py-1 font-medium transition-colors',
                ]"
                @click="statusFilter = 'ALL'"
              >
                Todos ({{ payments.length }})
              </button>
              <button
                type="button"
                :class="[
                  statusFilter === 'PENDING_CONFIRMATION'
                    ? 'bg-amber-600 text-white'
                    : 'bg-amber-50 text-amber-800 hover:bg-amber-100',
                  'rounded-lg px-2.5 py-1 font-medium transition-colors',
                ]"
                @click="statusFilter = 'PENDING_CONFIRMATION'"
              >
                En Validación ({{ pendingCount }})
              </button>
              <button
                type="button"
                :class="[
                  statusFilter === 'APPLIED'
                    ? 'bg-emerald-600 text-white'
                    : 'bg-emerald-50 text-emerald-800 hover:bg-emerald-100',
                  'rounded-lg px-2.5 py-1 font-medium transition-colors',
                ]"
                @click="statusFilter = 'APPLIED'"
              >
                Aplicados ({{ appliedCount }})
              </button>
            </div>
          </div>
        </CardHeader>

        <CardContent class="p-0">
          <!-- Loading -->
          <div v-if="historyLoading" class="p-8 text-center">
            <LoaderCircle class="mx-auto h-6 w-6 animate-spin text-brand-600" />
          </div>

          <!-- Empty state -->
          <div v-else-if="filteredPayments.length === 0" class="p-8 text-center text-slate-500 text-sm">
            No se encontraron pagos con los filtros seleccionados.
          </div>

          <!-- Tabla de Pagos -->
          <div v-else class="overflow-x-auto">
            <table class="w-full text-left text-xs">
              <thead class="border-y border-slate-200 bg-slate-50 font-semibold text-slate-600 uppercase tracking-wider">
                <tr>
                  <th class="py-3 px-4">Fecha</th>
                  <th class="py-3 px-4">Recibo / Ref</th>
                  <th class="py-3 px-4">Canal</th>
                  <th class="py-3 px-4 text-right">Monto</th>
                  <th class="py-3 px-4">Estado</th>
                  <th class="py-3 px-4 text-right">Comprobante</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                <tr v-for="p in filteredPayments" :key="p.id" class="hover:bg-slate-50/60">
                  <td class="py-3 px-4 font-medium text-slate-800">
                    {{ formatInstant(p.paidAt) }}
                  </td>
                  <td class="py-3 px-4 font-mono text-slate-600">
                    {{ p.receiptNumber ?? p.externalReference ?? p.id }}
                  </td>
                  <td class="py-3 px-4 text-slate-700">
                    {{ p.method }}
                  </td>
                  <td class="py-3 px-4 text-right font-bold text-slate-900">
                    S/ {{ p.amount }}
                  </td>
                  <td class="py-3 px-4">
                    <Badge
                      v-if="p.status === 'PENDING_CONFIRMATION'"
                      class="bg-amber-100 text-amber-800"
                    >
                      En validación
                    </Badge>
                    <Badge
                      v-else-if="p.status === 'APPLIED'"
                      class="bg-emerald-100 text-emerald-800"
                    >
                      Aplicado
                    </Badge>
                    <Badge
                      v-else
                      class="bg-rose-100 text-rose-800"
                    >
                      {{ p.status }}
                    </Badge>
                  </td>
                  <td class="py-3 px-4 text-right">
                    <Button
                      v-if="p.status === 'APPLIED'"
                      variant="ghost"
                      size="sm"
                      class="h-8 gap-1 text-brand-600 hover:text-brand-700"
                      @click="downloadHistoryReceipt(p)"
                    >
                      <Download class="h-3.5 w-3.5" />
                      <span>PDF</span>
                    </Button>
                    <span v-else class="text-slate-400 text-[11px]">—</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>
    </div>
  </div>
</template>
