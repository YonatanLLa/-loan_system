<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { Package, PackagePlus, LoaderCircle, AlertTriangle } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { LoanProduct, PaymentFrequency } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { Button } from '@prestamos/ui/components/ui/button'
import { Input } from '@prestamos/ui/components/ui/input'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@prestamos/ui/components/ui/sheet'
import { Alert, AlertDescription, AlertTitle } from '@prestamos/ui/components/ui/alert'

import { useProducts } from '../composables/useProducts'

// FE-BO-11 y FE-BO-12: Alta y edición de productos con rangos, comisiones, mora, DTI y validaciones.

const props = defineProps<{
  open: boolean
  productToEdit?: LoanProduct | null
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'saved', product: LoanProduct): void
}>()

const { saveProduct } = useProducts()
const isEditing = computed(() => !!props.productToEdit)
const isSubmitting = ref(false)
const rangeError = ref<string | null>(null)
const fieldErrors = ref<Record<string, string>>({})

const form = ref<Omit<LoanProduct, 'id'>>({
  name: '',
  description: '',
  currency: 'PEN',
  minAmount: '500.00',
  maxAmount: '20000.00',
  minTerm: 6,
  maxTerm: 36,
  allowedFrequencies: ['MENSUAL'] as PaymentFrequency[],
  amortizationMethod: 'FRANCES',
  annualRate: '0.350000', // 35% TEA
  openingFeeRate: '0.010000', // 1%
  openingFeeMode: 'DEDUCTIVA',
  periodicFee: '2.50',
  graceDays: 3,
  moraRatePerDay: '0.001000', // 0.1% diario
  moraBase: 'SALDO_CAPITAL_VENCIDO',
  prepaymentPenaltyRate: '0.000000',
  minAge: 18,
  maxAge: 70,
  minMonthlyIncome: '1200.00',
  maxDtiAuto: '0.350000', // 35% (FE-BO-12)
  maxDtiManual: '0.500000', // 50% (FE-BO-12)
  paymentDay: 15,
  allowAnatocism: false,
  holidayAdjustment: 'NEXT_BUSINESS_DAY',
  status: 'ACTIVO',
})

const availableFrequencies: { label: string; value: PaymentFrequency }[] = [
  { label: 'Diaria', value: 'DIARIA' },
  { label: 'Semanal', value: 'SEMANAL' },
  { label: 'Quincenal', value: 'QUINCENAL' },
  { label: 'Mensual', value: 'MENSUAL' },
]

watch(
  () => props.productToEdit,
  (p) => {
    rangeError.value = null
    fieldErrors.value = {}
    if (p) {
      form.value = {
        name: p.name,
        description: p.description,
        currency: p.currency,
        minAmount: p.minAmount,
        maxAmount: p.maxAmount,
        minTerm: p.minTerm,
        maxTerm: p.maxTerm,
        allowedFrequencies: [...p.allowedFrequencies],
        amortizationMethod: p.amortizationMethod,
        annualRate: p.annualRate,
        openingFeeRate: p.openingFeeRate,
        openingFeeMode: p.openingFeeMode,
        periodicFee: p.periodicFee,
        graceDays: p.graceDays,
        moraRatePerDay: p.moraRatePerDay,
        moraBase: p.moraBase,
        prepaymentPenaltyRate: p.prepaymentPenaltyRate,
        minAge: p.minAge,
        maxAge: p.maxAge,
        minMonthlyIncome: p.minMonthlyIncome,
        maxDtiAuto: p.maxDtiAuto,
        maxDtiManual: p.maxDtiManual,
        paymentDay: p.paymentDay ?? 15,
        allowAnatocism: p.allowAnatocism ?? false,
        holidayAdjustment: p.holidayAdjustment ?? 'NEXT_BUSINESS_DAY',
        status: p.status,
      }
    } else {
      form.value = {
        name: '',
        description: '',
        currency: 'PEN',
        minAmount: '500.00',
        maxAmount: '20000.00',
        minTerm: 6,
        maxTerm: 36,
        allowedFrequencies: ['MENSUAL'],
        amortizationMethod: 'FRANCES',
        annualRate: '0.350000',
        openingFeeRate: '0.010000',
        openingFeeMode: 'DEDUCTIVA',
        periodicFee: '2.50',
        graceDays: 3,
        moraRatePerDay: '0.001000',
        moraBase: 'SALDO_CAPITAL_VENCIDO',
        prepaymentPenaltyRate: '0.000000',
        minAge: 18,
        maxAge: 70,
        minMonthlyIncome: '1200.00',
        maxDtiAuto: '0.350000',
        maxDtiManual: '0.500000',
        paymentDay: 15,
        allowAnatocism: false,
        holidayAdjustment: 'NEXT_BUSINESS_DAY',
        status: 'ACTIVO',
      }
    }
  },
  { immediate: true },
)

function toggleFrequency(freq: PaymentFrequency) {
  const index = form.value.allowedFrequencies.indexOf(freq)
  if (index >= 0) {
    if (form.value.allowedFrequencies.length > 1) {
      form.value.allowedFrequencies.splice(index, 1)
    } else {
      toast.warning('Debe existir al menos una frecuencia habilitada.')
    }
  } else {
    form.value.allowedFrequencies.push(freq)
  }
}

function validateRanges(): boolean {
  rangeError.value = null
  fieldErrors.value = {}
  let valid = true

  if (!form.value.name.trim()) {
    fieldErrors.value.name = 'El nombre del producto es obligatorio.'
    valid = false
  }

  const minAmt = Number(form.value.minAmount)
  const maxAmt = Number(form.value.maxAmount)
  if (minAmt <= 0 || maxAmt <= 0 || minAmt > maxAmt) {
    fieldErrors.value.amount = 'El monto mínimo debe ser mayor a 0 y no puede superar al monto máximo.'
    valid = false
  }

  if (form.value.minTerm <= 0 || form.value.maxTerm <= 0 || form.value.minTerm > form.value.maxTerm) {
    fieldErrors.value.term = 'El plazo mínimo no puede superar al plazo máximo.'
    valid = false
  }

  const tea = Number(form.value.annualRate)
  if (Number.isNaN(tea) || tea <= 0) {
    fieldErrors.value.annualRate = 'La tasa TEA debe ser mayor a 0.'
    valid = false
  }

  const autoDti = Number(form.value.maxDtiAuto)
  const manualDti = Number(form.value.maxDtiManual)
  if (autoDti > manualDti) {
    fieldErrors.value.dti = 'El DTI automático no puede ser mayor que el DTI de revisión manual.'
    valid = false
  }

  if (!valid) {
    rangeError.value = 'Corrige los errores en la configuración de rangos antes de continuar.'
  }

  return valid
}

async function onSubmit() {
  if (!validateRanges()) return

  isSubmitting.value = true
  rangeError.value = null

  try {
    if (isEditing.value && props.productToEdit) {
      const data = await saveProduct(form.value, props.productToEdit.id)
      toast.success('Producto actualizado', {
        description: `Se guardaron las condiciones del producto ${data.name}.`,
      })
      emit('saved', data)
      emit('update:open', false)
    } else {
      const data = await saveProduct(form.value)
      toast.success('Producto creado', {
        description: `El producto ${data.name} ha sido configurado con éxito.`,
      })
      emit('saved', data)
      emit('update:open', false)
    }
  } catch (error) {
    const apiError = toApiError(error)
    rangeError.value = apiError.detail
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
</script>

<template>
  <Sheet :open="open" @update:open="(val) => emit('update:open', val)">
    <SheetContent side="right" class="w-full sm:max-w-2xl overflow-y-auto">
      <SheetHeader class="border-b pb-4">
        <div class="flex items-center gap-2 text-primary">
          <PackagePlus v-if="!isEditing" class="size-5" />
          <Package v-else class="size-5" />
          <SheetTitle>{{ isEditing ? 'Editar Producto de Crédito' : 'Nuevo Producto de Crédito' }}</SheetTitle>
        </div>
        <SheetDescription>
          Configura tasas, comisiones, amortización, mora y DTI máximo permitido (FE-BO-11 y FE-BO-12).
        </SheetDescription>
      </SheetHeader>

      <form class="space-y-6 pt-4" @submit.prevent="onSubmit">
        <Alert v-if="rangeError" variant="destructive">
          <AlertTriangle class="size-4" />
          <AlertTitle>Validación de Reglas de Negocio</AlertTitle>
          <AlertDescription>{{ rangeError }}</AlertDescription>
        </Alert>

        <!-- 1. Datos Generales -->
        <div class="space-y-4">
          <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            1. Datos Generales y Moneda
          </h4>

          <div class="space-y-1.5">
            <label for="name" class="text-xs font-medium">Nombre Comercial</label>
            <Input
              id="name"
              v-model="form.name"
              placeholder="Ej. Crédito Personal Digital"
              :class="{ 'border-destructive': fieldErrors.name }"
            />
            <p v-if="fieldErrors.name" class="text-xs text-destructive">{{ fieldErrors.name }}</p>
          </div>

          <div class="space-y-1.5">
            <label for="description" class="text-xs font-medium">Descripción</label>
            <Input
              id="description"
              v-model="form.description"
              placeholder="Ej. Préstamo de consumo 100% digital con cuota fija."
            />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="currency" class="text-xs font-medium">Moneda</label>
              <select
                id="currency"
                v-model="form.currency"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="PEN">Soles (PEN - S/)</option>
                <option value="USD">Dólares (USD - $)</option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label for="status" class="text-xs font-medium">Estado</label>
              <select
                id="status"
                v-model="form.status"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="ACTIVO">Activo para Originación</option>
                <option value="INACTIVO">Inactivo</option>
              </select>
            </div>
          </div>
        </div>

        <!-- 2. Montos, Plazos y Método de Amortización -->
        <div class="space-y-4 pt-4 border-t">
          <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            2. Montos, Plazos y Amortización
          </h4>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="minAmount" class="text-xs font-medium">Monto Mínimo</label>
              <Input
                id="minAmount"
                v-model="form.minAmount"
                type="text"
                placeholder="500.00"
              />
            </div>
            <div class="space-y-1.5">
              <label for="maxAmount" class="text-xs font-medium">Monto Máximo</label>
              <Input
                id="maxAmount"
                v-model="form.maxAmount"
                type="text"
                placeholder="20000.00"
              />
            </div>
          </div>
          <p v-if="fieldErrors.amount" class="text-xs text-destructive">{{ fieldErrors.amount }}</p>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="minTerm" class="text-xs font-medium">Plazo Mínimo (cuotas)</label>
              <Input
                id="minTerm"
                v-model.number="form.minTerm"
                type="number"
                min="1"
              />
            </div>
            <div class="space-y-1.5">
              <label for="maxTerm" class="text-xs font-medium">Plazo Máximo (cuotas)</label>
              <Input
                id="maxTerm"
                v-model.number="form.maxTerm"
                type="number"
                min="1"
              />
            </div>
          </div>
          <p v-if="fieldErrors.term" class="text-xs text-destructive">{{ fieldErrors.term }}</p>

          <div class="space-y-1.5">
            <label for="amortizationMethod" class="text-xs font-medium">Método de Amortización</label>
            <select
              id="amortizationMethod"
              v-model="form.amortizationMethod"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="FRANCES">Francés (Cuota constante nivelada)</option>
              <option value="ALEMAN">Alemán (Amortización de capital constante)</option>
              <option value="FLAT">Flat / Interés Simple</option>
              <option value="AMERICANO">Americano (Interés periódico, capital al final)</option>
            </select>
          </div>

          <div class="space-y-1.5">
            <label class="text-xs font-medium block">Frecuencias de Pago Habilitadas</label>
            <div class="flex flex-wrap gap-2 pt-1">
              <button
                v-for="freq in availableFrequencies"
                :key="freq.value"
                type="button"
                class="px-3 py-1.5 rounded-md text-xs font-semibold border transition-all"
                :class="
                  form.allowedFrequencies.includes(freq.value)
                    ? 'bg-primary text-primary-foreground border-primary'
                    : 'bg-muted text-muted-foreground border-border hover:bg-muted/80'
                "
                @click="toggleFrequency(freq.value)"
              >
                {{ freq.label }}
              </button>
            </div>
          </div>
        </div>

        <!-- 3. Tasas, Comisiones y Mora -->
        <div class="space-y-4 pt-4 border-t">
          <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            3. Tasas, Comisiones y Penalidades
          </h4>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="annualRate" class="text-xs font-medium">TEA (Decimal: 0.35 = 35%)</label>
              <Input
                id="annualRate"
                v-model="form.annualRate"
                type="text"
                placeholder="0.350000"
                :class="{ 'border-destructive': fieldErrors.annualRate }"
              />
              <p v-if="fieldErrors.annualRate" class="text-xs text-destructive">{{ fieldErrors.annualRate }}</p>
            </div>

            <div class="space-y-1.5">
              <label for="graceDays" class="text-xs font-medium">Días de Gracia (tolerancia)</label>
              <Input
                id="graceDays"
                v-model.number="form.graceDays"
                type="number"
                min="0"
              />
            </div>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="openingFeeRate" class="text-xs font-medium">Comisión de Apertura (%)</label>
              <Input
                id="openingFeeRate"
                v-model="form.openingFeeRate"
                type="text"
                placeholder="0.010000"
              />
            </div>

            <div class="space-y-1.5">
              <label for="openingFeeMode" class="text-xs font-medium">Modalidad de Comisión</label>
              <select
                id="openingFeeMode"
                v-model="form.openingFeeMode"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="DEDUCTIVA">Deductiva (se descuenta del desembolso)</option>
                <option value="FINANCIADA">Financiada (se suma al principal)</option>
              </select>
            </div>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="periodicFee" class="text-xs font-medium">Cargo Periódico por Cuota (S/)</label>
              <Input
                id="periodicFee"
                v-model="form.periodicFee"
                type="text"
                placeholder="2.50"
              />
            </div>

            <div class="space-y-1.5">
              <label for="moraRatePerDay" class="text-xs font-medium">Tasa de Mora Diaria (%)</label>
              <Input
                id="moraRatePerDay"
                v-model="form.moraRatePerDay"
                type="text"
                placeholder="0.001000"
              />
            </div>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="moraBase" class="text-xs font-medium">Base de Cálculo de Mora</label>
              <select
                id="moraBase"
                v-model="form.moraBase"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="SALDO_CAPITAL_VENCIDO">Saldo de Capital Vencido</option>
                <option value="CUOTA_TOTAL_VENCIDA">Cuota Total Vencida</option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label for="prepaymentPenaltyRate" class="text-xs font-medium">Penalidad Prepago (%)</label>
              <Input
                id="prepaymentPenaltyRate"
                v-model="form.prepaymentPenaltyRate"
                type="text"
                placeholder="0.000000"
              />
            </div>
          </div>

          <div class="grid grid-cols-3 gap-3">
            <div class="space-y-1.5">
              <label for="paymentDay" class="text-xs font-medium">Día Fijo de Pago (1-28)</label>
              <Input
                id="paymentDay"
                v-model.number="form.paymentDay"
                type="number"
                min="1"
                max="28"
              />
            </div>

            <div class="space-y-1.5">
              <label for="holidayAdjustment" class="text-xs font-medium">Ajuste por Feriados</label>
              <select
                id="holidayAdjustment"
                v-model="form.holidayAdjustment"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-2.5 py-1 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="NEXT_BUSINESS_DAY">Día hábil siguiente</option>
                <option value="PREVIOUS_BUSINESS_DAY">Día hábil anterior</option>
                <option value="NONE">Sin ajuste</option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label for="allowAnatocism" class="text-xs font-medium">Anatocismo</label>
              <select
                id="allowAnatocism"
                v-model="form.allowAnatocism"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-2.5 py-1 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option :value="false">No permitido</option>
                <option :value="true">Capitalizar interés</option>
              </select>
            </div>
          </div>
        </div>

        <!-- 4. Política DTI y Admisión (FE-BO-12) -->
        <div class="space-y-4 pt-4 border-t">
          <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            4. Política DTI y Criterios de Admisión (FE-BO-12)
          </h4>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="maxDtiAuto" class="text-xs font-medium">DTI Máximo Auto (0.35 = 35%)</label>
              <Input
                id="maxDtiAuto"
                v-model="form.maxDtiAuto"
                type="text"
                placeholder="0.350000"
              />
              <p class="text-[11px] text-muted-foreground">Aprobación automática directa del motor</p>
            </div>

            <div class="space-y-1.5">
              <label for="maxDtiManual" class="text-xs font-medium">DTI Máximo Manual (0.50 = 50%)</label>
              <Input
                id="maxDtiManual"
                v-model="form.maxDtiManual"
                type="text"
                placeholder="0.500000"
              />
              <p class="text-[11px] text-muted-foreground">Tope con revisión manual de analista</p>
            </div>
          </div>
          <p v-if="fieldErrors.dti" class="text-xs text-destructive">{{ fieldErrors.dti }}</p>

          <div class="grid grid-cols-3 gap-3">
            <div class="space-y-1.5">
              <label for="minAge" class="text-xs font-medium">Edad Mínima</label>
              <Input
                id="minAge"
                v-model.number="form.minAge"
                type="number"
                min="18"
              />
            </div>
            <div class="space-y-1.5">
              <label for="maxAge" class="text-xs font-medium">Edad Máxima</label>
              <Input
                id="maxAge"
                v-model.number="form.maxAge"
                type="number"
                max="90"
              />
            </div>
            <div class="space-y-1.5">
              <label for="minMonthlyIncome" class="text-xs font-medium">Ingreso Mínimo</label>
              <Input
                id="minMonthlyIncome"
                v-model="form.minMonthlyIncome"
                type="text"
              />
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
          <Button type="submit" :disabled="isSubmitting">
            <LoaderCircle v-if="isSubmitting" class="size-4 mr-2 animate-spin" />
            {{ isEditing ? 'Guardar Cambios' : 'Crear Producto' }}
          </Button>
        </div>
      </form>
    </SheetContent>
  </Sheet>
</template>
