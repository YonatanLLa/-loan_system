<script setup lang="ts">
import { ref } from 'vue'
import { RotateCcw, AlertTriangle, LoaderCircle } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { Payment } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { useBusyAction, MoneyText } from '@prestamos/ui'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Alert, AlertDescription, AlertTitle } from '@prestamos/ui/components/ui/alert'

import { usePayments } from '../composables/usePayments'

// FE-BO-23: Reverso de pago con motivo obligatorio y prevención de doble reverso (409).

const props = defineProps<{
  open: boolean
  payment: Payment | null
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'reversed'): void
}>()

const { reversePayment } = usePayments()
const reason = ref('')
const errorMsg = ref<string | null>(null)

const { run: submitReversal, isBusy } = useBusyAction(async () => {
  if (!props.payment) return
  errorMsg.value = null

  if (!reason.value.trim()) {
    errorMsg.value = 'El motivo y justificación del reverso son estrictamente obligatorios.'
    return
  }

  try {
    await reversePayment(props.payment.id, reason.value.trim())

    toast.success('Pago revertido', {
      description: `El pago #${props.payment.id} fue revertido y los saldos del préstamo fueron restituidos.`,
    })
    emit('reversed')
    emit('update:open', false)
  } catch (error) {
    const apiError = toApiError(error)
    // FE-BO-23: Manejo explícito ante doble reverso (409)
    if (apiError.status === 409) {
      errorMsg.value = 'Conflicto (409): Este pago ya se encuentra revertido en el sistema.'
    } else {
      errorMsg.value = apiError.detail
    }
    toast.error(apiError.title, { description: errorMsg.value })
  }
})
</script>

<template>
  <div
    v-if="open && payment"
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
  >
    <Card class="w-full max-w-md shadow-2xl border-border animate-in fade-in-0 zoom-in-95 duration-150">
      <CardHeader class="pb-3 border-b">
        <div class="flex items-center gap-2 text-destructive">
          <RotateCcw class="size-5" />
          <CardTitle class="text-lg">Reverso de Pago</CardTitle>
        </div>
        <CardDescription>
          Anulación de pago con restitución de saldos e intereses (FE-BO-23).
        </CardDescription>
      </CardHeader>

      <form @submit.prevent="submitReversal">
        <CardContent class="space-y-4 pt-4 text-xs">
          <Alert v-if="errorMsg" variant="destructive">
            <AlertTriangle class="size-4" />
            <AlertDescription>{{ errorMsg }}</AlertDescription>
          </Alert>

          <!-- Resumen del pago a revertir -->
          <div class="p-3 rounded-lg border bg-muted/40 space-y-1.5">
            <div class="flex justify-between">
              <span class="text-muted-foreground">ID de Pago:</span>
              <span class="font-mono font-bold text-foreground">#{{ payment.id }}</span>
            </div>
            <div class="flex justify-between">
              <span class="text-muted-foreground">Prestatario:</span>
              <span class="font-medium text-foreground">{{ payment.customerName }}</span>
            </div>
            <div class="flex justify-between">
              <span class="text-muted-foreground">Importe Aplicado:</span>
              <span class="text-sm font-bold text-destructive">
                <MoneyText :amount="payment.amount" />
              </span>
            </div>
            <div class="flex justify-between">
              <span class="text-muted-foreground">Referencia:</span>
              <span class="font-mono text-foreground">{{ payment.externalReference || '—' }}</span>
            </div>
          </div>

          <!-- Motivo Obligatorio (CA-18.2) -->
          <div class="space-y-1.5">
            <label for="revReason" class="text-xs font-semibold text-foreground">
              Motivo y Justificación del Reverso (Obligatorio)
            </label>
            <textarea
              id="revReason"
              v-model="reason"
              rows="3"
              placeholder="Ej. Operación duplicada en extracto / Cheque devuelto / Error operativo de digitación..."
              class="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              :class="{ 'border-destructive': !reason.trim() && errorMsg }"
            ></textarea>
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
            variant="destructive"
            size="sm"
            :disabled="isBusy || !reason.trim()"
          >
            <LoaderCircle v-if="isBusy" class="size-3.5 mr-1.5 animate-spin" />
            <RotateCcw v-else class="size-3.5 mr-1.5" />
            Confirmar Reverso
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
