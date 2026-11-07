<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { Layers, LoaderCircle, AlertTriangle } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { Loan, LoanSummary } from '@prestamos/api-client'
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

// FE-BO-26: Refinanciamiento / Consolidación de uno o varios préstamos en un nuevo préstamo (US-32).

const props = defineProps<{
  open: boolean
  customerName: string
  customerLoans: LoanSummary[]
  initialLoanId?: string
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'refinanced', newLoan: Loan): void
}>()

const { refinanceLoans } = useLoans()

const selectedLoanIds = ref<string[]>([])
const newTerm = ref(18)
const newAnnualRate = ref('0.320000')
const newGraceDays = ref(0)
const errorMsg = ref<string | null>(null)

const eligibleLoans = computed(() =>
  props.customerLoans.filter((l) => l.status === 'VIGENTE' || l.status === 'EN_MORA'),
)

const totalPrincipalToConsolidate = computed(() => {
  const sum = eligibleLoans.value
    .filter((l) => selectedLoanIds.value.includes(l.id))
    .reduce((acc, l) => acc + Number(l.outstandingPrincipal), 0)
  return sum.toFixed(2)
})

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      errorMsg.value = null
      if (props.initialLoanId) {
        selectedLoanIds.value = [props.initialLoanId]
      } else {
        selectedLoanIds.value = eligibleLoans.value.map((l) => l.id)
      }
    }
  },
  { immediate: true },
)

function toggleLoan(id: string) {
  const idx = selectedLoanIds.value.indexOf(id)
  if (idx >= 0) {
    selectedLoanIds.value.splice(idx, 1)
  } else {
    selectedLoanIds.value.push(id)
  }
}

const { run: submitRefinance, isBusy } = useBusyAction(async () => {
  errorMsg.value = null
  if (selectedLoanIds.value.length === 0) {
    errorMsg.value = 'Selecciona al menos un préstamo vigente o en mora para refinanciar.'
    return
  }

  try {
    const res = await refinanceLoans({
      loanIds: selectedLoanIds.value,
      term: newTerm.value,
      annualRate: newAnnualRate.value || undefined,
      graceDays: newGraceDays.value,
    })
    toast.success('Refinanciamiento completado', {
      description: `Se cancelaron ${res.consolidatedLoans.length} préstamo(s) y se originó el nuevo préstamo #${res.newLoan.id}.`,
    })
    emit('refinanced', res.newLoan)
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
    <Card class="w-full max-w-lg shadow-2xl border-border animate-in fade-in-0 zoom-in-95 duration-150">
      <CardHeader class="pb-3 border-b">
        <div class="flex items-center gap-2 text-primary">
          <Layers class="size-5" />
          <CardTitle class="text-lg">Refinanciar / Consolidar Deuda (FE-BO-26)</CardTitle>
        </div>
        <CardDescription>
          Cancela uno o varios préstamos vigentes de <strong>{{ customerName }}</strong> y origina un nuevo contrato (US-32).
        </CardDescription>
      </CardHeader>

      <form @submit.prevent="submitRefinance">
        <CardContent class="space-y-4 pt-4">
          <div
            v-if="errorMsg"
            class="p-3 bg-destructive/10 text-destructive text-xs rounded-md flex items-start gap-2"
          >
            <AlertTriangle class="size-4 shrink-0 mt-0.5" />
            <span>{{ errorMsg }}</span>
          </div>

          <div class="space-y-2">
            <label class="text-xs font-medium block">Préstamos a Consolidar</label>
            <div class="space-y-1.5 max-h-40 overflow-y-auto pr-1">
              <label
                v-for="l in eligibleLoans"
                :key="l.id"
                class="flex items-center justify-between p-2.5 rounded-lg border text-xs cursor-pointer transition-colors"
                :class="
                  selectedLoanIds.includes(l.id)
                    ? 'border-primary bg-primary/5'
                    : 'hover:bg-muted/40'
                "
              >
                <div class="flex items-center gap-2">
                  <input
                    type="checkbox"
                    :checked="selectedLoanIds.includes(l.id)"
                    class="size-4"
                    @change="toggleLoan(l.id)"
                  />
                  <div>
                    <span class="font-mono font-bold">#{{ l.id }}</span> · {{ l.productName }}
                    <span
                      v-if="l.daysLate > 0"
                      class="ml-1 text-[10px] text-destructive font-semibold"
                    >
                      ({{ l.daysLate }}d mora)
                    </span>
                  </div>
                </div>
                <span class="font-mono font-semibold">
                  <MoneyText :amount="l.outstandingPrincipal" />
                </span>
              </label>
            </div>
          </div>

          <div class="rounded-lg border p-3 bg-muted/30 flex justify-between items-center text-xs">
            <span class="text-muted-foreground">Nuevo Capital Consolidado (aprox.):</span>
            <span class="text-base font-bold text-primary font-mono">
              <MoneyText :amount="totalPrincipalToConsolidate" />
            </span>
          </div>

          <div class="grid grid-cols-3 gap-3">
            <div class="space-y-1.5">
              <label for="refTerm" class="text-xs font-medium">Nuevo Plazo (cuotas)</label>
              <Input id="refTerm" v-model.number="newTerm" type="number" min="1" max="60" />
            </div>

            <div class="space-y-1.5">
              <label for="refRate" class="text-xs font-medium">Nueva TEA</label>
              <Input id="refRate" v-model="newAnnualRate" type="text" placeholder="0.320000" />
            </div>

            <div class="space-y-1.5">
              <label for="refGrace" class="text-xs font-medium">Días Gracia</label>
              <Input id="refGrace" v-model.number="newGraceDays" type="number" min="0" />
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
          <Button type="submit" size="sm" :disabled="isBusy || selectedLoanIds.length === 0">
            <LoaderCircle v-if="isBusy" class="size-3.5 mr-1.5 animate-spin" />
            Refinanciar ({{ selectedLoanIds.length }})
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
