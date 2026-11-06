<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ArrowRightLeft, LoaderCircle, AlertTriangle } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { CompanyBankAccount } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { Button } from '@prestamos/ui/components/ui/button'
import { Input } from '@prestamos/ui/components/ui/input'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Alert, AlertDescription } from '@prestamos/ui/components/ui/alert'

import { useTreasury } from '../composables/useTreasury'

// RF-24.5, RF-24.6: Transferencia interna entre cuentas bancarias propias (Fondeo / Barrido de Tesorería).

const props = defineProps<{
  open: boolean
  companyAccounts: CompanyBankAccount[]
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'saved'): void
}>()

const { createInternalTransfer } = useTreasury()
const isSubmitting = ref(false)
const errorMsg = ref<string | null>(null)

const activeAccounts = computed(() => props.companyAccounts.filter((a) => a.isActive))

const form = ref({
  sourceBankAccountId: 'bca-002',
  destinationBankAccountId: 'bca-001',
  amount: '5000.00',
  transferDate: new Date().toISOString().slice(0, 10),
  bankOperationNumber: '',
  notes: 'Fondeo de cuenta recaudadora a cuenta pagadora de desembolsos',
})

const selectedSourceAccount = computed(() =>
  activeAccounts.value.find((a) => a.id === form.value.sourceBankAccountId),
)

const destinationOptions = computed(() => {
  const sourceCurrency = selectedSourceAccount.value?.currency
  return activeAccounts.value.filter(
    (a) =>
      a.id !== form.value.sourceBankAccountId &&
      (!sourceCurrency || a.currency === sourceCurrency),
  )
})

watch(
  () => form.value.sourceBankAccountId,
  () => {
    if (!destinationOptions.value.some((a) => a.id === form.value.destinationBankAccountId)) {
      form.value.destinationBankAccountId = destinationOptions.value[0]?.id ?? ''
    }
  },
)

async function onSubmit() {
  errorMsg.value = null

  if (!form.value.sourceBankAccountId || !form.value.destinationBankAccountId) {
    errorMsg.value = 'Selecciona la cuenta de origen y la cuenta de destino.'
    return
  }

  if (form.value.sourceBankAccountId === form.value.destinationBankAccountId) {
    errorMsg.value = 'La cuenta de origen y destino deben ser diferentes.'
    return
  }

  const parsedAmount = Number(form.value.amount)
  if (!Number.isFinite(parsedAmount) || parsedAmount <= 0) {
    errorMsg.value = 'Ingresa un monto válido mayor a cero.'
    return
  }

  if (!form.value.bankOperationNumber.trim()) {
    errorMsg.value = 'Ingresa el número de operación bancaria para conciliar ambos extractos.'
    return
  }

  isSubmitting.value = true
  try {
    const created = await createInternalTransfer({
      sourceBankAccountId: form.value.sourceBankAccountId,
      destinationBankAccountId: form.value.destinationBankAccountId,
      amount: parsedAmount.toFixed(2),
      transferDate: form.value.transferDate,
      bankOperationNumber: form.value.bankOperationNumber.trim(),
      notes: form.value.notes.trim() || undefined,
    })

    toast.success('Transferencia interna registrada', {
      description: `Se registró el fondeo #${created.id} (${created.sourceBankName} → ${created.destinationBankName}) y se conciliaron sus movimientos bancarios.`,
    })
    form.value.bankOperationNumber = ''
    emit('saved')
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
    <Card class="w-full max-w-lg shadow-2xl border-border animate-in fade-in-0 zoom-in-95 duration-150">
      <CardHeader class="pb-3 border-b">
        <div class="flex items-center gap-2 text-primary">
          <ArrowRightLeft class="size-5" />
          <CardTitle class="text-lg">Transferencia Interna entre Cuentas Propias</CardTitle>
        </div>
        <CardDescription>
          Registra el fondeo o barrido de tesorería (ej. Cuenta Recaudadora → Cuenta Pagadora de
          Desembolsos) para cuadrar ambos extractos bancarios sin afectar préstamos (RF-24.5).
        </CardDescription>
      </CardHeader>

      <form @submit.prevent="onSubmit">
        <CardContent class="space-y-4 pt-4 text-xs">
          <Alert v-if="errorMsg" variant="destructive">
            <AlertTriangle class="size-4" />
            <AlertDescription>{{ errorMsg }}</AlertDescription>
          </Alert>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="srcAcc" class="text-xs font-medium">
                Cuenta Origen (Salida / Recaudadora)
              </label>
              <select
                id="srcAcc"
                v-model="form.sourceBankAccountId"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option v-for="acc in activeAccounts" :key="acc.id" :value="acc.id">
                  {{ acc.bankName }} ({{ acc.accountNumberMasked }}) · {{ acc.currency }}
                </option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label for="dstAcc" class="text-xs font-medium">
                Cuenta Destino (Entrada / Desembolsos)
              </label>
              <select
                id="dstAcc"
                v-model="form.destinationBankAccountId"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option v-for="acc in destinationOptions" :key="acc.id" :value="acc.id">
                  {{ acc.bankName }} ({{ acc.accountNumberMasked }}) · {{ acc.currency }}
                </option>
              </select>
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div class="space-y-1.5">
              <label for="trfAmount" class="text-xs font-medium">
                Monto ({{ selectedSourceAccount?.currency ?? 'PEN' }})
              </label>
              <Input
                id="trfAmount"
                v-model="form.amount"
                type="number"
                step="0.01"
                min="0.01"
                placeholder="5000.00"
              />
            </div>

            <div class="space-y-1.5">
              <label for="trfDate" class="text-xs font-medium">Fecha Valor</label>
              <Input id="trfDate" v-model="form.transferDate" type="date" />
            </div>

            <div class="space-y-1.5">
              <label for="trfOp" class="text-xs font-medium">Nº Operación Banco</label>
              <Input
                id="trfOp"
                v-model="form.bankOperationNumber"
                placeholder="FND-900120"
              />
            </div>
          </div>

          <div class="space-y-1.5">
            <label for="trfNotes" class="text-xs font-medium">Concepto / Glosa de Tesorería</label>
            <Input
              id="trfNotes"
              v-model="form.notes"
              placeholder="Ej. Barrido semanal de recaudación Yape/BCP hacia cuenta de desembolsos"
            />
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
            :disabled="isSubmitting || !form.bankOperationNumber.trim()"
          >
            <LoaderCircle v-if="isSubmitting" class="size-3.5 mr-1.5 animate-spin" />
            Registrar Fondeo Interno
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
