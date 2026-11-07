<script setup lang="ts">
import { ref, watch } from 'vue'
import { CheckCircle2, DollarSign, Clock, AlertTriangle, LoaderCircle } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { PrepaymentQuote } from '@prestamos/api-client'
import { newIdempotencyKey, toApiError } from '@prestamos/api-client'
import { MoneyText, useBusyAction } from '@prestamos/ui'
import { Alert, AlertDescription } from '@prestamos/ui/components/ui/alert'
import { Button } from '@prestamos/ui/components/ui/button'
import { Input } from '@prestamos/ui/components/ui/input'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { formatBusinessDate } from '@prestamos/utils'

import { useLoans } from '../composables/useLoans'

// FE-BO-25: Cotización de prepago total con vigencia visible y registro de pago cancelatorio.

const props = defineProps<{
  open: boolean
  loanId: string
  customerName: string
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'paid'): void
}>()

const { quotePrepayment, payPrepayment } = useLoans()

const loadingQuote = ref(true)
const quote = ref<PrepaymentQuote | null>(null)
const externalRef = ref(`CANCEL-${Date.now().toString().slice(-6)}`)
const selectedPaymentMethod = ref('TRANSFERENCIA_BANCARIA')
const errorMsg = ref<string | null>(null)

async function loadQuote() {
  loadingQuote.value = true
  try {
    quote.value = await quotePrepayment(props.loanId)
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cotizar prepago', { description: apiError.detail })
    emit('update:open', false)
  } finally {
    loadingQuote.value = false
  }
}

const { run: submitPayment, isBusy } = useBusyAction(async () => {
  if (!quote.value) return
  errorMsg.value = null

  try {
    const idempotencyKey = newIdempotencyKey()
    await payPrepayment(
      props.loanId,
      {
        amount: quote.value.totalToPay,
        method: selectedPaymentMethod.value,
        externalReference: externalRef.value,
        companyBankAccountId: 'bca-001',
      },
      idempotencyKey,
    )

    toast.success('Pago cancelatorio registrado', {
      description: `El préstamo fue cancelado en su totalidad por S/ ${quote.value.totalToPay}.`,
    })
    emit('paid')
    emit('update:open', false)
  } catch (error) {
    const apiError = toApiError(error)
    errorMsg.value = apiError.detail
    toast.error(apiError.title, { description: apiError.detail })
  }
})

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      errorMsg.value = null
      externalRef.value = `CANCEL-${Date.now().toString().slice(-6)}`
      void loadQuote()
    }
  },
  { immediate: true },
)
</script>

<template>
  <div
    v-if="open"
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
  >
    <Card class="w-full max-w-md shadow-2xl border-border animate-in fade-in-0 zoom-in-95 duration-150">
      <CardHeader class="pb-3 border-b">
        <div class="flex items-center gap-2 text-primary">
          <DollarSign class="size-5" />
          <CardTitle class="text-lg">Cotización de Prepago Total</CardTitle>
        </div>
        <CardDescription>
          Liquidación anticipada de deuda para <strong>{{ customerName }}</strong> (FE-BO-25).
        </CardDescription>
      </CardHeader>

      <div v-if="loadingQuote" class="p-8 text-center text-xs text-muted-foreground">
        <LoaderCircle class="size-6 animate-spin mx-auto mb-2 text-primary" />
        Calculando liquidación financiera al día de hoy...
      </div>

      <form v-else-if="quote" @submit.prevent="submitPayment">
        <CardContent class="space-y-4 pt-4">
          <Alert v-if="errorMsg" variant="destructive">
            <AlertTriangle class="size-4" />
            <AlertDescription>{{ errorMsg }}</AlertDescription>
          </Alert>

          <!-- Vigencia de la cotización -->
          <div class="flex items-center justify-between p-2.5 rounded-lg border bg-muted/40 text-xs">
            <span class="text-muted-foreground flex items-center gap-1.5">
              <Clock class="size-3.5 text-primary" />
              Cotizado al:
            </span>
            <span class="font-medium text-foreground">
              {{ formatBusinessDate(quote.asOfDate) }}
            </span>
            <span class="text-muted-foreground">Válido hasta:</span>
            <span class="font-bold text-primary">
              {{ formatBusinessDate(quote.validUntil) }}
            </span>
          </div>

          <!-- Desglose de liquidación -->
          <div class="rounded-xl border p-4 space-y-2 text-xs bg-card">
            <div class="flex justify-between">
              <span class="text-muted-foreground">Saldo Capital Pendiente:</span>
              <span class="font-medium"><MoneyText :amount="quote.outstandingPrincipal" /></span>
            </div>
            <div class="flex justify-between">
              <span class="text-muted-foreground">Intereses Devengados a la fecha:</span>
              <span class="font-medium"><MoneyText :amount="quote.accruedInterest" /></span>
            </div>
            <div v-if="Number(quote.accruedMora) > 0" class="flex justify-between text-destructive">
              <span>Mora Acumulada:</span>
              <span class="font-medium"><MoneyText :amount="quote.accruedMora" /></span>
            </div>
            <div class="flex justify-between">
              <span class="text-muted-foreground">Penalidad de Prepago:</span>
              <span class="font-medium"><MoneyText :amount="quote.prepaymentPenalty" /></span>
            </div>
            <div class="pt-2 border-t flex justify-between items-center text-sm">
              <span class="font-bold text-foreground">Total Liquidación Cancelatoria:</span>
              <span class="text-lg font-extrabold text-primary">
                <MoneyText :amount="quote.totalToPay" />
              </span>
            </div>
          </div>

          <!-- Datos de registro del pago -->
          <div class="space-y-3 pt-2">
            <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Registrar Abono Cancelatorio
            </h4>

            <div class="space-y-1.5">
              <label for="prepMethod" class="text-xs font-medium">Medio de Pago</label>
              <select
                id="prepMethod"
                v-model="selectedPaymentMethod"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="TRANSFERENCIA_BANCARIA">Transferencia Bancaria (CCI)</option>
                <option value="YAPE">Yape</option>
                <option value="PLIN">Plin</option>
                <option value="TARJETA_DEBITO">Tarjeta de Débito</option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label for="prepRef" class="text-xs font-medium">Nº Operación / Referencia Bancaria</label>
              <Input
                id="prepRef"
                v-model="externalRef"
                placeholder="Ej. OP-998877"
              />
            </div>
          </div>
        </CardContent>

        <div class="flex items-center justify-end gap-2 p-4 border-t bg-muted/20">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            :disabled="isBusy"
            @click="emit('update:open', false)"
          >
            Cancelar
          </Button>
          <Button
            type="submit"
            size="sm"
            :disabled="isBusy"
          >
            <LoaderCircle v-if="isBusy" class="size-3.5 mr-1.5 animate-spin" />
            <CheckCircle2 v-else class="size-3.5 mr-1.5" />
            Registrar Cancelación
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
