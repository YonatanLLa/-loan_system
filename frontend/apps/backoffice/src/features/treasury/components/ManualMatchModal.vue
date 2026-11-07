<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { CheckCircle2, ArrowRightLeft, AlertTriangle, LoaderCircle } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { BankStatementLine, Payment } from '@prestamos/api-client'
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
import { Alert, AlertDescription } from '@prestamos/ui/components/ui/alert'

import { useTreasury } from '../composables/useTreasury'

// FE-BO-29: Conciliación manual de abonos no identificados con pagos pendientes.

const props = defineProps<{
  open: boolean
  statementLine: BankStatementLine | null
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'matched'): void
}>()

const { getPendingConfirmationPayments, matchStatementLine } = useTreasury()

const pendingPayments = ref<Payment[]>([])
const selectedPaymentId = ref('')
const errorMsg = ref<string | null>(null)

async function loadPendingPayments() {
  try {
    pendingPayments.value = await getPendingConfirmationPayments()
    if (pendingPayments.value.length > 0) {
      selectedPaymentId.value = pendingPayments.value[0]?.id ?? ''
    }
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar pagos pendientes', { description: apiError.detail })
  }
}

const { run: submitMatch, isBusy } = useBusyAction(async () => {
  if (!props.statementLine || !selectedPaymentId.value) return
  errorMsg.value = null

  try {
    await matchStatementLine(props.statementLine.id, selectedPaymentId.value)

    toast.success('Conciliación manual completada', {
      description: `El abono ${props.statementLine.operationNumber} fue emparejado con el pago exitosamente.`,
    })
    emit('matched')
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
      void loadPendingPayments()
    }
  },
)

onMounted(() => {
  if (props.open) {
    void loadPendingPayments()
  }
})
</script>

<template>
  <div
    v-if="open && statementLine"
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
  >
    <Card class="w-full max-w-md shadow-2xl border-border animate-in fade-in-0 zoom-in-95 duration-150">
      <CardHeader class="pb-3 border-b">
        <div class="flex items-center gap-2 text-primary">
          <ArrowRightLeft class="size-5" />
          <CardTitle class="text-lg">Conciliación Manual de Abono</CardTitle>
        </div>
        <CardDescription>
          Empareja un movimiento bancario sin identificar con un voucher reportado (FE-BO-29).
        </CardDescription>
      </CardHeader>

      <form @submit.prevent="submitMatch">
        <CardContent class="space-y-4 pt-4 text-xs">
          <Alert v-if="errorMsg" variant="destructive">
            <AlertTriangle class="size-4" />
            <AlertDescription>{{ errorMsg }}</AlertDescription>
          </Alert>

          <!-- Movimiento del extracto -->
          <div class="rounded-lg border p-3 bg-muted/40 space-y-1">
            <span class="text-muted-foreground block text-[10px]">Movimiento de Extracto:</span>
            <div class="flex justify-between items-center font-bold text-foreground">
              <span>{{ statementLine.operationNumber }}</span>
              <span class="text-sm font-extrabold text-primary">
                <MoneyText :amount="statementLine.amount" />
              </span>
            </div>
            <p class="text-[11px] text-muted-foreground">{{ statementLine.description }}</p>
          </div>

          <!-- Selector de pago pendiente -->
          <div class="space-y-1.5">
            <label for="paySelect" class="text-xs font-semibold text-foreground">
              Voucher / Pago Pendiente de Conciliar
            </label>
            <div v-if="pendingPayments.length === 0" class="p-3 bg-muted rounded text-muted-foreground">
              No hay pagos pendientes de confirmación para emparejar.
            </div>
            <select
              v-else
              id="paySelect"
              v-model="selectedPaymentId"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option v-for="p in pendingPayments" :key="p.id" :value="p.id">
                #{{ p.id }} - {{ p.customerName }} (S/ {{ p.amount }}) - Ref: {{ p.externalReference || 'Sin ref' }}
              </option>
            </select>
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
            :disabled="isBusy || !selectedPaymentId"
          >
            <LoaderCircle v-if="isBusy" class="size-3.5 mr-1.5 animate-spin" />
            <CheckCircle2 v-else class="size-3.5 mr-1.5" />
            Emparejar y Conciliar
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
