<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  ArrowLeft,
  Clock,
  FileCheck,
  AlertTriangle,
  Calculator,
  ShieldCheck,
  LoaderCircle,
} from '@lucide/vue'
import { toast } from 'vue-sonner'

import type {
  Customer360,
  DecisionRequest,
  LoanApplication,
  Simulation,
} from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { MoneyText, StatusBadge, TableGenerator } from '@prestamos/ui'
import { Alert, AlertDescription } from '@prestamos/ui/components/ui/alert'
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
import { formatInstant } from '@prestamos/utils'

import { useOrigination } from '../composables/useOrigination'
import { simulationInstallmentColumns } from '../utils/originationColumns'

// FE-BO-14 y FE-BO-17: Evaluación de crédito con DTI, recomendación, TCEA, Ficha 360 y dictamen.

const route = useRoute()
const router = useRouter()
const applicationId = computed(() => route.params.id as string)
const {
  getApplication,
  simulateExistingApplication,
  getCustomer360,
  startReview: startReviewApi,
  submitDecision: submitDecisionApi,
} = useOrigination()

const loading = ref(true)
const application = ref<LoanApplication | null>(null)
const customer = ref<Customer360 | null>(null)
const simulation = ref<Simulation | null>(null)

// Formulario de dictamen
const decision = ref<'APROBADO' | 'APROBADO_CON_CONDICIONES' | 'RECHAZADO'>('APROBADO')
const approvedAmount = ref('')
const decisionNotes = ref('')
const isSubmitting = ref(false)
const dictamenError = ref<string | null>(null)

const isPendingReview = computed(() => application.value?.status === 'SOLICITADO')
const isDecided = computed(() =>
  ['APROBADO', 'APROBADO_CON_CONDICIONES', 'RECHAZADO', 'DESEMBOLSADO', 'ANULADO'].includes(
    application.value?.status ?? '',
  ),
)

async function loadData() {
  loading.value = true
  try {
    const appData = await getApplication(applicationId.value)
    application.value = appData
    approvedAmount.value = appData.approvedAmount ?? appData.requestedAmount

    // Cargar simulación y ficha 360 en paralelo
    const [simData, custData] = await Promise.all([
      simulateExistingApplication(applicationId.value),
      getCustomer360(appData.customerId),
    ])
    simulation.value = simData
    customer.value = custData
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar solicitud', { description: apiError.detail })
    void router.replace({ name: 'applications' })
  } finally {
    loading.value = false
  }
}

async function startReview() {
  try {
    application.value = await startReviewApi(applicationId.value)
    toast.info('Evaluación iniciada', {
      description: 'La solicitud ha cambiado a estado EN_EVALUACIÓN.',
    })
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('No se pudo iniciar evaluación', { description: apiError.detail })
  }
}

async function submitDecision() {
  dictamenError.value = null

  if (!decisionNotes.value.trim()) {
    dictamenError.value = 'Las notas y sustento de la decisión son estrictamente obligatorias.'
    return
  }

  if (decision.value === 'APROBADO_CON_CONDICIONES') {
    const appAmt = Number(approvedAmount.value)
    const reqAmt = Number(application.value?.requestedAmount ?? 0)
    if (Number.isNaN(appAmt) || appAmt <= 0) {
      dictamenError.value = 'El monto aprobado debe ser mayor a cero.'
      return
    }
    if (appAmt >= reqAmt) {
      dictamenError.value =
        'En aprobación con condiciones, el monto aprobado debe ser menor al solicitado.'
      return
    }
  }

  isSubmitting.value = true
  try {
    const payload: DecisionRequest = {
      decision: decision.value,
      notes: decisionNotes.value.trim(),
      approvedAmount:
        decision.value === 'APROBADO_CON_CONDICIONES' ? approvedAmount.value : undefined,
    }

    const data = await submitDecisionApi(applicationId.value, payload)
    application.value = data
    toast.success('Dictamen registrado', {
      description: `La solicitud fue dictaminada como ${data.status}.`,
    })
  } catch (error) {
    const apiError = toApiError(error)
    dictamenError.value = apiError.detail
    toast.error(apiError.title, { description: apiError.detail })
  } finally {
    isSubmitting.value = false
  }
}

watch(applicationId, (newId, oldId) => {
  if (newId && oldId && newId !== oldId) {
    void loadData()
  }
})

onMounted(() => {
  void loadData()
})
</script>

<template>
  <div class="space-y-6">
    <!-- ── Botón Volver ── -->
    <div class="flex items-center gap-2">
      <Button variant="ghost" size="sm" as-child class="-ml-2">
        <RouterLink :to="{ name: 'applications' }">
          <ArrowLeft class="size-4 mr-1.5" />
          Volver a Solicitudes
        </RouterLink>
      </Button>
    </div>

    <div v-if="loading" class="space-y-4">
      <Skeleton class="h-28 w-full" />
      <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Skeleton class="h-64 md:col-span-2" />
        <Skeleton class="h-64" />
      </div>
    </div>

    <template v-else-if="application">
      <!-- ── Encabezado de la Solicitud ── -->
      <Card>
        <CardContent class="p-6">
          <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div class="space-y-1">
              <div class="flex flex-wrap items-center gap-2">
                <h1 class="text-2xl font-bold tracking-tight text-foreground">
                  Solicitud #{{ application.id }}
                </h1>
                <Badge variant="outline" class="font-mono text-xs">
                  {{ application.productName }}
                </Badge>
                <StatusBadge kind="application" :status="application.status" />
              </div>
              <p class="text-xs text-muted-foreground">
                Ingresada el {{ formatInstant(application.createdAt) }} vía canal
                <strong class="text-foreground">{{ application.originatedFrom }}</strong
                >.
              </p>
            </div>

            <!-- Botón para iniciar revisión si está en SOLICITADO -->
            <div v-if="isPendingReview" class="shrink-0">
              <Button @click="startReview">
                <Clock class="size-4 mr-2" />
                Comenzar Evaluación
              </Button>
            </div>
          </div>

          <div class="grid grid-cols-2 sm:grid-cols-4 gap-4 mt-6 pt-4 border-t text-xs">
            <div>
              <span class="text-muted-foreground block">Cliente:</span>
              <RouterLink
                :to="{ name: 'customer-detail', params: { id: application.customerId } }"
                class="font-semibold text-primary hover:underline flex items-center gap-1"
              >
                {{ application.customerName }}
              </RouterLink>
            </div>
            <div>
              <span class="text-muted-foreground block">Monto Solicitado:</span>
              <span class="text-base font-bold text-foreground">
                <MoneyText :amount="application.requestedAmount" />
              </span>
            </div>
            <div>
              <span class="text-muted-foreground block">Plazo y Frecuencia:</span>
              <span class="font-medium text-foreground">
                {{ application.term }} cuotas {{ application.frequency.toLowerCase() }}
              </span>
            </div>
            <div>
              <span class="text-muted-foreground block">Destino del Crédito:</span>
              <span class="font-medium text-foreground">{{ application.destination }}</span>
            </div>
          </div>
        </CardContent>
      </Card>

      <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
        <!-- ── Columna Izquierda: Motor de Riesgo + Simulador (FE-BO-14) ── -->
        <div class="md:col-span-2 space-y-6">
          <!-- 1. Motor de Evaluación DTI -->
          <Card>
            <CardHeader class="pb-3">
              <CardTitle class="text-base flex items-center gap-2">
                <ShieldCheck class="size-4 text-primary" />
                Evaluación del Motor de Riesgo
              </CardTitle>
              <CardDescription>
                Cálculo de capacidad de pago DTI (Debt-to-Income) contra política crediticia.
              </CardDescription>
            </CardHeader>
            <CardContent class="space-y-4">
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <!-- Ratio DTI -->
                <div class="rounded-lg border p-4 bg-muted/20 space-y-1">
                  <span class="text-xs text-muted-foreground block">Ratio DTI Proyectado:</span>
                  <div class="flex items-baseline gap-2">
                    <span
                      class="text-3xl font-extrabold"
                      :class="
                        Number(application.dtiRatio ?? 0) > 0.5
                          ? 'text-destructive'
                          : Number(application.dtiRatio ?? 0) > 0.35
                            ? 'text-warning'
                            : 'text-success'
                      "
                    >
                      {{ (Number(application.dtiRatio ?? 0) * 100).toFixed(1) }}%
                    </span>
                    <span class="text-xs text-muted-foreground">de la renta neta</span>
                  </div>
                  <p class="text-[11px] text-muted-foreground pt-1">
                    Tope automático: 35% · Tope manual analista: 50%
                  </p>
                </div>

                <!-- Dictamen Motor -->
                <div class="rounded-lg border p-4 bg-muted/20 space-y-1">
                  <span class="text-xs text-muted-foreground block"
                    >Recomendación del Algoritmo:</span
                  >
                  <div class="pt-1">
                    <Badge
                      variant="outline"
                      class="text-xs px-2.5 py-1 font-bold"
                      :class="
                        application.systemRecommendation === 'APROBABLE'
                          ? 'border-success text-success bg-success/10'
                          : application.systemRecommendation === 'REVISION_MANUAL'
                            ? 'border-warning text-warning bg-warning/10'
                            : 'border-destructive text-destructive bg-destructive/10'
                      "
                    >
                      {{ application.systemRecommendation }}
                    </Badge>
                  </div>
                  <p class="text-[11px] text-muted-foreground pt-2">
                    Basado en score crediticio, ingresos declarados y endeudamiento total.
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>

          <!-- 2. Hoja Resumen Financiera con TCEA (FE-BO-14) -->
          <Card v-if="simulation">
            <CardHeader class="pb-3">
              <CardTitle class="text-base flex items-center gap-2">
                <Calculator class="size-4 text-primary" />
                Hoja Resumen y TCEA Transparente (FE-BO-14)
              </CardTitle>
              <CardDescription>
                Tasa de Costo Efectivo Anual (TCEA) calculada bajo estándares SBS.
              </CardDescription>
            </CardHeader>
            <CardContent class="space-y-4">
              <div
                class="rounded-xl border border-primary/40 bg-primary/5 p-4 grid grid-cols-2 sm:grid-cols-4 gap-4 text-center"
              >
                <div>
                  <span class="text-xs text-muted-foreground block">Cuota Mensual:</span>
                  <span class="text-lg font-bold text-foreground">
                    <MoneyText :amount="simulation.summary.installmentAmount" />
                  </span>
                </div>
                <div>
                  <span class="text-xs text-muted-foreground block">TEA Anual:</span>
                  <span class="text-lg font-bold">
                    {{ (Number(simulation.summary.tea) * 100).toFixed(2) }}%
                  </span>
                </div>
                <!-- TCEA DESTACADA -->
                <div class="bg-primary/10 rounded-lg p-1.5 border border-primary/30">
                  <span class="text-xs text-primary font-bold block">TCEA Efectiva:</span>
                  <span class="text-xl font-extrabold text-primary">
                    {{ (Number(simulation.summary.tcea) * 100).toFixed(2) }}%
                  </span>
                </div>
                <div>
                  <span class="text-xs text-muted-foreground block">Costo Total:</span>
                  <span class="text-lg font-bold text-foreground">
                    <MoneyText :amount="simulation.summary.totalCost" />
                  </span>
                </div>
              </div>

              <!-- Cronograma Proyectado de Cuotas -->
              <div class="border rounded-lg max-h-56 overflow-y-auto text-xs">
                <TableGenerator
                  :columns="simulationInstallmentColumns"
                  :rows="simulation.installments"
                  row-key="number"
                  table-class="w-full text-xs text-left"
                  head-class="bg-muted text-[11px] text-muted-foreground border-b sticky top-0"
                  header-cell-class="p-2"
                  body-class="divide-y"
                  cell-class="p-2"
                />
              </div>
            </CardContent>
          </Card>
        </div>

        <!-- ── Columna Derecha: Ficha 360 Resumida + Formulario de Dictamen (FE-BO-17) ── -->
        <div class="space-y-6">
          <!-- 1. Ficha 360 Resumida del Prestatario -->
          <Card v-if="customer">
            <CardHeader class="pb-3">
              <CardTitle class="text-sm flex items-center justify-between">
                <span>Ficha 360 Resumida</span>
                <RouterLink
                  :to="{ name: 'customer-detail', params: { id: customer.id } }"
                  class="text-xs text-primary hover:underline font-normal"
                >
                  Ver Ficha Completa →
                </RouterLink>
              </CardTitle>
            </CardHeader>
            <CardContent class="space-y-3 text-xs">
              <div class="flex justify-between items-center py-1 border-b">
                <span class="text-muted-foreground">Estado KYC:</span>
                <StatusBadge kind="kyc" :status="customer.kycStatus" />
              </div>
              <div class="flex justify-between items-center py-1 border-b">
                <span class="text-muted-foreground">Ingreso Neto:</span>
                <span class="font-bold"><MoneyText :amount="customer.monthlyNetIncome" /></span>
              </div>
              <div class="flex justify-between items-center py-1 border-b">
                <span class="text-muted-foreground">Deuda Consolidada:</span>
                <span class="font-bold"><MoneyText :amount="customer.totalDebt" /></span>
              </div>
              <div class="flex justify-between items-center py-1 border-b">
                <span class="text-muted-foreground">Atraso Máximo:</span>
                <span
                  class="font-bold"
                  :class="customer.maxDaysLate > 0 ? 'text-destructive' : 'text-success'"
                >
                  {{ customer.maxDaysLate }} días
                </span>
              </div>
              <div class="flex justify-between items-center py-1">
                <span class="text-muted-foreground">Préstamos Previos:</span>
                <span class="font-bold">{{ customer.loans.length }}</span>
              </div>
            </CardContent>
          </Card>

          <!-- 2. Formulario de Dictamen de Crédito (FE-BO-17) -->
          <Card>
            <CardHeader class="pb-3">
              <CardTitle class="text-base flex items-center gap-2">
                <FileCheck class="size-4 text-primary" />
                Dictamen del Analista
              </CardTitle>
              <CardDescription>
                Resolución formal con notas obligatorias y monto aprobado (FE-BO-17).
              </CardDescription>
            </CardHeader>

            <!-- Si ya está dictaminado, mostrar resolución -->
            <CardContent v-if="isDecided" class="space-y-3 text-xs">
              <div class="p-3 rounded-lg border bg-muted/40 space-y-2">
                <div class="flex items-center justify-between">
                  <span class="text-muted-foreground">Resolución Final:</span>
                  <StatusBadge kind="application" :status="application.status" />
                </div>
                <div v-if="application.approvedAmount" class="flex items-center justify-between">
                  <span class="text-muted-foreground">Monto Aprobado:</span>
                  <span class="text-base font-bold text-foreground">
                    <MoneyText :amount="application.approvedAmount" />
                  </span>
                </div>
                <div class="pt-2 border-t">
                  <span class="text-muted-foreground block text-[11px] mb-1"
                    >Notas del Dictamen:</span
                  >
                  <p class="font-medium text-foreground bg-background p-2 rounded border">
                    {{ application.decisionNotes || 'Sin notas registradas.' }}
                  </p>
                </div>
                <div class="text-[11px] text-muted-foreground pt-1">
                  Dictaminado por {{ application.decidedBy }} el
                  {{ application.decidedAt ? formatInstant(application.decidedAt) : '' }}
                </div>
              </div>
            </CardContent>

            <!-- Si está pendiente o en evaluación: Formulario para dictaminar -->
            <form v-else @submit.prevent="submitDecision">
              <CardContent class="space-y-4">
                <Alert v-if="dictamenError" variant="destructive">
                  <AlertTriangle class="size-4" />
                  <AlertDescription>{{ dictamenError }}</AlertDescription>
                </Alert>

                <!-- Selección de Decisión -->
                <div class="space-y-2">
                  <label class="text-xs font-medium">Resolución</label>
                  <div class="space-y-1.5">
                    <label
                      class="flex items-center gap-2 p-2 rounded-lg border cursor-pointer transition-colors text-xs font-medium"
                      :class="
                        decision === 'APROBADO'
                          ? 'border-success bg-success/10 text-success'
                          : 'hover:bg-muted'
                      "
                    >
                      <input
                        v-model="decision"
                        type="radio"
                        value="APROBADO"
                        class="size-4 text-success"
                      />
                      Aprobar Solicitud (100% del monto)
                    </label>

                    <label
                      class="flex items-center gap-2 p-2 rounded-lg border cursor-pointer transition-colors text-xs font-medium"
                      :class="
                        decision === 'APROBADO_CON_CONDICIONES'
                          ? 'border-warning bg-warning/10 text-warning'
                          : 'hover:bg-muted'
                      "
                    >
                      <input
                        v-model="decision"
                        type="radio"
                        value="APROBADO_CON_CONDICIONES"
                        class="size-4 text-warning"
                      />
                      Aprobar con Condiciones (Monto Reducido)
                    </label>

                    <label
                      class="flex items-center gap-2 p-2 rounded-lg border cursor-pointer transition-colors text-xs font-medium"
                      :class="
                        decision === 'RECHAZADO'
                          ? 'border-destructive bg-destructive/10 text-destructive'
                          : 'hover:bg-muted'
                      "
                    >
                      <input
                        v-model="decision"
                        type="radio"
                        value="RECHAZADO"
                        class="size-4 text-destructive"
                      />
                      Rechazar Solicitud
                    </label>
                  </div>
                </div>

                <!-- Monto Aprobado si es con condiciones -->
                <div v-if="decision === 'APROBADO_CON_CONDICIONES'" class="space-y-1.5 pt-2">
                  <label for="approvedAmount" class="text-xs font-medium"
                    >Monto Aprobado (S/)</label
                  >
                  <Input id="approvedAmount" v-model="approvedAmount" placeholder="Ej. 2500.00" />
                  <p class="text-[11px] text-muted-foreground">
                    Debe ser estrictamente menor al solicitado (<MoneyText
                      :amount="application.requestedAmount"
                    />).
                  </p>
                </div>

                <!-- Notas Obligatorias (CA-12.2) -->
                <div class="space-y-1.5 pt-2">
                  <label for="decisionNotes" class="text-xs font-medium">
                    Sustento del Dictamen (Obligatorio)
                  </label>
                  <textarea
                    id="decisionNotes"
                    v-model="decisionNotes"
                    rows="3"
                    placeholder="Indica las razones técnicas, análisis DTI y justificación de la decisión..."
                    class="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
                    :class="{ 'border-destructive': !decisionNotes.trim() && dictamenError }"
                  ></textarea>
                </div>

                <Button
                  type="submit"
                  class="w-full"
                  :variant="decision === 'RECHAZADO' ? 'destructive' : 'default'"
                  :disabled="isSubmitting || !decisionNotes.trim()"
                >
                  <LoaderCircle v-if="isSubmitting" class="size-4 mr-2 animate-spin" />
                  Emitir Dictamen Formal
                </Button>
              </CardContent>
            </form>
          </Card>
        </div>
      </div>
    </template>
  </div>
</template>
