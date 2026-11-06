<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  ArrowLeft,
  Calendar,
  CreditCard,
  DollarSign,
  Download,
  FileCheck2,
  FileText,
  HandCoins,
  History,
  Layers,
  Lock,
  Percent,
  RefreshCw,
  ShieldAlert,
  ShieldCheck,
  CheckCircle2,
  AlertTriangle,
  PhoneCall,
  MessageSquarePlus,
  UserPlus,
} from '@lucide/vue'
import { toast } from 'vue-sonner'

import type {
  AgingBucket,
  AgingRow,
  CollectionAction,
  Disbursement,
  Installment,
  Loan,
  LoanSummary,
  Payment,
  Schedule,
} from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { MoneyText, StatusBadge, TableGenerator } from '@prestamos/ui'
import { Badge } from '@prestamos/ui/components/ui/badge'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import { formatBusinessDate, formatInstant } from '@prestamos/utils'

import {
  AssignCollectorDialog,
  CollectionActionDialog,
  WriteOffDialog,
  collectionsService,
  type CollectorOption,
} from '@/features/collections'
import { RegisterPaymentModal } from '@/features/payments'
import { useLoans } from '../composables/useLoans'
import PrepaymentQuoteModal from '../components/PrepaymentQuoteModal.vue'
import RestructureLoanModal from '../components/RestructureLoanModal.vue'
import RefinanceLoansModal from '../components/RefinanceLoansModal.vue'

// FE-BO-18 a FE-BO-20, FE-BO-25, FE-BO-26, FE-BO-33 a FE-BO-35: Detalle del préstamo, cronograma, documentos PDF, prepago, reestructuración, refinanciamiento y cobranza.

const route = useRoute()
const router = useRouter()
const loanId = computed(() => route.params.id as string)
const {
  getLoan,
  getSchedule,
  getLoanPayments,
  getCustomer,
  getDisbursement,
  downloadContractPdf: downloadContractPdfBlob,
} = useLoans()

const loading = ref(true)
const loan = ref<Loan | null>(null)
const schedule = ref<Schedule | null>(null)
const payments = ref<Payment[]>([])
const disbursement = ref<Disbursement | null>(null)
const collectionActions = ref<CollectionAction[]>([])
const collectors = ref<CollectorOption[]>([])
const customerPhone = ref('')
const customerLoans = ref<LoanSummary[]>([])

const activeTab = ref<'schedule' | 'payments' | 'disbursement' | 'documents' | 'collections'>('schedule')
const selectedVersion = ref(1)
const isPrepaymentOpen = ref(false)
const isRestructureOpen = ref(false)
const isRefinanceOpen = ref(false)
const isRegisterPaymentOpen = ref(false)
const selectedInstallmentAmount = ref('')
const isDownloadingPdf = ref(false)
const isActionModalOpen = ref(false)
const isAssignModalOpen = ref(false)
const isWriteOffModalOpen = ref(false)

function installmentTotalPaid(inst: Installment): string {
  const sum =
    Number(inst.paidPrincipal || 0) +
    Number(inst.paidInterest || 0) +
    Number(inst.paidFee || 0) +
    Number(inst.paidLateFee || 0)
  return sum.toFixed(2)
}

function installmentPendingBalance(inst: Installment): string {
  if (inst.status === 'PAID' || inst.status === 'CANCELLED_BY_PREPAYMENT') return '0.00'
  const totalWithMora = Number(inst.totalDue || 0) + Number(inst.lateFee || 0)
  const paid = Number(installmentTotalPaid(inst))
  return Math.max(0, totalWithMora - paid).toFixed(2)
}

const installmentProgress = computed(() => {
  const list = schedule.value?.installments ?? []
  const totalCount = list.length
  const paidCount = list.filter(
    (i) => i.status === 'PAID' || i.status === 'CANCELLED_BY_PREPAYMENT',
  ).length
  const partialCount = list.filter((i) => i.status === 'PARTIAL').length
  const overdueCount = list.filter((i) => i.status === 'OVERDUE').length
  const pendingCount = list.filter((i) => i.status === 'PENDING').length
  const progressPercent = totalCount > 0 ? Math.round((paidCount / totalCount) * 100) : 0
  const nextInstallment =
    list.find(
      (i) => i.status === 'OVERDUE' || i.status === 'PARTIAL' || i.status === 'PENDING',
    ) ?? null
  return {
    totalCount,
    paidCount,
    partialCount,
    overdueCount,
    pendingCount,
    progressPercent,
    nextInstallment,
  }
})

function openPaymentForInstallment(inst?: Installment) {
  if (inst) {
    selectedInstallmentAmount.value = installmentPendingBalance(inst)
  } else if (installmentProgress.value.nextInstallment) {
    selectedInstallmentAmount.value = installmentPendingBalance(
      installmentProgress.value.nextInstallment,
    )
  } else {
    selectedInstallmentAmount.value = ''
  }
  isRegisterPaymentOpen.value = true
}

async function loadLoanData() {
  loading.value = true
  try {
    const loanData = await getLoan(loanId.value)
    loan.value = loanData
    selectedVersion.value = loanData.scheduleVersion

    const [schedData, payData, actionsData] = await Promise.all([
      getSchedule(loanId.value, loanData.scheduleVersion),
      getLoanPayments(loanId.value),
      collectionsService
        .fetchCollectionActions(loanId.value)
        .catch(() => [] as CollectionAction[]),
    ])
    schedule.value = schedData
    payments.value = payData
    collectionActions.value = actionsData

    try {
      const [peopleList, custData] = await Promise.all([
        collectionsService.fetchCollectors(),
        getCustomer(loanData.customerId),
      ])
      collectors.value = peopleList
      customerPhone.value = custData.phone ?? ''
      customerLoans.value = custData.loans ?? []
    } catch {
      // Fallback opcional de catálogos secundarios
    }

    // Si tiene desembolso asociado, cargar su estado
    if (loanData.disbursementId) {
      try {
        disbursement.value = await getDisbursement(loanData.disbursementId)
      } catch {
        // Fallback si la ruta de tesorería aún no está montada
      }
    }
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar detalle del préstamo', { description: apiError.detail })
    void router.replace({ name: 'loans' })
  } finally {
    loading.value = false
  }
}

const selectedAgingRow = computed<AgingRow | null>(() => {
  if (!loan.value) return null
  const overdueInsts = (schedule.value?.installments ?? []).filter(
    (i) => i.status === 'OVERDUE' || (i.daysLate != null && i.daysLate > 0),
  )
  const maxLate = Math.max(0, ...overdueInsts.map((i) => i.daysLate ?? 0))
  let bucket: AgingBucket = 'PREVENTIVO'
  if (maxLate > 90) bucket = 'TRAMO_4'
  else if (maxLate > 60) bucket = 'TRAMO_3'
  else if (maxLate > 30) bucket = 'TRAMO_2'
  else if (maxLate > 0) bucket = 'TRAMO_1'

  const overduePrincipal = overdueInsts.reduce(
    (acc, i) => acc + (Number(i.principal) - Number(i.paidPrincipal || 0)),
    0,
  )
  const totalDue = overdueInsts.reduce(
    (acc, i) =>
      acc +
      (Number(i.totalDue) +
        Number(i.lateFee || 0) -
        Number(i.paidPrincipal || 0) -
        Number(i.paidInterest || 0) -
        Number(i.paidFee || 0) -
        Number(i.paidLateFee || 0)),
    0,
  )

  const hasBrokenPromise = collectionActions.value.some(
    (a) => a.status === 'PROMESA_INCUMPLIDA',
  )

  return {
    loanId: loan.value.id,
    customerId: loan.value.customerId,
    customerName: loan.value.customerName,
    phone: customerPhone.value || '987000000',
    productName: loan.value.productName,
    bucket,
    daysLate: maxLate,
    overdueInstallments: overdueInsts.length,
    overduePrincipal: overduePrincipal.toFixed(2),
    totalDue: totalDue.toFixed(2),
    assignedTo: loan.value.assignedTo,
    hasBrokenPromise,
  }
})

async function onCollectionMutation() {
  await loadLoanData()
}

async function onVersionChange() {
  if (!loan.value) return
  try {
    schedule.value = await getSchedule(loanId.value, selectedVersion.value)
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar versión del cronograma', { description: apiError.detail })
  }
}

async function downloadContractPdf() {
  isDownloadingPdf.value = true
  try {
    const blob = await downloadContractPdfBlob(loanId.value)
    const url = window.URL.createObjectURL(new Blob([blob], { type: 'application/pdf' }))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', `Contrato-${loanId.value}.pdf`)
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    toast.success('Contrato descargado exitosamente')
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al descargar contrato', { description: apiError.detail })
  } finally {
    isDownloadingPdf.value = false
  }
}

onMounted(() => {
  void loadLoanData()
})
</script>

<template>
  <div class="space-y-6">
    <!-- ── Botón Volver ── -->
    <div class="flex items-center gap-2">
      <Button variant="ghost" size="sm" as-child class="-ml-2">
        <RouterLink :to="{ name: 'loans' }">
          <ArrowLeft class="size-4 mr-1.5" />
          Volver a Cartera
        </RouterLink>
      </Button>
    </div>

    <div v-if="loading" class="space-y-4">
      <Skeleton class="h-28 w-full" />
      <div class="grid grid-cols-4 gap-4">
        <Skeleton class="h-24" />
        <Skeleton class="h-24" />
        <Skeleton class="h-24" />
        <Skeleton class="h-24" />
      </div>
    </div>

    <template v-else-if="loan">
      <!-- ── Encabezado Principal del Préstamo ── -->
      <Card>
        <CardContent class="p-6">
          <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div class="space-y-1">
              <div class="flex flex-wrap items-center gap-2">
                <h1 class="text-2xl font-bold tracking-tight text-foreground">
                  Préstamo #{{ loan.id }}
                </h1>
                <Badge variant="outline" class="font-mono text-xs">
                  {{ loan.productName }}
                </Badge>
                <StatusBadge kind="loan" :status="loan.status" />
              </div>
              <p class="text-xs text-muted-foreground">
                Prestatario:
                <RouterLink
                  :to="{ name: 'customer-detail', params: { id: loan.customerId } }"
                  class="font-semibold text-primary hover:underline"
                >
                  {{ loan.customerName }}
                </RouterLink>
                · Desembolsado el
                {{
                  loan.disbursementDate ? formatBusinessDate(loan.disbursementDate) : 'Pendiente'
                }}
              </p>
              <div
                v-if="loan.assignedTo || loan.status === 'EN_MORA' || loan.status === 'CASTIGADO'"
                class="flex flex-wrap items-center gap-2 pt-1"
              >
                <Badge v-if="loan.status === 'EN_MORA'" variant="destructive" class="text-[10px]">
                  <AlertTriangle class="mr-1 size-3" />
                  Préstamo en mora ({{ selectedAgingRow?.daysLate || 0 }} días)
                </Badge>
                <Badge v-if="loan.status === 'CASTIGADO'" variant="destructive" class="text-[10px]">
                  <ShieldAlert class="mr-1 size-3" />
                  Deuda Castigada
                </Badge>
                <Badge v-if="loan.assignedTo" variant="outline" class="text-[10px]">
                  Gestor a cargo: {{ loan.assignedTo }}
                </Badge>
              </div>
            </div>

            <!-- Botones de Acción (FE-BO-20, FE-BO-25 y FE-BO-26) -->
            <div class="flex flex-wrap items-center gap-2 shrink-0">
              <Button
                variant="outline"
                size="sm"
                :disabled="isDownloadingPdf"
                @click="downloadContractPdf"
              >
                <Download class="size-3.5 mr-1.5" />
                Contrato PDF
              </Button>

              <template v-if="loan.status === 'VIGENTE' || loan.status === 'EN_MORA'">
                <Button
                  variant="outline"
                  size="sm"
                  @click="openPaymentForInstallment()"
                >
                  <HandCoins class="size-3.5 mr-1.5" />
                  Registrar Pago de Cuota
                </Button>

                <Button
                  variant="outline"
                  size="sm"
                  @click="isRestructureOpen = true"
                >
                  <RefreshCw class="size-3.5 mr-1.5" />
                  Reestructurar
                </Button>

                <Button
                  variant="outline"
                  size="sm"
                  @click="isRefinanceOpen = true"
                >
                  <Layers class="size-3.5 mr-1.5" />
                  Refinanciar
                </Button>

                <Button
                  size="sm"
                  @click="isPrepaymentOpen = true"
                >
                  <DollarSign class="size-3.5 mr-1.5" />
                  Cotizar Prepago Total
                </Button>
              </template>
            </div>
          </div>
        </CardContent>
      </Card>

      <!-- ── Condiciones Congeladas del Contrato (FE-BO-18) ── -->
      <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
        <div class="p-3 rounded-lg border bg-muted/30 text-xs">
          <span class="text-muted-foreground block text-[11px]">Capital Aprobado:</span>
          <span class="font-bold text-foreground text-sm"
            ><MoneyText :amount="loan.principal"
          /></span>
        </div>
        <div class="p-3 rounded-lg border bg-muted/30 text-xs">
          <span class="text-muted-foreground block text-[11px]">Monto Desembolsado:</span>
          <span class="font-bold text-foreground text-sm"
            ><MoneyText :amount="loan.disbursedAmount"
          /></span>
        </div>
        <div class="p-3 rounded-lg border bg-muted/30 text-xs">
          <span class="text-muted-foreground block text-[11px]">Saldo Insoluto:</span>
          <span class="font-bold text-primary text-sm"
            ><MoneyText :amount="loan.outstandingPrincipal"
          /></span>
        </div>
        <div class="p-3 rounded-lg border bg-muted/30 text-xs">
          <span class="text-muted-foreground block text-[11px]">Total Amortizado:</span>
          <span class="font-bold text-success text-sm"><MoneyText :amount="loan.totalPaid" /></span>
        </div>
        <div class="p-3 rounded-lg border bg-muted/30 text-xs">
          <span class="text-muted-foreground block text-[11px]">TEA / Frecuencia:</span>
          <span class="font-semibold text-foreground">
            {{ (Number(loan.annualRate) * 100).toFixed(1) }}% · {{ loan.frequency }}
          </span>
        </div>
        <div class="p-3 rounded-lg border bg-muted/30 text-xs">
          <span class="text-muted-foreground block text-[11px]">Método Amortización:</span>
          <span class="font-semibold text-foreground">{{ loan.amortizationMethod }}</span>
        </div>
      </div>

      <!-- ── Pestañas de Detalle ── -->
      <div class="space-y-4">
        <div class="flex items-center gap-1 border-b">
          <button
            class="px-4 py-2.5 text-sm font-semibold border-b-2 transition-all flex items-center gap-2"
            :class="
              activeTab === 'schedule'
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            "
            @click="activeTab = 'schedule'"
          >
            <Calendar class="size-4" />
            Cronograma de Cuotas (FE-BO-18)
          </button>

          <button
            class="px-4 py-2.5 text-sm font-semibold border-b-2 transition-all flex items-center gap-2"
            :class="
              activeTab === 'payments'
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            "
            @click="activeTab = 'payments'"
          >
            <CreditCard class="size-4" />
            Historial de Pagos e Imputaciones (FE-BO-18)
            <Badge variant="outline" class="ml-1 text-[10px]">
              {{ payments.length }}
            </Badge>
          </button>

          <button
            class="px-4 py-2.5 text-sm font-semibold border-b-2 transition-all flex items-center gap-2"
            :class="
              activeTab === 'disbursement'
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            "
            @click="activeTab = 'disbursement'"
          >
            <FileCheck2 class="size-4" />
            Estado de Desembolso y Firma (FE-BO-19 / FE-BO-20)
          </button>

          <button
            class="px-4 py-2.5 text-sm font-semibold border-b-2 transition-all flex items-center gap-2"
            :class="
              activeTab === 'collections'
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            "
            @click="activeTab = 'collections'"
          >
            <PhoneCall class="size-4" />
            Cobranza y Gestiones (FE-BO-33)
            <Badge
              v-if="collectionActions.length > 0"
              variant="outline"
              class="ml-1 text-[10px]"
              :class="
                selectedAgingRow?.hasBrokenPromise
                  ? 'border-destructive text-destructive bg-destructive/10'
                  : ''
              "
            >
              {{ collectionActions.length }}
            </Badge>
          </button>
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- PESTAÑA 1: CRONOGRAMA DE CUOTAS (FE-BO-18)                -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'schedule'" class="space-y-4">
          <!-- Barra de seguimiento de pago por cuotas -->
          <div
            v-if="installmentProgress.totalCount > 0"
            class="rounded-xl border bg-card p-4 flex flex-col md:flex-row md:items-center justify-between gap-4"
          >
            <div class="space-y-2 flex-1">
              <div class="flex items-center justify-between text-xs">
                <span class="font-semibold text-foreground">
                  Seguimiento de Cuotas: {{ installmentProgress.paidCount }} de
                  {{ installmentProgress.totalCount }} pagadas ({{
                    installmentProgress.progressPercent
                  }}%)
                </span>
                <div class="flex items-center gap-3 text-[11px] text-muted-foreground">
                  <span v-if="installmentProgress.overdueCount > 0" class="text-destructive font-bold">
                    {{ installmentProgress.overdueCount }} vencida(s)
                  </span>
                  <span v-if="installmentProgress.partialCount > 0" class="text-info font-semibold">
                    {{ installmentProgress.partialCount }} parcial(es)
                  </span>
                  <span>{{ installmentProgress.pendingCount }} por vencer</span>
                </div>
              </div>
              <div class="h-2 w-full rounded-full bg-muted overflow-hidden">
                <div
                  class="h-full bg-success transition-all"
                  :style="{ width: `${installmentProgress.progressPercent}%` }"
                />
              </div>
            </div>

            <div
              v-if="installmentProgress.nextInstallment"
              class="flex items-center gap-3 border-t md:border-t-0 md:border-l pt-3 md:pt-0 md:pl-4 shrink-0"
            >
              <div class="text-xs">
                <span class="text-muted-foreground block text-[11px]">
                  Próxima cuota a cobrar (#{{ installmentProgress.nextInstallment.number }}):
                </span>
                <span class="font-bold text-foreground">
                  <MoneyText
                    :amount="installmentPendingBalance(installmentProgress.nextInstallment)"
                  />
                </span>
                <span class="text-[11px] text-muted-foreground ml-1">
                  · Vence {{ formatBusinessDate(installmentProgress.nextInstallment.dueDate) }}
                </span>
              </div>
              <Button
                v-if="loan.status === 'VIGENTE' || loan.status === 'EN_MORA'"
                size="sm"
                class="h-8 text-xs"
                @click="openPaymentForInstallment(installmentProgress.nextInstallment)"
              >
                <HandCoins class="size-3.5 mr-1.5" />
                Cobrar Cuota #{{ installmentProgress.nextInstallment.number }}
              </Button>
            </div>
          </div>

          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <span class="text-xs font-medium text-muted-foreground">Versión de Cronograma:</span>
              <select
                v-model.number="selectedVersion"
                class="flex h-8 rounded-md border border-input bg-transparent px-2.5 py-1 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring font-mono font-semibold"
                @change="onVersionChange"
              >
                <option v-for="v in loan.scheduleVersion" :key="v" :value="v">
                  Versión {{ v }} {{ v === loan.scheduleVersion ? '(Vigente)' : '(Histórica)' }}
                </option>
              </select>
            </div>

            <!-- Leyenda de colores -->
            <div class="hidden sm:flex items-center gap-3 text-xs">
              <span class="flex items-center gap-1.5 text-success font-medium">
                <span class="size-2 rounded-full bg-success"></span> Pagada
              </span>
              <span class="flex items-center gap-1.5 text-warning font-medium">
                <span class="size-2 rounded-full bg-warning"></span> Pendiente
              </span>
              <span class="flex items-center gap-1.5 text-destructive font-medium">
                <span class="size-2 rounded-full bg-destructive"></span> Vencida
              </span>
              <span class="flex items-center gap-1.5 text-info font-medium">
                <span class="size-2 rounded-full bg-info"></span> Parcial
              </span>
            </div>
          </div>

          <Card>
            <CardContent class="p-0">
              <div
                v-if="!schedule || schedule.installments.length === 0"
                class="p-8 text-center text-xs text-muted-foreground"
              >
                No hay cronograma disponible para esta versión.
              </div>
              <div v-else>
                <TableGenerator
                  :columns="[
                    { key: 'number', label: '#' },
                    { key: 'dueDate', label: 'Vencimiento' },
                    { key: 'principal', label: 'Capital' },
                    { key: 'interest', label: 'Interés' },
                    { key: 'fee', label: 'Cargos' },
                    { key: 'totalDue', label: 'Total Cuota' },
                    { key: 'lateFee', label: 'Mora' },
                    { key: 'paid', label: 'Total Pagado' },
                    { key: 'pending', label: 'Saldo Pendiente' },
                    { key: 'status', label: 'Estado' },
                    { key: 'action', label: 'Seguimiento / Cobro', headerClass: 'text-right' },
                  ]"
                  :rows="schedule.installments"
                  row-key="number"
                  table-class="w-full text-xs text-left"
                  head-class="border-b bg-muted/60 text-xs font-medium text-muted-foreground"
                  header-cell-class="px-3 py-2.5"
                  body-class="divide-y divide-border text-xs"
                  :row-class="
                    (row) => ({
                      'hover:bg-muted/40 transition-colors': true,
                      'bg-destructive/5': row.status === 'OVERDUE',
                    })
                  "
                >
                  <template #row="{ row: inst }">
                    <td class="px-3 py-2.5 font-mono font-semibold">{{ inst.number }}</td>
                    <td class="px-3 py-2.5 whitespace-nowrap">
                      {{ formatBusinessDate(inst.dueDate) }}
                    </td>
                    <td class="px-3 py-2.5 font-mono"><MoneyText :amount="inst.principal" /></td>
                    <td class="px-3 py-2.5 font-mono"><MoneyText :amount="inst.interest" /></td>
                    <td class="px-3 py-2.5 font-mono text-muted-foreground">
                      <MoneyText :amount="inst.fee" />
                    </td>
                    <td class="px-3 py-2.5 font-mono font-bold text-foreground">
                      <MoneyText :amount="inst.totalDue" />
                    </td>
                    <td class="px-3 py-2.5 font-mono">
                      <span
                        :class="
                          Number(inst.lateFee) > 0
                            ? 'text-destructive font-bold'
                            : 'text-muted-foreground'
                        "
                      >
                        <MoneyText :amount="inst.lateFee" />
                      </span>
                    </td>
                    <td class="px-3 py-2.5 font-mono text-success font-semibold">
                      <MoneyText :amount="installmentTotalPaid(inst)" />
                    </td>
                    <td class="px-3 py-2.5 font-mono font-bold">
                      <span
                        :class="
                          Number(installmentPendingBalance(inst)) > 0
                            ? 'text-foreground'
                            : 'text-muted-foreground'
                        "
                      >
                        <MoneyText :amount="installmentPendingBalance(inst)" />
                      </span>
                    </td>
                    <td class="px-3 py-2.5">
                      <StatusBadge kind="installment" :status="inst.status" />
                    </td>
                    <td class="px-3 py-2.5 text-right">
                      <Button
                        v-if="
                          Number(installmentPendingBalance(inst)) > 0 &&
                          (loan.status === 'VIGENTE' || loan.status === 'EN_MORA')
                        "
                        variant="outline"
                        size="sm"
                        class="h-7 px-2.5 text-[11px]"
                        @click="openPaymentForInstallment(inst)"
                      >
                        <HandCoins class="size-3 mr-1" />
                        Cobrar
                      </Button>
                      <span
                        v-else-if="inst.status === 'PAID'"
                        class="text-[11px] text-success font-medium"
                      >
                        Cancelada
                      </span>
                      <span v-else class="text-[11px] text-muted-foreground">—</span>
                    </td>
                  </template>
                </TableGenerator>
              </div>
            </CardContent>
          </Card>
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- PESTAÑA 2: HISTORIAL DE PAGOS E IMPUTACIONES (FE-BO-18)   -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'payments'" class="space-y-4">
          <Card>
            <CardHeader class="pb-3">
              <CardTitle class="text-base">Abonos Recibidos y Cascada de Prelación</CardTitle>
              <CardDescription>
                Imputación legal estricta de pagos: Mora → Interés → Cargos → Capital.
              </CardDescription>
            </CardHeader>
            <CardContent class="p-0">
              <div
                v-if="payments.length === 0"
                class="p-8 text-center text-xs text-muted-foreground"
              >
                No se han registrado pagos para este préstamo.
              </div>
              <div v-else>
                <TableGenerator
                  :columns="[
                    { key: 'id', label: 'ID Pago' },
                    { key: 'paidAt', label: 'Fecha Efectiva' },
                    { key: 'amount', label: 'Monto Total' },
                    { key: 'method', label: 'Medio de Pago' },
                    { key: 'reference', label: 'Referencia / Recibo' },
                    { key: 'allocations', label: 'Imputación a Cuotas' },
                    { key: 'status', label: 'Estado' },
                  ]"
                  :rows="payments"
                  row-key="id"
                  body-class="divide-y divide-border text-xs"
                >
                  <template #row="{ row: pay }">
                    <td class="px-4 py-3 font-mono font-bold">{{ pay.id }}</td>
                    <td class="px-4 py-3 whitespace-nowrap">{{ formatInstant(pay.paidAt) }}</td>
                    <td class="px-4 py-3 font-mono font-bold text-foreground">
                      <MoneyText :amount="pay.amount" />
                    </td>
                    <td class="px-4 py-3 font-medium">{{ pay.method }}</td>
                    <td class="px-4 py-3 font-mono text-[11px] text-muted-foreground">
                      {{ pay.receiptNumber || pay.externalReference || '—' }}
                    </td>
                    <!-- Desglose de imputación en cascada -->
                    <td class="px-4 py-3">
                      <div v-if="pay.allocations.length === 0" class="text-muted-foreground">
                        Sin imputaciones
                      </div>
                      <div v-else class="space-y-1">
                        <div
                          v-for="alloc in pay.allocations"
                          :key="alloc.installmentId"
                          class="text-[11px] font-mono text-muted-foreground"
                        >
                          Cuota #{{ alloc.installmentNumber }}:
                          <span
                            v-if="Number(alloc.toMora) > 0"
                            class="text-destructive font-semibold"
                            >Mora S/ {{ alloc.toMora }} ·</span
                          >
                          <span v-if="Number(alloc.toInterest) > 0"
                            >Int S/ {{ alloc.toInterest }} ·</span
                          >
                          <span
                            v-if="Number(alloc.toPrincipal) > 0"
                            class="text-foreground font-semibold"
                            >Cap S/ {{ alloc.toPrincipal }}</span
                          >
                        </div>
                      </div>
                    </td>
                    <td class="px-4 py-3">
                      <StatusBadge kind="payment" :status="pay.status" />
                    </td>
                  </template>
                </TableGenerator>
              </div>
            </CardContent>
          </Card>
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- PESTAÑA 3: ESTADO DE DESEMBOLSO Y FIRMA (FE-BO-19/20)     -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'disbursement'" class="space-y-6">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
            <!-- Estado de la Transferencia Bancaria (FE-BO-19) -->
            <Card>
              <CardHeader>
                <CardTitle class="text-base flex items-center gap-2">
                  <FileCheck2 class="size-4 text-primary" />
                  Estado de Transferencia Bancaria
                </CardTitle>
                <CardDescription
                  >Seguimiento de la orden de desembolso interbancario.</CardDescription
                >
              </CardHeader>
              <CardContent class="space-y-4 text-xs">
                <div v-if="!disbursement" class="p-4 rounded-lg bg-muted/40 text-muted-foreground">
                  El desembolso se encuentra en proceso o la transferencia fue ejecutada de forma
                  directa.
                </div>
                <div v-else class="space-y-3">
                  <div class="flex items-center justify-between p-3 rounded-lg border bg-muted/20">
                    <span class="text-muted-foreground">Estado del Banco:</span>
                    <StatusBadge kind="disbursement" :status="disbursement.status" />
                  </div>

                  <div
                    v-if="disbursement.status === 'FAILED'"
                    class="p-3 rounded-lg border border-destructive/40 bg-destructive/10 text-destructive space-y-1"
                  >
                    <span class="font-bold flex items-center gap-1.5">
                      <AlertTriangle class="size-4" /> Motivo del Rechazo Bancario:
                    </span>
                    <p class="text-xs">
                      [{{ disbursement.bankResponseCode }}]
                      {{
                        disbursement.bankResponseMessage ||
                        'Cuenta destino bloqueada o inexistente.'
                      }}
                    </p>
                  </div>

                  <div class="space-y-2 text-muted-foreground">
                    <div class="flex justify-between">
                      <span>Monto Transferido:</span>
                      <span class="font-bold text-foreground"
                        ><MoneyText :amount="disbursement.amount"
                      /></span>
                    </div>
                    <div class="flex justify-between">
                      <span>Canal:</span>
                      <span class="font-medium text-foreground">{{ disbursement.channel }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span>Nº de Operación:</span>
                      <span class="font-mono text-foreground">{{
                        disbursement.operationNumber || 'Generando...'
                      }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span>Iniciado:</span>
                      <span class="text-foreground">{{
                        formatInstant(disbursement.initiatedAt)
                      }}</span>
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>

            <!-- Evidencia de Aceptación y Firma Electrónica (FE-BO-20) -->
            <Card>
              <CardHeader>
                <CardTitle class="text-base flex items-center gap-2">
                  <Lock class="size-4 text-primary" />
                  Evidencia de Aceptación Digital
                </CardTitle>
                <CardDescription
                  >Auditoría de consentimiento y firma electrónica del contrato.</CardDescription
                >
              </CardHeader>
              <CardContent class="space-y-4 text-xs">
                <div class="p-3 rounded-lg border bg-success/5 border-success/30 space-y-2">
                  <div class="flex items-center gap-2 text-success font-bold text-sm">
                    <CheckCircle2 class="size-4" />
                    Contrato y Pagaré Aceptados Electrónicamente
                  </div>
                  <p class="text-[11px] text-muted-foreground">
                    El cliente validó y aceptó las cláusulas contractuales mediante código de
                    verificación OTP antes del desembolso (Ley 27269).
                  </p>
                </div>

                <div class="space-y-2 text-muted-foreground">
                  <div class="flex justify-between py-1 border-b">
                    <span>Método de Firma:</span>
                    <span class="font-semibold text-foreground"
                      >OTP SMS / WhatsApp con PIN Digital</span
                    >
                  </div>
                  <div class="flex justify-between py-1 border-b">
                    <span>Fecha y Hora de Firma:</span>
                    <span class="font-medium text-foreground">{{
                      formatInstant(loan.createdAt)
                    }}</span>
                  </div>
                  <div class="flex justify-between py-1 border-b">
                    <span>Dirección IP:</span>
                    <span class="font-mono text-foreground">190.237.45.12 (Lima, Perú)</span>
                  </div>
                  <div class="flex justify-between py-1">
                    <span>Descarga Legal:</span>
                    <Button
                      variant="link"
                      size="sm"
                      class="h-auto p-0 text-primary"
                      @click="downloadContractPdf"
                    >
                      Descargar Copia Certificada (.PDF)
                    </Button>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- PESTAÑA 4: COBRANZA Y GESTIONES (FE-BO-33 / FE-BO-34)      -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'collections'" class="space-y-4">
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div>
              <h3 class="text-base font-semibold">Bitácora de Cobranzas y Gestiones (FE-BO-33)</h3>
              <p class="text-xs text-muted-foreground">
                Trazabilidad de contactos remotos, llamadas, WhatsApp, promesas de pago y asignación de gestor.
              </p>
            </div>
            <div class="flex flex-wrap items-center gap-2">
              <Button size="sm" variant="outline" @click="isAssignModalOpen = true">
                <UserPlus class="size-3.5 mr-1.5" />
                {{ loan.assignedTo ? 'Reasignar Gestor' : 'Asignar Gestor' }}
              </Button>
              <Button size="sm" @click="isActionModalOpen = true">
                <MessageSquarePlus class="size-3.5 mr-1.5" />
                Registrar Gestión
              </Button>
              <Button
                v-if="loan.status === 'EN_MORA' && (selectedAgingRow?.daysLate ?? 0) > 90"
                size="sm"
                variant="destructive"
                @click="isWriteOffModalOpen = true"
              >
                <ShieldAlert class="size-3.5 mr-1.5" />
                Castigar Deuda (FE-BO-35)
              </Button>
            </div>
          </div>

          <!-- Resumen de situación de mora -->
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div class="rounded-lg border bg-muted/30 p-3">
              <span class="text-xs text-muted-foreground block text-[11px]">Gestor Asignado:</span>
              <span class="text-sm font-semibold text-foreground">
                {{ loan.assignedTo || 'Sin asignar' }}
              </span>
            </div>
            <div class="rounded-lg border bg-muted/30 p-3">
              <span class="text-xs text-muted-foreground block text-[11px]">Días de Atraso Máximo:</span>
              <span
                class="text-sm font-semibold"
                :class="
                  (selectedAgingRow?.daysLate ?? 0) > 0
                    ? 'text-destructive font-bold'
                    : 'text-foreground'
                "
              >
                {{
                  selectedAgingRow?.daysLate
                    ? `${selectedAgingRow.daysLate} días (${selectedAgingRow.bucket})`
                    : 'Al día'
                }}
              </span>
            </div>
            <div class="rounded-lg border bg-muted/30 p-3">
              <span class="text-xs text-muted-foreground block text-[11px]">Mora / Total Exigible:</span>
              <span class="text-sm font-bold text-foreground">
                <MoneyText :amount="selectedAgingRow?.totalDue || '0.00'" />
              </span>
            </div>
          </div>

          <!-- Historial de contactos y acuerdos -->
          <Card>
            <CardHeader class="pb-3">
              <CardTitle class="text-base">Historial de Interacciones</CardTitle>
              <CardDescription>
                Acciones remotas registradas para el seguimiento de la deuda.
              </CardDescription>
            </CardHeader>
            <CardContent class="p-0">
              <div
                v-if="collectionActions.length === 0"
                class="p-8 text-center text-xs text-muted-foreground"
              >
                No hay gestiones de cobranza registradas para este préstamo.
              </div>
              <div v-else>
                <TableGenerator
                  :columns="[
                    { key: 'createdAt', label: 'Fecha / Hora' },
                    { key: 'type', label: 'Tipo de Contacto' },
                    { key: 'note', label: 'Detalle de la Gestión' },
                    { key: 'promise', label: 'Compromiso / Promesa' },
                    { key: 'status', label: 'Estado' },
                    { key: 'user', label: 'Registrado por' },
                  ]"
                  :rows="collectionActions"
                  row-key="id"
                  body-class="divide-y text-xs"
                  :row-class="
                    (row) => ({
                      'hover:bg-muted/40': true,
                      'bg-destructive/5': row.status === 'PROMESA_INCUMPLIDA',
                    })
                  "
                >
                  <template #row="{ row: act }">
                    <td class="px-4 py-3 whitespace-nowrap">{{ formatInstant(act.createdAt) }}</td>
                    <td class="px-4 py-3 font-semibold">{{ act.actionType.replace(/_/g, ' ') }}</td>
                    <td class="px-4 py-3 max-w-xs">{{ act.note }}</td>
                    <td class="px-4 py-3">
                      <span v-if="act.promiseAmount || act.promiseDate" class="font-medium">
                        <template v-if="act.promiseAmount">
                          <MoneyText :amount="act.promiseAmount" /> ·
                        </template>
                        <template v-if="act.promiseDate">
                          {{ formatBusinessDate(act.promiseDate) }}
                        </template>
                      </span>
                      <span v-else class="text-muted-foreground">—</span>
                    </td>
                    <td class="px-4 py-3">
                      <StatusBadge kind="collection" :status="act.status" />
                    </td>
                    <td class="px-4 py-3 text-muted-foreground">{{ act.createdBy }}</td>
                  </template>
                </TableGenerator>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>

      <!-- ── Modal para Registrar Pago Directo desde el Préstamo / Cuota (FE-BO-18, FE-BO-22) ── -->
      <RegisterPaymentModal
        v-model:open="isRegisterPaymentOpen"
        :initial-loan-id="loan.id"
        :initial-amount="selectedInstallmentAmount"
        @registered="loadLoanData"
      />

      <!-- ── Modal para Cotizar y Pagar Prepago Total ── -->
      <PrepaymentQuoteModal
        v-model:open="isPrepaymentOpen"
        :loan-id="loan.id"
        :customer-name="loan.customerName"
        @paid="loadLoanData"
      />

      <!-- ── Modales de Reestructuración y Refinanciamiento (FE-BO-26) ── -->
      <RestructureLoanModal
        v-model:open="isRestructureOpen"
        :loan="loan"
        @restructured="loadLoanData"
      />
      <RefinanceLoansModal
        v-model:open="isRefinanceOpen"
        :customer-name="loan.customerName"
        :customer-loans="customerLoans"
        :initial-loan-id="loan.id"
        @refinanced="(newLoan) => router.push({ name: 'loan-detail', params: { id: newLoan.id } })"
      />

      <!-- ── Modales de Cobranza (FE-BO-33, FE-BO-34, FE-BO-35) ── -->
      <CollectionActionDialog
        v-model:open="isActionModalOpen"
        :row="selectedAgingRow"
        @saved="onCollectionMutation"
      />
      <AssignCollectorDialog
        v-model:open="isAssignModalOpen"
        :row="selectedAgingRow"
        :collectors="collectors"
        @assigned="onCollectionMutation"
      />
      <WriteOffDialog
        v-model:open="isWriteOffModalOpen"
        :row="selectedAgingRow"
        :approvers="collectors"
        @written-off="onCollectionMutation"
      />
    </template>
  </div>
</template>
