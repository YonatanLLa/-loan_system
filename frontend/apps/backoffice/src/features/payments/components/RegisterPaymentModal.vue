<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { PlusCircle, CreditCard, LoaderCircle, AlertTriangle } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { CompanyBankAccount, Loan, PaymentMethodType } from '@prestamos/api-client'
import { newIdempotencyKey, toApiError } from '@prestamos/api-client'
import { useBusyAction } from '@prestamos/ui'
import { Button } from '@prestamos/ui/components/ui/button'
import { Input } from '@prestamos/ui/components/ui/input'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Alert, AlertDescription, AlertTitle } from '@prestamos/ui/components/ui/alert'

import { useApi } from '@/app/api'

// FE-BO-22: Registrar un pago identificado en el extracto (canal, cuenta, referencia, fecha).

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'registered'): void
}>()

const loans = ref<Loan[]>([])
const companyAccounts = ref<CompanyBankAccount[]>([])

const form = ref({
  loanId: '',
  amount: '',
  method: 'TRANSFERENCIA_BANCARIA' as PaymentMethodType,
  companyBankAccountId: '',
  externalReference: '',
  paidAt: new Date().toISOString().slice(0, 16),
})

const errorMsg = ref<string | null>(null)

async function loadData() {
  try {
    const [loanRes, accRes] = await Promise.all([
      useApi().get<{ items: Loan[] }>('/loans?status=VIGENTE,EN_MORA'),
      useApi().get<CompanyBankAccount[]>('/treasury/company-accounts'),
    ])
    loans.value = loanRes.data.items
    companyAccounts.value = accRes.data.filter((a) => a.isActive && a.isCollection)

    if (companyAccounts.value.length > 0) {
      form.value.companyBankAccountId = companyAccounts.value[0]?.id ?? ''
    }
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar datos de pago', { description: apiError.detail })
  }
}

const { run: submitPayment, isBusy } = useBusyAction(async () => {
  errorMsg.value = null

  if (!form.value.loanId || !form.value.amount || !form.value.externalReference.trim()) {
    errorMsg.value = 'El préstamo, monto y número de operación son obligatorios.'
    return
  }

  try {
    const idempotencyKey = newIdempotencyKey()
    await useApi().post(
      `/loans/${form.value.loanId}/payments`,
      {
        amount: form.value.amount,
        method: form.value.method,
        companyBankAccountId: form.value.companyBankAccountId || undefined,
        externalReference: form.value.externalReference.trim(),
        paidAt: new Date(form.value.paidAt).toISOString(),
      },
      {
        headers: { 'Idempotency-Key': idempotencyKey },
      },
    )

    toast.success('Pago registrado exitosamente', {
      description: `El abono por S/ ${form.value.amount} fue imputado al préstamo según la cascada legal.`,
    })
    emit('registered')
    emit('update:open', false)
  } catch (error) {
    const apiError = toApiError(error)
    errorMsg.value = apiError.detail
    toast.error(apiError.title, { description: apiError.detail })
  }
})

onMounted(() => {
  void loadData()
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
          <PlusCircle class="size-5" />
          <CardTitle class="text-lg">Registrar Pago Identificado en Extracto</CardTitle>
        </div>
        <CardDescription>
          Aplica un abono bancario directamente sobre el préstamo con imputación en cascada (FE-BO-22).
        </CardDescription>
      </CardHeader>

      <form @submit.prevent="submitPayment">
        <CardContent class="space-y-4 pt-4">
          <Alert v-if="errorMsg" variant="destructive">
            <AlertTriangle class="size-4" />
            <AlertDescription>{{ errorMsg }}</AlertDescription>
          </Alert>

          <!-- Préstamo -->
          <div class="space-y-1.5">
            <label for="payLoan" class="text-xs font-medium">Préstamo a Amortizar</label>
            <select
              id="payLoan"
              v-model="form.loanId"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="">Selecciona el préstamo...</option>
              <option v-for="l in loans" :key="l.id" :value="l.id">
                {{ l.id }} - {{ l.customerName }} (Saldo: S/ {{ l.outstandingPrincipal }})
              </option>
            </select>
          </div>

          <!-- Monto y Medio -->
          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="payAmount" class="text-xs font-medium">Monto Pagado (S/)</label>
              <Input
                id="payAmount"
                v-model="form.amount"
                type="text"
                placeholder="Ej. 520.00"
              />
            </div>

            <div class="space-y-1.5">
              <label for="payMethod" class="text-xs font-medium">Canal de Pago</label>
              <select
                id="payMethod"
                v-model="form.method"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="TRANSFERENCIA_BANCARIA">Transferencia Bancaria</option>
                <option value="YAPE">Yape</option>
                <option value="PLIN">Plin</option>
                <option value="TARJETA_DEBITO">Tarjeta de Débito</option>
              </select>
            </div>
          </div>

          <!-- Cuenta de Destino (Fintech) y Referencia -->
          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="payAcc" class="text-xs font-medium">Cuenta Recaudadora</label>
              <select
                id="payAcc"
                v-model="form.companyBankAccountId"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option v-for="acc in companyAccounts" :key="acc.id" :value="acc.id">
                  {{ acc.bankName }} ({{ acc.accountNumberMasked }})
                </option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label for="payRef" class="text-xs font-medium">Nº Operación / Referencia</label>
              <Input
                id="payRef"
                v-model="form.externalReference"
                placeholder="Ej. OP-445566"
              />
            </div>
          </div>

          <!-- Fecha Efectiva -->
          <div class="space-y-1.5">
            <label for="payDate" class="text-xs font-medium">Fecha y Hora Efectiva en Extracto</label>
            <Input
              id="payDate"
              v-model="form.paidAt"
              type="datetime-local"
            />
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
            :disabled="isBusy || !form.loanId || !form.amount"
          >
            <LoaderCircle v-if="isBusy" class="size-3.5 mr-1.5 animate-spin" />
            <PlusCircle v-else class="size-3.5 mr-1.5" />
            Aplicar Pago
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
