<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { DollarSign, LoaderCircle, AlertTriangle, ArrowRightLeft, Building2 } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { Customer360 } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { MoneyText } from '@prestamos/ui'
import { Button } from '@prestamos/ui/components/ui/button'
import { Input } from '@prestamos/ui/components/ui/input'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'

import { useCustomers } from '../composables/useCustomers'

// FE-BO-24: Gestión de Saldo a Favor (US-17, RF-17.3): aplicar a otro préstamo del cliente o devolver a cuenta verificada.

const props = defineProps<{
  open: boolean
  customer: Customer360
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'processed'): void
}>()

const { applyCreditBalance, refundCreditBalance } = useCustomers()

const actionMode = ref<'APPLY_LOAN' | 'REFUND_BANK'>('APPLY_LOAN')
const selectedLoanId = ref('')
const selectedBankAccountId = ref('')
const amount = ref('')
const isSubmitting = ref(false)
const errorMsg = ref<string | null>(null)

const eligibleLoans = computed(() =>
  props.customer.loans.filter((l) => l.status === 'VIGENTE' || l.status === 'EN_MORA'),
)

const verifiedBankAccounts = computed(() =>
  props.customer.bankAccounts.filter((a) => a.isVerified),
)

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      errorMsg.value = null
      amount.value = props.customer.creditBalance
      selectedLoanId.value = eligibleLoans.value[0]?.id ?? ''
      selectedBankAccountId.value =
        verifiedBankAccounts.value.find((a) => a.isPrimary)?.id ??
        verifiedBankAccounts.value[0]?.id ??
        ''
      if (eligibleLoans.value.length === 0 && verifiedBankAccounts.value.length > 0) {
        actionMode.value = 'REFUND_BANK'
      } else {
        actionMode.value = 'APPLY_LOAN'
      }
    }
  },
  { immediate: true },
)

async function onSubmit() {
  errorMsg.value = null
  const numAmount = Number(amount.value)
  const maxBalance = Number(props.customer.creditBalance)

  if (Number.isNaN(numAmount) || numAmount <= 0) {
    errorMsg.value = 'Ingresa un monto válido mayor a cero.'
    return
  }
  if (numAmount > maxBalance) {
    errorMsg.value = `El monto no puede superar el saldo a favor disponible (S/ ${props.customer.creditBalance}).`
    return
  }

  isSubmitting.value = true
  try {
    if (actionMode.value === 'APPLY_LOAN') {
      if (!selectedLoanId.value) {
        errorMsg.value = 'Selecciona un préstamo vigente o en mora para aplicar el saldo.'
        isSubmitting.value = false
        return
      }
      await applyCreditBalance(props.customer.id, {
        loanId: selectedLoanId.value,
        amount: numAmount.toFixed(2),
      })
      toast.success('Saldo a favor aplicado al préstamo', {
        description: `Se amortizaron S/ ${numAmount.toFixed(2)} en el préstamo #${selectedLoanId.value} con prelación de ley.`,
      })
    } else {
      if (!selectedBankAccountId.value) {
        errorMsg.value = 'Selecciona una cuenta bancaria verificada para procesar la devolución.'
        isSubmitting.value = false
        return
      }
      await refundCreditBalance(props.customer.id, {
        bankAccountId: selectedBankAccountId.value,
        amount: numAmount.toFixed(2),
      })
      toast.success('Devolución de saldo a favor procesada', {
        description: `Se registró la devolución de S/ ${numAmount.toFixed(2)} a la cuenta verificada del cliente.`,
      })
    }
    emit('processed')
    emit('update:open', false)
  } catch (error) {
    const apiError = toApiError(error)
    errorMsg.value = apiError.detail
    toast.error(apiError.title, { description: apiError.detail })
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div
    v-if="open"
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
  >
    <Card class="w-full max-w-lg shadow-xl border-border animate-in fade-in-0 zoom-in-95 duration-150">
      <CardHeader class="pb-3 border-b">
        <div class="flex items-center gap-2 text-primary">
          <DollarSign class="size-5" />
          <CardTitle class="text-lg">Gestión de Saldo a Favor (FE-BO-24)</CardTitle>
        </div>
        <CardDescription>
          Cliente: <strong>{{ customer.fullName }}</strong> · Saldo disponible:
          <strong class="text-primary font-mono"><MoneyText :amount="customer.creditBalance" /></strong>
        </CardDescription>
      </CardHeader>

      <form @submit.prevent="onSubmit">
        <CardContent class="space-y-4 pt-4">
          <div class="flex gap-2 p-1 bg-muted rounded-lg">
            <button
              type="button"
              class="flex-1 py-2 px-3 rounded-md text-xs font-semibold transition-all flex items-center justify-center gap-1.5"
              :class="
                actionMode === 'APPLY_LOAN'
                  ? 'bg-background shadow-xs text-primary'
                  : 'text-muted-foreground hover:text-foreground'
              "
              @click="actionMode = 'APPLY_LOAN'"
            >
              <ArrowRightLeft class="size-3.5" />
              Aplicar a Préstamo
            </button>
            <button
              type="button"
              class="flex-1 py-2 px-3 rounded-md text-xs font-semibold transition-all flex items-center justify-center gap-1.5"
              :class="
                actionMode === 'REFUND_BANK'
                  ? 'bg-background shadow-xs text-primary'
                  : 'text-muted-foreground hover:text-foreground'
              "
              @click="actionMode = 'REFUND_BANK'"
            >
              <Building2 class="size-3.5" />
              Devolver a Cuenta Verificada
            </button>
          </div>

          <div
            v-if="errorMsg"
            class="p-3 bg-destructive/10 text-destructive text-xs rounded-md flex items-start gap-2"
          >
            <AlertTriangle class="size-4 shrink-0 mt-0.5" />
            <span>{{ errorMsg }}</span>
          </div>

          <!-- Opción 1: Aplicar a otro préstamo vigente -->
          <div v-if="actionMode === 'APPLY_LOAN'" class="space-y-1.5">
            <label for="targetLoan" class="text-xs font-medium">Préstamo Destino (Vigente o en Mora)</label>
            <select
              v-if="eligibleLoans.length > 0"
              id="targetLoan"
              v-model="selectedLoanId"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option v-for="l in eligibleLoans" :key="l.id" :value="l.id">
                #{{ l.id }} — {{ l.productName }} (Saldo: S/ {{ l.outstandingPrincipal }})
              </option>
            </select>
            <p v-else class="text-xs text-muted-foreground p-2.5 rounded border bg-muted/30">
              El cliente no tiene préstamos vigentes o en mora. Usa la opción de devolución a cuenta bancaria verificada.
            </p>
          </div>

          <!-- Opción 2: Devolución a cuenta verificada -->
          <div v-else class="space-y-1.5">
            <label for="targetBank" class="text-xs font-medium">Cuenta Bancaria Verificada (RF-17.3)</label>
            <select
              v-if="verifiedBankAccounts.length > 0"
              id="targetBank"
              v-model="selectedBankAccountId"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option v-for="acc in verifiedBankAccounts" :key="acc.id" :value="acc.id">
                {{ acc.bankName }} — {{ acc.accountNumberMasked }} {{ acc.isPrimary ? '(Primaria)' : '' }}
              </option>
            </select>
            <p v-else class="text-xs text-destructive p-2.5 rounded border border-destructive/30 bg-destructive/5">
              El cliente no posee cuentas bancarias verificadas. Verifica primero una cuenta en la pestaña Cuentas Bancarias.
            </p>
          </div>

          <!-- Monto -->
          <div class="space-y-1.5">
            <label for="creditAmount" class="text-xs font-medium">Monto a Disponer (S/)</label>
            <Input
              id="creditAmount"
              v-model="amount"
              type="text"
              placeholder="0.00"
              class="font-mono"
            />
            <p class="text-[11px] text-muted-foreground">
              Máximo disponible: S/ {{ customer.creditBalance }}
            </p>
          </div>
        </CardContent>

        <div class="flex items-center justify-end gap-2 p-4 border-t bg-muted/20">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            :disabled="isSubmitting"
            @click="emit('update:open', false)"
          >
            Cancelar
          </Button>
          <Button
            type="submit"
            size="sm"
            :disabled="
              isSubmitting ||
              (actionMode === 'APPLY_LOAN' && eligibleLoans.length === 0) ||
              (actionMode === 'REFUND_BANK' && verifiedBankAccounts.length === 0)
            "
          >
            <LoaderCircle v-if="isSubmitting" class="size-3.5 mr-1.5 animate-spin" />
            {{ actionMode === 'APPLY_LOAN' ? 'Aplicar a Préstamo' : 'Procesar Devolución' }}
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
