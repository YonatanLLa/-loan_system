<script setup lang="ts">
import { onMounted, ref } from 'vue'
import {
  AlertCircle,
  Briefcase,
  Calculator,
  CheckCircle2,
  ChevronDown,
  FileUp,
  LoaderCircle,
  Send,
  Sparkles,
} from '@lucide/vue'

import { MoneyText } from '@prestamos/ui'
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
import { Label } from '@prestamos/ui/components/ui/label'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import { formatBusinessDate } from '@prestamos/utils'

import { useLoanApplication, useSimulator } from '../composables'

// FE-CP-06 y FE-CP-07: Simulador interactivo con TEA/TCEA y formulario de solicitud con sustentos.

const {
  products,
  selectedProductId,
  selectedProduct,
  amount,
  term,
  frequency,
  minAmount,
  maxAmount,
  minTerm,
  maxTerm,
  isCalculating,
  productsLoading,
  error: simulatorError,
  teaDisplay,
  tceaDisplay,
  installmentAmount,
  totalInterest,
  totalFees,
  totalCost,
  scheduleRows,
  loadProducts,
} = useSimulator()

const {
  destination,
  employerName,
  occupation,
  monthlyNetIncome,
  incomeProof,
  isSubmitting,
  error: applicationError,
  simulateUploadIncomeProof,
  submit,
} = useLoanApplication()

const isScheduleExpanded = ref(false)

const DESTINATIONS = [
  { value: 'CONSUMO', label: 'Gastos Personales / Consumo' },
  { value: 'CAPITAL_TRABAJO', label: 'Capital de Trabajo para Negocio' },
  { value: 'MEJORAS_HOGAR', label: 'Mejoras y Remodelación del Hogar' },
  { value: 'SALUD_EDUCACION', label: 'Salud, Emergencia o Educación' },
  { value: 'CONSOLIDACION_DEUDA', label: 'Consolidación de Deudas' },
]

async function handleSubmitApplication() {
  if (!selectedProductId.value) return
  await submit({
    productId: selectedProductId.value,
    amount: amount.value,
    term: term.value,
    frequency: frequency.value,
  })
}

onMounted(() => {
  void loadProducts()
})
</script>

<template>
  <div class="space-y-5 py-2">
    <!-- Header -->
    <div class="text-center space-y-1">
      <h2 class="text-xl font-bold flex items-center justify-center gap-2">
        <Sparkles class="size-5 text-primary" />
        Solicita tu Préstamo Digital
      </h2>
      <p class="text-xs text-muted-foreground">
        Simula tu cuota con tasas transparentes y envía tu solicitud en solo 2 pasos.
      </p>
    </div>

    <div v-if="productsLoading" class="space-y-3 p-4">
      <Skeleton class="h-10 w-full" />
      <Skeleton class="h-32 w-full" />
      <Skeleton class="h-20 w-full" />
    </div>

    <template v-else>
      <!-- ══════════════════════════════════════════════════════════ -->
      <!-- PASO 1: SIMULADOR INTERACTIVO (FE-CP-06)                   -->
      <!-- ══════════════════════════════════════════════════════════ -->
      <Card class="shadow-md border-border overflow-hidden">
        <CardHeader class="pb-3 border-b bg-muted/10">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2 text-primary">
              <Calculator class="size-4" />
              <CardTitle class="text-base font-bold">1. Simula tu Crédito</CardTitle>
            </div>
            <Badge variant="outline" class="text-[10px] font-mono">
              {{ selectedProduct?.name || 'Crédito Digital' }}
            </Badge>
          </div>
        </CardHeader>

        <CardContent class="space-y-4 pt-4 text-xs">
          <Alert v-if="simulatorError" variant="destructive" class="py-2.5">
            <AlertCircle class="size-4" />
            <AlertDescription class="text-xs">{{ simulatorError }}</AlertDescription>
          </Alert>

          <!-- Selector de Producto (cuando hay más de un producto activo) -->
          <div v-if="products.length > 1" class="space-y-1">
            <Label for="productId" class="text-xs font-semibold text-muted-foreground">Producto crediticio</Label>
            <select
              id="productId"
              v-model="selectedProductId"
              class="flex h-10 w-full rounded-md border border-input bg-transparent px-3 py-1 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option v-for="p in products" :key="p.id" :value="p.id">
                {{ p.name }} (TEA {{ (Number(p.annualRate) * 100).toFixed(1) }}%)
              </option>
            </select>
          </div>

          <!-- Monto Slider y Valor -->
          <div class="space-y-2">
            <div class="flex items-center justify-between">
              <Label class="text-xs font-semibold text-muted-foreground">Monto que necesitas</Label>
              <span class="text-lg font-bold font-mono text-primary">
                S/ {{ Number(amount).toLocaleString('es-PE', { minimumFractionDigits: 2 }) }}
              </span>
            </div>
            <input
              v-model.number="amount"
              type="range"
              :min="minAmount"
              :max="maxAmount"
              step="100"
              class="w-full accent-primary h-2 bg-muted rounded-lg cursor-pointer"
            />
            <div class="flex justify-between text-[10px] text-muted-foreground font-mono">
              <span>Mín: S/ {{ minAmount.toLocaleString() }}</span>
              <span>Máx: S/ {{ maxAmount.toLocaleString() }}</span>
            </div>
          </div>

          <!-- Plazo Slider y Valor -->
          <div class="space-y-2">
            <div class="flex items-center justify-between">
              <Label class="text-xs font-semibold text-muted-foreground">Plazo de pago</Label>
              <span class="text-base font-bold font-mono text-foreground">
                {{ term }} meses
              </span>
            </div>
            <input
              v-model.number="term"
              type="range"
              :min="minTerm"
              :max="maxTerm"
              step="1"
              class="w-full accent-primary h-2 bg-muted rounded-lg cursor-pointer"
            />
            <div class="flex justify-between text-[10px] text-muted-foreground font-mono">
              <span>{{ minTerm }} meses</span>
              <span>{{ maxTerm }} meses</span>
            </div>
          </div>

          <!-- Tarjeta de Resumen de Cuota y Tasas (FE-CP-06) -->
          <div class="rounded-xl border border-primary/30 bg-primary/5 p-4 space-y-3">
            <div class="text-center">
              <span class="text-[11px] text-muted-foreground font-medium block">Tu cuota mensual aproximada sería:</span>
              <div class="text-3xl font-extrabold font-mono text-primary tracking-tight mt-0.5">
                <MoneyText :amount="installmentAmount" />
              </div>
            </div>

            <!-- Indicadores SBS: TEA y TCEA -->
            <div class="grid grid-cols-2 gap-2 text-center pt-2 border-t border-primary/10">
              <div class="p-2 rounded-lg bg-background/80 border">
                <span class="text-[10px] text-muted-foreground block">TEA (Tasa Efectiva):</span>
                <span class="font-bold text-xs text-foreground font-mono">{{ teaDisplay || '-' }}</span>
              </div>
              <div class="p-2 rounded-lg bg-background/80 border border-primary/20">
                <span class="text-[10px] text-primary font-semibold block">TCEA (Costo Total):</span>
                <span class="font-extrabold text-xs text-primary font-mono">{{ tceaDisplay || '-' }}</span>
              </div>
            </div>

            <!-- Desglose de Costo Transparente -->
            <div class="space-y-1 text-[11px] pt-1">
              <div class="flex justify-between text-muted-foreground">
                <span>Total de Intereses:</span>
                <span class="font-mono text-foreground font-medium"><MoneyText :amount="totalInterest" /></span>
              </div>
              <div class="flex justify-between text-muted-foreground">
                <span>Comisiones y Gastos:</span>
                <span class="font-mono text-foreground font-medium"><MoneyText :amount="totalFees" /></span>
              </div>
              <div class="flex justify-between text-foreground font-semibold pt-1 border-t">
                <span>Costo Total a Pagar:</span>
                <span class="font-mono text-primary font-bold"><MoneyText :amount="totalCost" /></span>
              </div>
            </div>

            <!-- Cronograma Desplegable -->
            <div class="pt-1">
              <button
                type="button"
                class="w-full text-center text-[11px] text-primary font-semibold flex items-center justify-center gap-1 hover:underline py-1"
                @click="isScheduleExpanded = !isScheduleExpanded"
              >
                <span>{{ isScheduleExpanded ? 'Ocultar cronograma proyectado' : 'Ver cronograma de cuotas' }}</span>
                <ChevronDown class="size-3 transition-transform" :class="{ 'rotate-180': isScheduleExpanded }" />
              </button>

              <div v-if="isScheduleExpanded" class="mt-2 max-h-48 overflow-y-auto rounded-lg border bg-background text-[11px]">
                <table class="w-full text-left">
                  <thead class="border-b bg-muted/40 font-semibold text-muted-foreground sticky top-0">
                    <tr>
                      <th class="px-2.5 py-1.5">#</th>
                      <th class="px-2.5 py-1.5">Vencimiento</th>
                      <th class="px-2.5 py-1.5 text-right">Cuota</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-border">
                    <tr v-for="inst in scheduleRows" :key="inst.number" class="hover:bg-muted/20">
                      <td class="px-2.5 py-1.5 font-mono">{{ inst.number }}</td>
                      <td class="px-2.5 py-1.5">{{ formatBusinessDate(inst.dueDate) }}</td>
                      <td class="px-2.5 py-1.5 text-right font-mono font-medium">
                        <MoneyText :amount="inst.totalDue" />
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      <!-- ══════════════════════════════════════════════════════════ -->
      <!-- PASO 2: DATOS LABORALES Y SUSTENTOS (FE-CP-07)             -->
      <!-- ══════════════════════════════════════════════════════════ -->
      <Card class="shadow-md border-border">
        <CardHeader class="pb-3 border-b bg-muted/10">
          <div class="flex items-center gap-2 text-primary">
            <Briefcase class="size-4" />
            <CardTitle class="text-base font-bold">2. Datos Laborales y de Ingresos</CardTitle>
          </div>
          <CardDescription class="text-xs">
            Esta información se evaluará automáticamente por nuestro motor de riesgo de admisión.
          </CardDescription>
        </CardHeader>

        <CardContent class="space-y-4 pt-4 text-xs">
          <Alert v-if="applicationError" variant="destructive" class="py-2.5">
            <AlertCircle class="size-4" />
            <AlertDescription class="text-xs">{{ applicationError }}</AlertDescription>
          </Alert>

          <form class="space-y-3.5" @submit.prevent="handleSubmitApplication">
            <!-- Destino del Crédito -->
            <div class="space-y-1">
              <Label for="destination" class="text-xs font-medium">¿Para qué utilizarás el préstamo?</Label>
              <select
                id="destination"
                v-model="destination"
                class="flex h-10 w-full rounded-md border border-input bg-transparent px-3 py-1 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option v-for="d in DESTINATIONS" :key="d.value" :value="d.value">{{ d.label }}</option>
              </select>
            </div>

            <!-- Empresa / Empleador -->
            <div class="space-y-1">
              <Label for="employerName" class="text-xs font-medium">Empresa donde laboras o Razón Social</Label>
              <Input
                id="employerName"
                v-model="employerName"
                placeholder="Ej. Retail Perú S.A. o Independiente"
                required
                class="h-10 text-xs"
              />
            </div>

            <!-- Ocupación / Cargo -->
            <div class="space-y-1">
              <Label for="occupation" class="text-xs font-medium">Ocupación o Cargo</Label>
              <Input
                id="occupation"
                v-model="occupation"
                placeholder="Ej. Ejecutivo de Cuentas, Contador, Comerciante"
                required
                class="h-10 text-xs"
              />
            </div>

            <!-- Ingreso Neto Mensual -->
            <div class="space-y-1">
              <Label for="income" class="text-xs font-medium">Ingreso Neto Mensual (S/)</Label>
              <Input
                id="income"
                v-model="monthlyNetIncome"
                type="number"
                step="50"
                min="500"
                placeholder="Ej. 3500.00"
                required
                class="h-10 text-xs font-mono"
              />
              <p class="text-[10px] text-muted-foreground">
                Monto que percibes después de descuentos de ley.
              </p>
            </div>

            <!-- Carga de Sustento de Ingresos (FE-CP-07) -->
            <div class="space-y-1.5 pt-1">
              <Label class="text-xs font-medium">Sustento de Ingresos (Boleta de Pago o Recibo por Honorarios)</Label>
              <div
                class="rounded-xl border border-dashed p-3 text-center bg-muted/10 transition-colors"
                :class="incomeProof ? 'border-success bg-success/10' : 'border-input'"
              >
                <div v-if="incomeProof" class="flex items-center justify-between text-success px-2 py-1">
                  <div class="flex items-center gap-2">
                    <CheckCircle2 class="size-4" />
                    <span class="font-semibold text-[11px]">{{ incomeProof }}</span>
                  </div>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    class="h-7 text-xs text-destructive hover:bg-destructive/10"
                    @click="incomeProof = null"
                  >
                    Quitar
                  </Button>
                </div>
                <div v-else class="space-y-2 py-1">
                  <FileUp class="size-6 mx-auto text-muted-foreground" />
                  <p class="text-[11px] text-muted-foreground">
                    Carga el archivo PDF o foto de tu última boleta de pago.
                  </p>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    class="h-8 text-xs"
                    @click="simulateUploadIncomeProof"
                  >
                    Adjuntar Boleta de Ingresos
                  </Button>
                </div>
              </div>
            </div>

            <!-- Botón de Envío -->
            <div class="pt-2">
              <Button
                type="submit"
                class="w-full h-11 text-xs font-semibold shadow-lg"
                :disabled="isSubmitting || isCalculating"
              >
                <LoaderCircle v-if="isSubmitting" class="mr-2 size-4 animate-spin" />
                <Send v-else class="mr-2 size-4" />
                <span>Enviar Solicitud de Préstamo (S/ {{ amount }})</span>
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </template>
  </div>
</template>
