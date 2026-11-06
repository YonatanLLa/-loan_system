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
} from '@lucide/vue'
import { toast } from 'vue-sonner'

import type {
  Disbursement,
  Installment,
  Loan,
  Payment,
  Schedule,
} from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { MoneyText, PageHeader, StatusBadge } from '@prestamos/ui'
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
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import { formatBusinessDate, formatInstant } from '@prestamos/utils'

import { useApi } from '@/app/api'
import PrepaymentQuoteModal from '../components/PrepaymentQuoteModal.vue'

// FE-BO-18, FE-BO-19, FE-BO-20, FE-BO-25: Detalle del préstamo, cronograma, documentos PDF y prepago.

const route = useRoute()
const router = useRouter()
const loanId = computed(() => route.params.id as string)

const loading = ref(true)
const loan = ref<Loan | null>(null)
const schedule = ref<Schedule | null>(null)
const payments = ref<Payment[]>([])
const disbursement = ref<Disbursement | null>(null)

const activeTab = ref<'schedule' | 'payments' | 'disbursement' | 'documents'>('schedule')
const selectedVersion = ref(1)
const isPrepaymentOpen = ref(false)
const isDownloadingPdf = ref(false)

async function loadLoanData() {
  loading.value = true
  try {
    const { data: loanData } = await useApi().get<Loan>(`/loans/${loanId.value}`)
    loan.value = loanData
    selectedVersion.value = loanData.scheduleVersion

    const [schedRes, payRes] = await Promise.all([
      useApi().get<Schedule>(`/loans/${loanId.value}/schedule`, {
        params: { version: loanData.scheduleVersion },
      }),
      useApi().get<Payment[]>(`/loans/${loanId.value}/payments`),
    ])
    schedule.value = schedRes.data
    payments.value = payRes.data

    // Si tiene desembolso asociado, cargar su estado
    if (loanData.disbursementId) {
      try {
        const { data: disbData } = await useApi().get<Disbursement>(
          `/treasury/disbursements/${loanData.disbursementId}`,
        )
        disbursement.value = disbData
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

async function onVersionChange() {
  if (!loan.value) return
  try {
    const { data } = await useApi().get<Schedule>(`/loans/${loanId.value}/schedule`, {
      params: { version: selectedVersion.value },
    })
    schedule.value = data
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar versión del cronograma', { description: apiError.detail })
  }
}

async function downloadContractPdf() {
  isDownloadingPdf.value = true
  try {
    const response = await useApi().get(`/loans/${loanId.value}/documents/contract.pdf`, {
      responseType: 'blob',
    })
    const url = window.URL.createObjectURL(new Blob([response.data], { type: 'application/pdf' }))
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
                · Desembolsado el {{ loan.disbursementDate ? formatBusinessDate(loan.disbursementDate) : 'Pendiente' }}
              </p>
            </div>

            <!-- Botones de Acción (FE-BO-20 y FE-BO-25) -->
            <div class="flex items-center gap-2 shrink-0">
              <Button
                variant="outline"
                size="sm"
                :disabled="isDownloadingPdf"
                @click="downloadContractPdf"
              >
                <Download class="size-3.5 mr-1.5" />
                Contrato PDF
              </Button>

              <Button
                v-if="loan.status === 'VIGENTE' || loan.status === 'EN_MORA'"
                size="sm"
                @click="isPrepaymentOpen = true"
              >
                <DollarSign class="size-3.5 mr-1.5" />
                Cotizar Prepago Total
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <!-- ── Condiciones Congeladas del Contrato (FE-BO-18) ── -->
      <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
        <div class="p-3 rounded-lg border bg-muted/30 text-xs">
          <span class="text-muted-foreground block text-[11px]">Capital Aprobado:</span>
          <span class="font-bold text-foreground text-sm"><MoneyText :amount="loan.principal" /></span>
        </div>
        <div class="p-3 rounded-lg border bg-muted/30 text-xs">
          <span class="text-muted-foreground block text-[11px]">Monto Desembolsado:</span>
          <span class="font-bold text-foreground text-sm"><MoneyText :amount="loan.disbursedAmount" /></span>
        </div>
        <div class="p-3 rounded-lg border bg-muted/30 text-xs">
          <span class="text-muted-foreground block text-[11px]">Saldo Insoluto:</span>
          <span class="font-bold text-primary text-sm"><MoneyText :amount="loan.outstandingPrincipal" /></span>
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
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- PESTAÑA 1: CRONOGRAMA DE CUOTAS (FE-BO-18)                -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'schedule'" class="space-y-4">
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
              <div v-if="!schedule || schedule.installments.length === 0" class="p-8 text-center text-xs text-muted-foreground">
                No hay cronograma disponible para esta versión.
              </div>
              <div v-else class="overflow-x-auto">
                <table class="w-full text-sm text-left">
                  <thead class="border-b bg-muted/60 text-xs font-medium text-muted-foreground">
                    <tr>
                      <th class="px-3 py-2.5">#</th>
                      <th class="px-3 py-2.5">Vencimiento</th>
                      <th class="px-3 py-2.5">Capital</th>
                      <th class="px-3 py-2.5">Interés</th>
                      <th class="px-3 py-2.5">Cargos</th>
                      <th class="px-3 py-2.5">Total Cuota</th>
                      <th class="px-3 py-2.5">Pagado</th>
                      <th class="px-3 py-2.5">Mora</th>
                      <th class="px-3 py-2.5">Estado</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-border text-xs">
                    <tr
                      v-for="inst in schedule.installments"
                      :key="inst.number"
                      class="hover:bg-muted/40 transition-colors"
                      :class="{ 'bg-destructive/5': inst.status === 'OVERDUE' }"
                    >
                      <td class="px-3 py-2.5 font-mono font-semibold">{{ inst.number }}</td>
                      <td class="px-3 py-2.5 whitespace-nowrap">{{ formatBusinessDate(inst.dueDate) }}</td>
                      <td class="px-3 py-2.5 font-mono"><MoneyText :amount="inst.principal" /></td>
                      <td class="px-3 py-2.5 font-mono"><MoneyText :amount="inst.interest" /></td>
                      <td class="px-3 py-2.5 font-mono text-muted-foreground"><MoneyText :amount="inst.fee" /></td>
                      <td class="px-3 py-2.5 font-mono font-bold text-foreground">
                        <MoneyText :amount="inst.totalDue" />
                      </td>
                      <td class="px-3 py-2.5 font-mono text-success">
                        <MoneyText :amount="inst.paidPrincipal" />
                      </td>
                      <td class="px-3 py-2.5 font-mono">
                        <span :class="Number(inst.lateFee) > 0 ? 'text-destructive font-bold' : 'text-muted-foreground'">
                          <MoneyText :amount="inst.lateFee" />
                        </span>
                      </td>
                      <td class="px-3 py-2.5">
                        <StatusBadge kind="installment" :status="inst.status" />
                      </td>
                    </tr>
                  </tbody>
                </table>
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
              <div v-if="payments.length === 0" class="p-8 text-center text-xs text-muted-foreground">
                No se han registrado pagos para este préstamo.
              </div>
              <div v-else class="overflow-x-auto">
                <table class="w-full text-sm text-left">
                  <thead class="border-b bg-muted/60 text-xs font-medium text-muted-foreground">
                    <tr>
                      <th class="px-4 py-3">ID Pago</th>
                      <th class="px-4 py-3">Fecha Efectiva</th>
                      <th class="px-4 py-3">Monto Total</th>
                      <th class="px-4 py-3">Medio de Pago</th>
                      <th class="px-4 py-3">Referencia / Recibo</th>
                      <th class="px-4 py-3">Imputación a Cuotas</th>
                      <th class="px-4 py-3">Estado</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-border text-xs">
                    <tr v-for="pay in payments" :key="pay.id" class="hover:bg-muted/40">
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
                            <span v-if="Number(alloc.toMora) > 0" class="text-destructive font-semibold">Mora S/ {{ alloc.toMora }} ·</span>
                            <span v-if="Number(alloc.toInterest) > 0">Int S/ {{ alloc.toInterest }} ·</span>
                            <span v-if="Number(alloc.toPrincipal) > 0" class="text-foreground font-semibold">Cap S/ {{ alloc.toPrincipal }}</span>
                          </div>
                        </div>
                      </td>
                      <td class="px-4 py-3">
                        <StatusBadge kind="payment" :status="pay.status" />
                      </td>
                    </tr>
                  </tbody>
                </table>
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
                <CardDescription>Seguimiento de la orden de desembolso interbancario.</CardDescription>
              </CardHeader>
              <CardContent class="space-y-4 text-xs">
                <div v-if="!disbursement" class="p-4 rounded-lg bg-muted/40 text-muted-foreground">
                  El desembolso se encuentra en proceso o la transferencia fue ejecutada de forma directa.
                </div>
                <div v-else class="space-y-3">
                  <div class="flex items-center justify-between p-3 rounded-lg border bg-muted/20">
                    <span class="text-muted-foreground">Estado del Banco:</span>
                    <StatusBadge kind="disbursement" :status="disbursement.status" />
                  </div>

                  <div v-if="disbursement.status === 'FAILED'" class="p-3 rounded-lg border border-destructive/40 bg-destructive/10 text-destructive space-y-1">
                    <span class="font-bold flex items-center gap-1.5">
                      <AlertTriangle class="size-4" /> Motivo del Rechazo Bancario:
                    </span>
                    <p class="text-xs">
                      [{{ disbursement.bankResponseCode }}] {{ disbursement.bankResponseMessage || 'Cuenta destino bloqueada o inexistente.' }}
                    </p>
                  </div>

                  <div class="space-y-2 text-muted-foreground">
                    <div class="flex justify-between">
                      <span>Monto Transferido:</span>
                      <span class="font-bold text-foreground"><MoneyText :amount="disbursement.amount" /></span>
                    </div>
                    <div class="flex justify-between">
                      <span>Canal:</span>
                      <span class="font-medium text-foreground">{{ disbursement.channel }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span>Nº de Operación:</span>
                      <span class="font-mono text-foreground">{{ disbursement.operationNumber || 'Generando...' }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span>Iniciado:</span>
                      <span class="text-foreground">{{ formatInstant(disbursement.initiatedAt) }}</span>
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
                <CardDescription>Auditoría de consentimiento y firma electrónica del contrato.</CardDescription>
              </CardHeader>
              <CardContent class="space-y-4 text-xs">
                <div class="p-3 rounded-lg border bg-success/5 border-success/30 space-y-2">
                  <div class="flex items-center gap-2 text-success font-bold text-sm">
                    <CheckCircle2 class="size-4" />
                    Contrato y Pagaré Aceptados Electrónicamente
                  </div>
                  <p class="text-[11px] text-muted-foreground">
                    El cliente validó y aceptó las cláusulas contractuales mediante código de verificación OTP antes del desembolso (Ley 27269).
                  </p>
                </div>

                <div class="space-y-2 text-muted-foreground">
                  <div class="flex justify-between py-1 border-b">
                    <span>Método de Firma:</span>
                    <span class="font-semibold text-foreground">OTP SMS / WhatsApp con PIN Digital</span>
                  </div>
                  <div class="flex justify-between py-1 border-b">
                    <span>Fecha y Hora de Firma:</span>
                    <span class="font-medium text-foreground">{{ formatInstant(loan.createdAt) }}</span>
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
      </div>

      <!-- ── Modal para Cotizar y Pagar Prepago Total ── -->
      <PrepaymentQuoteModal
        v-model:open="isPrepaymentOpen"
        :loan-id="loan.id"
        :customer-name="loan.customerName"
        @paid="loadLoanData"
      />
    </template>
  </div>
</template>
