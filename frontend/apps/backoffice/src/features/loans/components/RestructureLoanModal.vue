<script setup lang="ts">
import { ref, watch } from 'vue'
import { RefreshCw, LoaderCircle, AlertTriangle } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { Loan } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { MoneyText, useBusyAction } from '@prestamos/ui'
import { Button } from '@prestamos/ui/components/ui/button'
import { Input } from '@prestamos/ui/components/ui/input'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'

import { useLoans } from '../composables/useLoans'

// FE-BO-26: Reestructuración de un préstamo (US-31): ampliar plazo, ajustar tasa o días de gracia generando schedule v+1.

const props = defineProps<{
  open: boolean
  loan: Loan
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'restructured', updated: Loan): void
}>()

const { restructureLoan } = useLoans()

const newTerm = ref(12)
const newAnnualRate = ref('')
const newGraceDays = ref(0)
const errorMsg = ref<string | null>(null)

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      errorMsg.value = null
      newTerm.value = props.loan.term + 6
      newAnnualRate.value = props.loan.annualRate
      newGraceDays.value = props.loan.graceDays ?? 0
    }
  },
  { immediate: true },
)

const { run: submitRestructure, isBusy } = useBusyAction(async () => {
  errorMsg.value = null
  if (!newTerm.value || newTerm.value <= 0) {
    errorMsg.value = 'Indica un nuevo plazo de cuotas mayor a cero.'
    return
  }

  try {
    const updated = await restructureLoan(props.loan.id, {
      term: newTerm.value,
      annualRate: newAnnualRate.value || undefined,
      graceDays: newGraceDays.value,
    })
    toast.success('Préstamo reestructurado', {
      description: `Se generó la Versión ${updated.scheduleVersion} del cronograma con ${updated.term} cuotas.`,
    })
    emit('restructured', updated)
    emit('update:open', false)
  } catch (error) {
    const apiError = toApiError(error)
    errorMsg.value = apiError.detail
    toast.error(apiError.title, { description: apiError.detail })
  }
})
</script>

<template>
  <div
    v-if="open"
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
  >
    <Card class="w-full max-w-md shadow-2xl border-border animate-in fade-in-0 zoom-in-95 duration-150">
      <CardHeader class="pb-3 border-b">
        <div class="flex items-center gap-2 text-primary">
          <RefreshCw class="size-5" />
          <CardTitle class="text-lg">Reestructurar Préstamo (FE-BO-26)</CardTitle>
        </div>
        <CardDescription>
          Préstamo <strong>#{{ loan.id }}</strong> · {{ loan.customerName }} (US-31).
        </CardDescription>
      </CardHeader>

      <form @submit.prevent="submitRestructure">
        <CardContent class="space-y-4 pt-4">
          <div
            v-if="errorMsg"
            class="p-3 bg-destructive/10 text-destructive text-xs rounded-md flex items-start gap-2"
          >
            <AlertTriangle class="size-4 shrink-0 mt-0.5" />
            <span>{{ errorMsg }}</span>
          </div>

          <div class="rounded-lg border p-3 bg-muted/30 text-xs space-y-1.5">
            <div class="flex justify-between">
              <span class="text-muted-foreground">Saldo Insoluto a Reprogramar:</span>
              <span class="font-bold text-foreground">
                <MoneyText :amount="loan.outstandingPrincipal" />
              </span>
            </div>
            <div class="flex justify-between">
              <span class="text-muted-foreground">Versión de Cronograma Actual:</span>
              <span class="font-mono font-semibold">v{{ loan.scheduleVersion }} → v{{ loan.scheduleVersion + 1 }}</span>
            </div>
          </div>

          <div class="space-y-1.5">
            <label for="restructureTerm" class="text-xs font-medium">Nuevo Plazo Total (cuotas)</label>
            <Input
              id="restructureTerm"
              v-model.number="newTerm"
              type="number"
              min="1"
              max="60"
            />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="restructureRate" class="text-xs font-medium">Nueva TEA (Decimal)</label>
              <Input
                id="restructureRate"
                v-model="newAnnualRate"
                type="text"
                placeholder="0.350000"
              />
            </div>

            <div class="space-y-1.5">
              <label for="restructureGrace" class="text-xs font-medium">Días de Gracia</label>
              <Input
                id="restructureGrace"
                v-model.number="newGraceDays"
                type="number"
                min="0"
                max="60"
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
          <Button type="submit" size="sm" :disabled="isBusy">
            <LoaderCircle v-if="isBusy" class="size-3.5 mr-1.5 animate-spin" />
            Generar Cronograma v{{ loan.scheduleVersion + 1 }}
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
