<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import {
  AlertCircle,
  ArrowRight,
  CheckCircle2,
  Clock,
  CreditCard,
  Download,
  Info,
  LoaderCircle,
  QrCode,
  Receipt,
  ShieldCheck,
  Sparkles,
  TrendingDown,
  UploadCloud,
  Wallet,
} from '@lucide/vue'

import type { PaymentMethodType } from '@prestamos/api-client'
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
import { formatBusinessDate, formatInstant } from '@prestamos/utils'

import { useClientLoans, useLoanSchedule } from '@/features/loans'
import {
  usePaymentGateway,
  usePaymentHistory,
  usePrepayment,
} from '../composables'

// Requerimientos FE-CP-12 a FE-CP-17
const route = useRoute()

const activeTab = ref<'PAY' | 'PREPAY_TOTAL' | 'PREPAY_PARTIAL' | 'HISTORY'>('PAY')

const {
  loans,
  selectedLoanId,
  activeLoan,
  loadLoans,
} = useClientLoans()

const {
  nextInstallment: nextPendingInstallment,
  loadSchedule,
} = useLoanSchedule()

const {
  payments,
  filteredPayments,
  isLoading: historyLoading,
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

const {
  loanId: gatewayLoanId,
  amount: gatewayAmount,
  installmentNumber: gatewayInstallmentNumber,
  concept: gatewayConcept,
  selectedMethod,
  voucherFileName,
  step: gatewayStep,
  isSubmitting: gatewaySubmitting,
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
} = usePaymentGateway({
  onSuccess: () => {
    void loadLoans()
    if (selectedLoanId.value) {
      void loadSchedule(selectedLoanId.value)
    }
    void fetchHistory()
  },
})

watch(gatewayStep, (newStep) => {
  if (newStep === 'IN_VALIDATION') {
    void fetchHistory()
  }
})

const selectLoan = (id: string) => {
  if (!id) return
  const changed = selectedLoanId.value !== id
  selectedLoanId.value = id
  if (changed) {
    resetGateway()
    gatewayAmount.value = ''
    gatewayInstallmentNumber.value = undefined
    resetQuote()
    resetPartial()
    if (activeTab.value === 'PREPAY_TOTAL') {
      void loadTotalPrepaymentQuote(id)
    }
  }
  void loadSchedule(id)
}

watch(selectedLoanId, (newId) => {
  if (newId) void loadSchedule(newId)
})

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

// Soporte de query params: ?tab=prepago|abono|historial | ?mode=PREPAYMENT & ?loanId=...
onMounted(async () => {
  await loadLoans()

  if (route.query.loanId && typeof route.query.loanId === 'string') {
    selectLoan(route.query.loanId)
  }

  if (route.query.tab === 'prepago' || route.query.mode === 'PREPAYMENT') {
    activeTab.value = 'PREPAY_TOTAL'
    void handleLoadQuote()
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
    void handleLoadQuote()
  }
})

// Preparar pago desde Prepago Total
const proceedFromTotalQuote = () => {
  if (!quote.value) return
  resetGateway()
  gatewayInstallmentNumber.value = undefined
  gatewayAmount.value = quote.value.totalToPay
  gatewayConcept.value = `Cancelación anticipada total (${activeLoan.value?.productName ?? 'Préstamo'})`
  activeTab.value = 'PAY'
}

// Preparar pago desde Abono Extraordinario
const proceedFromPartialSimulation = () => {
  if (!partialSimulation.value) return
  resetGateway()
  gatewayInstallmentNumber.value = undefined
  gatewayAmount.value = partialSimulation.value.prepaymentAmount
  gatewayConcept.value = `Abono extraordinario a capital - ${
    partialSimulation.value.option === 'REDUCE_TERM' ? 'Reducción de plazo' : 'Reducción de cuota'
  }`
  activeTab.value = 'PAY'
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
          class="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-800 shadow-sm focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
          @change="selectLoan(($event.target as HTMLSelectElement).value)"
        >
          <option v-for="l in loans" :key="l.id" :value="l.id">
            {{ l.productName }} · S/ {{ l.principal }} ({{ l.id }})
          </option>
        </select>
      </div>
    </div>

    <!-- Navegación por pestañas -->
    <div class="border-b border-border">
      <nav class="-mb-px flex flex-wrap gap-2 sm:gap-6" aria-label="Tabs">
        <button
          type="button"
          :class="[
            activeTab === 'PAY'
              ? 'border-primary text-primary font-semibold'
              : 'border-transparent text-muted-foreground hover:border-border hover:text-foreground',
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
              ? 'border-primary text-primary font-semibold'
              : 'border-transparent text-muted-foreground hover:border-border hover:text-foreground',
            'flex items-center gap-2 border-b-2 py-3 px-1 text-sm font-medium transition-colors',
          ]"
          @click="activeTab = 'PREPAY_TOTAL'"
        >
          <Sparkles class="h-4 w-4" />
          <span>Cancelación Anticipada (Total)</span>
          <Badge variant="outline" class="border-success/30 bg-success/10 text-[11px] text-success">
            0% penalidad
          </Badge>
        </button>

        <button
          type="button"
          :class="[
            activeTab === 'PREPAY_PARTIAL'
              ? 'border-primary text-primary font-semibold'
              : 'border-transparent text-muted-foreground hover:border-border hover:text-foreground',
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
              ? 'border-primary text-primary font-semibold'
              : 'border-transparent text-muted-foreground hover:border-border hover:text-foreground',
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
              <CreditCard class="h-5 w-5 text-primary" />
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
                    ? 'border-primary bg-primary/5 ring-2 ring-primary'
                    : 'border-border bg-card hover:border-muted-foreground/30 hover:bg-muted/30',
                  'relative flex flex-col items-start rounded-xl border p-4 text-left transition-all',
                ]"
                @click="selectedMethod = m.id"
              >
                <div class="flex w-full items-center justify-between">
                  <span class="font-semibold text-foreground">{{ m.name }}</span>
                  <Badge v-if="m.badge" class="bg-primary/10 text-primary border-primary/20 text-[10px]">
                    {{ m.badge }}
                  </Badge>
                </div>
                <span class="mt-1 text-xs text-muted-foreground leading-relaxed">{{ m.tagline }}</span>
              </button>
            </div>

            <!-- Campo de Monto -->
            <div class="space-y-2 rounded-xl border border-border bg-muted/20 p-4">
              <label for="amount-input" class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Monto a pagar (S/)
              </label>
              <div class="relative rounded-lg shadow-sm">
                <span class="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3 font-semibold text-muted-foreground">
                  S/
                </span>
                <input
                  id="amount-input"
                  v-model="gatewayAmount"
                  type="number"
                  step="0.01"
                  min="1"
                  placeholder="0.00"
                  class="w-full rounded-lg border border-input bg-card py-2.5 pl-9 pr-3 text-lg font-bold text-foreground focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                />
              </div>

              <!-- Atajos de monto -->
              <div class="flex flex-wrap items-center gap-2 pt-2">
                <span class="text-xs text-muted-foreground">Sugerencias:</span>
                <button
                  v-if="nextPendingInstallment"
                  type="button"
                  class="rounded-full border border-border bg-card px-2.5 py-1 text-xs font-medium text-foreground hover:border-primary hover:text-primary transition-colors"
                  @click="gatewayAmount = nextPendingInstallment.totalDue; gatewayConcept = `Cuota N° ${nextPendingInstallment.number}`"
                >
                  Próxima cuota (S/ {{ nextPendingInstallment.totalDue }})
                </button>
                <button
                  v-if="activeLoan?.outstandingPrincipal"
                  type="button"
                  class="rounded-full border border-border bg-card px-2.5 py-1 text-xs font-medium text-foreground hover:border-primary hover:text-primary transition-colors"
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

          <Card class="bg-primary/5 border-primary/20">
            <CardContent class="p-4 space-y-2 text-xs text-foreground">
              <div class="flex items-center gap-2 font-semibold text-primary">
                <ShieldCheck class="h-4 w-4 text-primary" />
                <span>Pago 100% Protegido</span>
              </div>
              <p class="leading-relaxed text-muted-foreground">
                Tus pagos son procesados con encriptación de nivel bancario. Los comprobantes oficiales se generan de forma instantánea con validez tributaria.
              </p>
            </CardContent>
          </Card>
        </div>
      </div>

      <!-- PASO B: ESPERANDO PAGO CON QR DINÁMICO O TARJETA (FE-CP-12, FE-CP-14) -->
      <div v-else-if="gatewayStep === 'AWAITING_PAYMENT'" class="mx-auto max-w-xl">
        <Card class="border-primary/20 shadow-md">
          <CardHeader class="text-center pb-2">
            <div class="mx-auto mb-2 flex h-12 w-12 items-center justify-center rounded-full bg-primary/10 text-primary">
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
                <span class="text-[10px] font-bold uppercase text-success bg-success/10 px-2 py-0.5 rounded">PCI-DSS Level 1</span>
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
        <Card class="border-success/30 bg-gradient-to-b from-success/5 to-card shadow-lg">
          <CardHeader class="text-center pb-3">
            <div class="mx-auto mb-2 flex h-16 w-16 items-center justify-center rounded-full bg-success/15 text-success">
              <CheckCircle2 class="h-10 w-10" />
            </div>
            <CardTitle class="text-2xl font-bold text-success">¡Pago Confirmado con Éxito!</CardTitle>
            <CardDescription class="text-muted-foreground">
              Tu cuota ha sido amortizada y registrada en el sistema.
            </CardDescription>
          </CardHeader>

          <CardContent class="space-y-4">
            <div class="rounded-xl border border-border bg-card p-4 space-y-2.5 text-sm">
              <div class="flex justify-between">
                <span class="text-muted-foreground">N° de Recibo</span>
                <span class="font-mono font-bold text-foreground">
                  {{ confirmedPayment?.receiptNumber ?? 'REC-20261106-0001' }}
                </span>
              </div>
              <div class="flex justify-between">
                <span class="text-muted-foreground">Monto Abonado</span>
                <span class="font-bold text-success">S/ {{ confirmedPayment?.amount ?? gatewayAmount }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-muted-foreground">Medio de Pago</span>
                <span class="font-medium text-foreground">{{ confirmedPayment?.method ?? selectedMethod }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-muted-foreground">Fecha y Hora</span>
                <span class="text-foreground">{{ confirmedPayment?.paidAt ? formatInstant(confirmedPayment.paidAt) : 'Hoy' }}</span>
              </div>
              <div class="flex justify-between border-t border-border pt-2">
                <span class="text-muted-foreground">Estado</span>
                <Badge class="bg-success/15 text-success border-success/30 font-semibold">APLICADO</Badge>
              </div>
            </div>

            <!-- Botón de descarga de comprobante PDF oficial -->
            <Button class="w-full bg-success hover:bg-success/90 text-white font-semibold" size="lg" @click="downloadGatewayReceipt">
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
              <Sparkles class="h-5 w-5 text-success" />
              Liquidación por Cancelación Anticipada (Prepago Total)
            </CardTitle>
            <Badge class="bg-success/15 text-success border-success/30 font-semibold">Ley 29571 / SBS</Badge>
          </div>
          <CardDescription>
            Calcula la deuda exacta a la fecha de hoy, ahorrando los intereses futuros no devengados.
          </CardDescription>
        </CardHeader>

        <CardContent class="space-y-6">
          <!-- Loading state -->
          <div v-if="isQuoting" class="py-12 text-center space-y-2">
            <LoaderCircle class="mx-auto h-8 w-8 animate-spin text-primary" />
            <p class="text-sm text-muted-foreground">Calculando cotización al día de hoy...</p>
          </div>

          <!-- Error state -->
          <div v-else-if="quoteError" class="rounded-lg border border-destructive/30 bg-destructive/10 p-4 text-xs text-destructive">
            <p class="font-semibold">No se pudo cotizar:</p>
            <p>{{ quoteError }}</p>
          </div>

          <!-- Cotización calculada -->
          <div v-else-if="quote" class="grid gap-6 md:grid-cols-2">
            <!-- Desglose Financiero -->
            <div class="space-y-3 rounded-xl border border-border bg-muted/20 p-5 text-sm">
              <h3 class="font-semibold text-foreground border-b border-border pb-2">
                Desglose de Liquidación al {{ formatBusinessDate(quote.asOfDate) }}
              </h3>
              <div class="flex justify-between">
                <span class="text-muted-foreground">Saldo Capital Insoluto</span>
                <span class="font-medium text-foreground">S/ {{ quote.outstandingPrincipal }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-muted-foreground">Interés Devengado a la Fecha</span>
                <span class="font-medium text-foreground">S/ {{ quote.accruedInterest }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-muted-foreground">Mora Acumulada</span>
                <span class="font-medium text-foreground">S/ {{ quote.accruedMora }}</span>
              </div>
              <div class="flex justify-between text-success">
                <span class="font-medium">Comisión / Penalidad por Prepago</span>
                <span class="font-bold">S/ {{ quote.prepaymentPenalty }} (0.00%)</span>
              </div>
              <div class="flex justify-between border-t-2 border-border pt-3 text-base">
                <span class="font-bold text-foreground">Total a Liquidar Hoy</span>
                <span class="font-extrabold text-primary text-lg">S/ {{ quote.totalToPay }}</span>
              </div>
            </div>

            <!-- Beneficios y Vigencia -->
            <div class="space-y-4">
              <div class="rounded-xl border border-success/30 bg-success/10 p-4 text-xs text-foreground space-y-2">
                <div class="flex items-center gap-2 font-bold text-sm text-success">
                  <CheckCircle2 class="h-4 w-4 text-success" />
                  <span>Beneficio SBS: Cero Penalidades</span>
                </div>
                <p class="leading-relaxed">
                  Conforme al Código de Protección al Consumidor y normas de la SBS, tienes derecho a cancelar tu crédito en cualquier momento sin comisión de prepago y con descuento íntegro de cuotas e intereses futuros.
                </p>
                <p class="font-medium text-muted-foreground">
                  Vigencia de esta cotización: <strong>Hoy hasta las 23:59 hrs</strong>.
                </p>
              </div>

              <!-- CTA de pago -->
              <Button class="w-full bg-primary hover:bg-primary/90 text-primary-foreground font-semibold" size="lg" @click="proceedFromTotalQuote">
                <Wallet class="mr-2 h-5 w-5" />
                <span>Pagar Liquidación Total (S/ {{ quote.totalToPay }})</span>
              </Button>

              <RouterLink
                :to="{ path: '/prestamos', query: { loanId: selectedLoanId } }"
                class="text-xs text-primary hover:underline text-center block pt-1 font-medium"
              >
                Consultar Estado de Cuenta Oficial y Movimientos Históricos →
              </RouterLink>
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
              <TrendingDown class="h-5 w-5 text-primary" />
              Simulador de Abono a Capital (Prepago Parcial)
            </CardTitle>
            <Badge class="bg-primary/10 text-primary border-primary/20">Ley 29571 Art. 85</Badge>
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
                <label for="partial-amount" class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Monto extraordinario a amortizar (S/)
                </label>
                <div class="relative">
                  <span class="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3 font-semibold text-muted-foreground">
                    S/
                  </span>
                  <input
                    id="partial-amount"
                    v-model="partialAmount"
                    type="number"
                    step="50"
                    placeholder="Ej. 1000.00"
                    class="w-full rounded-lg border border-input bg-card py-2.5 pl-9 pr-3 text-base font-bold text-foreground focus:border-primary focus:ring-1 focus:ring-primary"
                  />
                </div>
                <p class="text-[11px] text-muted-foreground">
                  Saldo capital disponible: S/ {{ activeLoan?.outstandingPrincipal }}
                </p>
              </div>

              <!-- Elección de modalidad (Ley 29571) -->
              <div class="space-y-2">
                <label class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Modalidad de Amortización:
                </label>
                <div class="grid grid-cols-2 gap-2">
                  <button
                    type="button"
                    :class="[
                      partialOption === 'REDUCE_TERM'
                        ? 'border-primary bg-primary/5 font-semibold text-primary ring-2 ring-primary'
                        : 'border-border bg-card text-foreground hover:bg-muted/30',
                      'rounded-lg border p-3 text-left text-xs transition-all',
                    ]"
                    @click="partialOption = 'REDUCE_TERM'; handleSimulatePartial()"
                  >
                    <p class="font-bold">Reducir Plazo</p>
                    <span class="text-[11px] text-muted-foreground">Pagas antes, ahorras más intereses</span>
                  </button>

                  <button
                    type="button"
                    :class="[
                      partialOption === 'REDUCE_FEE'
                        ? 'border-primary bg-primary/5 font-semibold text-primary ring-2 ring-primary'
                        : 'border-border bg-card text-foreground hover:bg-muted/30',
                      'rounded-lg border p-3 text-left text-xs transition-all',
                    ]"
                    @click="partialOption = 'REDUCE_FEE'; handleSimulatePartial()"
                  >
                    <p class="font-bold">Reducir Cuota</p>
                    <span class="text-[11px] text-muted-foreground">Mismo plazo, cuotas más cómodas</span>
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

              <div v-if="partialError" class="rounded-lg border border-destructive/30 bg-destructive/10 p-3 text-xs text-destructive">
                {{ partialError }}
              </div>
            </div>

            <!-- Resultado de la Simulación -->
            <div class="rounded-xl border border-border bg-muted/20 p-5 space-y-4">
              <h3 class="font-semibold text-foreground border-b border-border pb-2 text-sm">
                Comparativo de Impacto
              </h3>

              <div v-if="partialSimulation" class="space-y-3 text-xs">
                <div class="grid grid-cols-2 gap-3 rounded-lg bg-card p-3 border border-border">
                  <div>
                    <span class="text-muted-foreground block">Cuotas restantes</span>
                    <span class="text-sm font-bold text-foreground">
                      {{ partialSimulation.newTermRemaining }} cuotas
                    </span>
                    <span class="text-[10px] text-muted-foreground block line-through">
                      Antes: {{ partialSimulation.currentTermRemaining }}
                    </span>
                  </div>
                  <div>
                    <span class="text-muted-foreground block">Valor por cuota</span>
                    <span class="text-sm font-bold text-foreground">
                      S/ {{ partialSimulation.newInstallmentAmount }}
                    </span>
                    <span class="text-[10px] text-muted-foreground block line-through">
                      Antes: S/ {{ partialSimulation.currentInstallmentAmount }}
                    </span>
                  </div>
                </div>

                <div class="rounded-lg bg-success/10 border border-success/30 p-3 text-foreground">
                  <span class="text-[11px] font-semibold block text-success uppercase">
                    Ahorro Total Proyectado de Intereses
                  </span>
                  <span class="text-xl font-extrabold text-success">
                    S/ {{ partialSimulation.totalInterestSaved }}
                  </span>
                </div>

                <Button class="w-full bg-primary hover:bg-primary/90 text-primary-foreground font-semibold mt-2" @click="proceedFromPartialSimulation">
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
                    ? 'bg-success text-white'
                    : 'bg-success/10 text-success hover:bg-success/20',
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
            <LoaderCircle class="mx-auto h-6 w-6 animate-spin text-primary" />
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
                      class="bg-success/15 text-success border border-success/30 font-medium"
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
                      class="h-8 gap-1 text-primary hover:text-primary/80"
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
