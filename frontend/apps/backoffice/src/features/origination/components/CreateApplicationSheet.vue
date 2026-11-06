<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import {
  FileText,
  FilePlus,
  Calculator,
  LoaderCircle,
  AlertTriangle,
  Percent,
  CheckCircle2,
  Calendar,
  Layers,
} from '@lucide/vue'
import { toast } from 'vue-sonner'

import type {
  Customer,
  LoanProduct,
  PaymentFrequency,
  Simulation,
} from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { MoneyText } from '@prestamos/ui'
import { Button } from '@prestamos/ui/components/ui/button'
import { Input } from '@prestamos/ui/components/ui/input'
import { Badge } from '@prestamos/ui/components/ui/badge'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@prestamos/ui/components/ui/sheet'
import { Alert, AlertDescription, AlertTitle } from '@prestamos/ui/components/ui/alert'
import { formatBusinessDate } from '@prestamos/utils'

import { useApi } from '@/app/api'

// FE-BO-13, FE-BO-14, FE-BO-15: Registro de solicitud con simulador interactivo y cálculo de TCEA.

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'created'): void
}>()

const products = ref<LoanProduct[]>([])
const customers = ref<Customer[]>([])
const selectedCustomer = ref<Customer | null>(null)

const form = ref({
  customerId: '',
  productId: '',
  amount: '3000.00',
  term: 12,
  frequency: 'MENSUAL' as PaymentFrequency,
  destination: 'CONSUMO',
})

const simulation = ref<Simulation | null>(null)
const isSimulating = ref(false)
const isSubmitting = ref(false)
const fieldErrors = ref<Record<string, string>>({})
const generalError = ref<string | null>(null)
const showScheduleModal = ref(false)

async function loadInitialData() {
  try {
    const [prodRes, custRes] = await Promise.all([
      useApi().get<LoanProduct[]>('/loan-products?status=ACTIVO'),
      useApi().get<{ items: Customer[] }>('/customers?pageSize=50'),
    ])
    products.value = prodRes.data
    customers.value = custRes.data.items

    if (products.value.length > 0 && !form.value.productId) {
      const first = products.value[0]
      if (first) {
        form.value.productId = first.id
        form.value.frequency = first.allowedFrequencies[0] ?? 'MENSUAL'
      }
    }
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al inicializar formulario', { description: apiError.detail })
  }
}

watch(
  () => form.value.customerId,
  (id) => {
    selectedCustomer.value = customers.value.find((c) => c.id === id) || null
  },
)

async function runSimulation() {
  if (!form.value.productId || !form.value.amount || !form.value.term) return
  isSimulating.value = true
  fieldErrors.value = {}
  generalError.value = null

  try {
    const { data } = await useApi().post<Simulation>('/loan-applications/simulate', {
      productId: form.value.productId,
      amount: form.value.amount,
      term: form.value.term,
      frequency: form.value.frequency,
    })
    simulation.value = data
  } catch (error) {
    const apiError = toApiError(error)
    simulation.value = null
    if (apiError.fieldErrors) {
      for (const [k, v] of Object.entries(apiError.fieldErrors)) {
        if (v && v[0]) fieldErrors.value[k.toLowerCase()] = v[0]
      }
    }
  } finally {
    isSimulating.value = false
  }
}

watch(
  [() => form.value.productId, () => form.value.amount, () => form.value.term, () => form.value.frequency],
  () => {
    void runSimulation()
  },
  { deep: true },
)

async function onSubmit() {
  fieldErrors.value = {}
  generalError.value = null

  if (!form.value.customerId) {
    fieldErrors.value.customerid = 'Selecciona el cliente solicitante.'
    return
  }

  isSubmitting.value = true
  try {
    await useApi().post('/loan-applications', form.value)
    toast.success('Solicitud registrada con éxito', {
      description: 'La solicitud ha ingresado a la bandeja de evaluación de crédito.',
    })
    emit('created')
    emit('update:open', false)
  } catch (error) {
    const apiError = toApiError(error)
    generalError.value = apiError.detail
    if (apiError.fieldErrors) {
      for (const [k, v] of Object.entries(apiError.fieldErrors)) {
        if (v && v[0]) fieldErrors.value[k.toLowerCase()] = v[0]
      }
    }
    toast.error(apiError.title, { description: apiError.detail })
  } finally {
    isSubmitting.value = false
  }
}

onMounted(() => {
  void loadInitialData()
})
</script>

<template>
  <Sheet :open="open" @update:open="(val) => emit('update:open', val)">
    <SheetContent side="right" class="w-full sm:max-w-2xl overflow-y-auto">
      <SheetHeader class="border-b pb-4">
        <div class="flex items-center gap-2 text-primary">
          <FilePlus class="size-5" />
          <SheetTitle>Nueva Solicitud de Crédito</SheetTitle>
        </div>
        <SheetDescription>
          Registro de solicitud con simulación en tiempo real y cálculo de TCEA (FE-BO-13 a FE-BO-15).
        </SheetDescription>
      </SheetHeader>

      <form class="space-y-6 pt-4" @submit.prevent="onSubmit">
        <Alert v-if="generalError" variant="destructive">
          <AlertTriangle class="size-4" />
          <AlertTitle>Error de Admisión</AlertTitle>
          <AlertDescription>{{ generalError }}</AlertDescription>
        </Alert>

        <!-- 1. Selección de Cliente -->
        <div class="space-y-3">
          <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            1. Prestatario
          </h4>

          <div class="space-y-1.5">
            <label for="customerId" class="text-xs font-medium">Cliente Registrado</label>
            <select
              id="customerId"
              v-model="form.customerId"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              :class="{ 'border-destructive': fieldErrors.customerid }"
            >
              <option value="">Selecciona un cliente...</option>
              <option v-for="c in customers" :key="c.id" :value="c.id">
                {{ c.fullName }} ({{ c.docType }}: {{ c.docNumber }}) - KYC: {{ c.kycStatus }}
              </option>
            </select>
            <p v-if="fieldErrors.customerid" class="text-xs text-destructive">{{ fieldErrors.customerid }}</p>
          </div>

          <!-- Resumen rápido del cliente seleccionado -->
          <div v-if="selectedCustomer" class="rounded-lg border bg-muted/30 p-3 text-xs space-y-1">
            <div class="flex justify-between">
              <span class="text-muted-foreground">Ingreso Mensual:</span>
              <span class="font-semibold text-foreground">
                <MoneyText :amount="selectedCustomer.monthlyNetIncome" />
              </span>
            </div>
            <div class="flex justify-between">
              <span class="text-muted-foreground">Estado KYC:</span>
              <span
                class="font-semibold"
                :class="selectedCustomer.kycStatus === 'VERIFIED' ? 'text-success' : 'text-warning'"
              >
                {{ selectedCustomer.kycStatus }}
              </span>
            </div>
          </div>
        </div>

        <!-- 2. Parámetros del Crédito -->
        <div class="space-y-4 pt-4 border-t">
          <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            2. Condiciones Solicitadas
          </h4>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="productId" class="text-xs font-medium">Producto de Crédito</label>
              <select
                id="productId"
                v-model="form.productId"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option v-for="p in products" :key="p.id" :value="p.id">
                  {{ p.name }} (TEA {{ (Number(p.annualRate) * 100).toFixed(1) }}%)
                </option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label for="destination" class="text-xs font-medium">Destino del Préstamo</label>
              <select
                id="destination"
                v-model="form.destination"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="CONSUMO">Consumo Personal</option>
                <option value="CAPITAL_TRABAJO">Capital de Trabajo</option>
                <option value="COMPRA_DEUDA">Compra de Deuda / Refinanciamiento</option>
                <option value="MEJORA_VIVIENDA">Mejora de Vivienda</option>
              </select>
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div class="space-y-1.5">
              <label for="amount" class="text-xs font-medium">Monto Solicitado (S/)</label>
              <Input
                id="amount"
                v-model="form.amount"
                type="text"
                placeholder="3000.00"
                :class="{ 'border-destructive': fieldErrors.amount }"
              />
              <p v-if="fieldErrors.amount" class="text-xs text-destructive">{{ fieldErrors.amount }}</p>
            </div>

            <div class="space-y-1.5">
              <label for="term" class="text-xs font-medium">Plazo (cuotas)</label>
              <Input
                id="term"
                v-model.number="form.term"
                type="number"
                min="1"
                :class="{ 'border-destructive': fieldErrors.term }"
              />
              <p v-if="fieldErrors.term" class="text-xs text-destructive">{{ fieldErrors.term }}</p>
            </div>

            <div class="space-y-1.5">
              <label for="frequency" class="text-xs font-medium">Frecuencia</label>
              <select
                id="frequency"
                v-model="form.frequency"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="MENSUAL">Mensual</option>
                <option value="QUINCENAL">Quincenal</option>
                <option value="SEMANAL">Semanal</option>
                <option value="DIARIA">Diaria</option>
              </select>
            </div>
          </div>
        </div>

        <!-- 3. Resultados del Simulador en Tiempo Real (FE-BO-13 y FE-BO-14) -->
        <div class="space-y-3 pt-4 border-t">
          <div class="flex items-center justify-between">
            <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
              <Calculator class="size-4 text-primary" />
              Simulación de Cuota y Tasas
            </h4>
            <span v-if="isSimulating" class="text-xs text-muted-foreground flex items-center gap-1">
              <LoaderCircle class="size-3 animate-spin" /> Recalculando...
            </span>
          </div>

          <div v-if="simulation" class="rounded-xl border border-primary/30 bg-primary/5 p-4 space-y-4">
            <!-- Cuota y TCEA Destacada -->
            <div class="grid grid-cols-2 gap-4 border-b border-primary/20 pb-3">
              <div>
                <span class="text-xs text-muted-foreground block">Cuota Proyectada:</span>
                <span class="text-xl font-bold text-foreground">
                  <MoneyText :amount="simulation.summary.installmentAmount" />
                </span>
                <span class="text-[11px] text-muted-foreground block">
                  {{ form.term }} pagos con frecuencia {{ form.frequency.toLowerCase() }}
                </span>
              </div>

              <!-- TCEA DESTACADA (FE-BO-14) -->
              <div>
                <span class="text-xs text-muted-foreground block font-semibold text-primary">
                  TCEA (Costo Efectivo Anual):
                </span>
                <span class="text-xl font-bold text-primary">
                  {{ (Number(simulation.summary.tcea) * 100).toFixed(2) }}%
                </span>
                <span class="text-[11px] text-muted-foreground block">
                  TEA: {{ (Number(simulation.summary.tea) * 100).toFixed(2) }}% · TEM: {{ (Number(simulation.summary.tem) * 100).toFixed(2) }}%
                </span>
              </div>
            </div>

            <!-- Desglose de Costo Total -->
            <div class="grid grid-cols-3 gap-2 text-xs">
              <div>
                <span class="text-muted-foreground block">Total Intereses:</span>
                <span class="font-medium text-foreground">
                  <MoneyText :amount="simulation.summary.totalInterest" />
                </span>
              </div>
              <div>
                <span class="text-muted-foreground block">Comisiones/Cargos:</span>
                <span class="font-medium text-foreground">
                  <MoneyText :amount="simulation.summary.totalFees" />
                </span>
              </div>
              <div>
                <span class="text-muted-foreground block font-semibold">Costo Total:</span>
                <span class="font-bold text-foreground">
                  <MoneyText :amount="simulation.summary.totalCost" />
                </span>
              </div>
            </div>

            <!-- Botón para ver el cronograma proyectado -->
            <div class="pt-2 flex justify-end">
              <Button
                type="button"
                variant="outline"
                size="sm"
                class="h-7 text-xs"
                @click="showScheduleModal = !showScheduleModal"
              >
                {{ showScheduleModal ? 'Ocultar Cronograma' : 'Ver Cronograma Proyectado' }}
              </Button>
            </div>

            <!-- Tabla de Cronograma Proyectado -->
            <div v-if="showScheduleModal" class="mt-3 max-h-56 overflow-y-auto rounded-lg border bg-background text-xs">
              <table class="w-full text-left">
                <thead class="bg-muted/80 text-[11px] text-muted-foreground border-b sticky top-0">
                  <tr>
                    <th class="p-2">#</th>
                    <th class="p-2">Vencimiento</th>
                    <th class="p-2">Capital</th>
                    <th class="p-2">Interés</th>
                    <th class="p-2">Cuota</th>
                    <th class="p-2">Saldo</th>
                  </tr>
                </thead>
                <tbody class="divide-y">
                  <tr v-for="inst in simulation.installments" :key="inst.number">
                    <td class="p-2 font-mono">{{ inst.number }}</td>
                    <td class="p-2">{{ formatBusinessDate(inst.dueDate) }}</td>
                    <td class="p-2"><MoneyText :amount="inst.principal" /></td>
                    <td class="p-2"><MoneyText :amount="inst.interest" /></td>
                    <td class="p-2 font-semibold"><MoneyText :amount="inst.totalDue" /></td>
                    <td class="p-2 text-muted-foreground"><MoneyText :amount="inst.balance" /></td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <div class="flex items-center justify-end gap-3 pt-4 border-t">
          <Button
            type="button"
            variant="ghost"
            :disabled="isSubmitting"
            @click="emit('update:open', false)"
          >
            Cancelar
          </Button>
          <Button type="submit" :disabled="isSubmitting || !simulation">
            <LoaderCircle v-if="isSubmitting" class="size-4 mr-2 animate-spin" />
            Registrar Solicitud
          </Button>
        </div>
      </form>
    </SheetContent>
  </Sheet>
</template>
