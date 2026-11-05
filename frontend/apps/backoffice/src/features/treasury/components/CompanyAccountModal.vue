<script setup lang="ts">
import { ref } from 'vue'
import { Building2, LoaderCircle, AlertTriangle } from '@lucide/vue'
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

import { useApi } from '@/app/api'

// FE-BO-27: Alta y configuración de cuentas bancarias de la Fintech (uso, moneda, activación).

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'saved'): void
}>()

const isSubmitting = ref(false)
const errorMsg = ref<string | null>(null)

const form = ref({
  bankName: 'BCP',
  cci: '',
  accountNumberMasked: '',
  currency: 'PEN' as 'PEN' | 'USD',
  isDisbursement: true,
  isCollection: true,
})

async function onSubmit() {
  errorMsg.value = null

  if (!/^\d{20}$/.test(form.value.cci)) {
    errorMsg.value = 'El Código de Cuenta Interbancario (CCI) debe contener exactamente 20 dígitos numéricos.'
    return
  }

  isSubmitting.value = true
  try {
    const payload = {
      bankName: form.value.bankName,
      accountNumberMasked: `**** ${form.value.cci.slice(-4)}`,
      cci: form.value.cci,
      currency: form.value.currency,
      isDisbursement: form.value.isDisbursement,
      isCollection: form.value.isCollection,
    }

    await useApi().post('/treasury/bank-accounts', payload)
    toast.success('Cuenta corporativa registrada', {
      description: `La cuenta ${payload.bankName} (${payload.currency}) fue añadida al catálogo de tesorería.`,
    })
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
    <Card class="w-full max-w-md shadow-2xl border-border animate-in fade-in-0 zoom-in-95 duration-150">
      <CardHeader class="pb-3 border-b">
        <div class="flex items-center gap-2 text-primary">
          <Building2 class="size-5" />
          <CardTitle class="text-lg">Nueva Cuenta Corporativa</CardTitle>
        </div>
        <CardDescription>
          Configura una cuenta bancaria de la Fintech para desembolsos y/o recaudación (FE-BO-27).
        </CardDescription>
      </CardHeader>

      <form @submit.prevent="onSubmit">
        <CardContent class="space-y-4 pt-4 text-xs">
          <Alert v-if="errorMsg" variant="destructive">
            <AlertTriangle class="size-4" />
            <AlertDescription>{{ errorMsg }}</AlertDescription>
          </Alert>

          <div class="space-y-1.5">
            <label for="bank" class="text-xs font-medium">Entidad Financiera</label>
            <select
              id="bank"
              v-model="form.bankName"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="BCP">Banco de Crédito del Perú (BCP)</option>
              <option value="Interbank">Interbank</option>
              <option value="BBVA">BBVA Perú</option>
              <option value="Scotiabank">Scotiabank</option>
              <option value="Banco de la Nación">Banco de la Nación</option>
              <option value="BanBif">BanBif</option>
              <option value="Pichincha">Banco Pichincha</option>
            </select>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="curr" class="text-xs font-medium">Moneda</label>
              <select
                id="curr"
                v-model="form.currency"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="PEN">Soles (PEN - S/)</option>
                <option value="USD">Dólares (USD - $)</option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label for="cci" class="text-xs font-medium">CCI (20 dígitos)</label>
              <Input
                id="cci"
                v-model="form.cci"
                placeholder="00219..."
                maxlength="20"
              />
            </div>
          </div>

          <!-- Uso de la Cuenta (FE-BO-27) -->
          <div class="space-y-2 pt-2 border-t">
            <span class="text-xs font-semibold block text-foreground">Finalidad y Operación Permitida</span>
            <div class="space-y-2">
              <label class="flex items-center gap-2 cursor-pointer">
                <input v-model="form.isDisbursement" type="checkbox" class="size-4 rounded text-primary" />
                <span>Habilitada para emitir <strong>Desembolsos</strong> a prestatarios</span>
              </label>
              <label class="flex items-center gap-2 cursor-pointer">
                <input v-model="form.isCollection" type="checkbox" class="size-4 rounded text-primary" />
                <span>Habilitada para recibir <strong>Recaudación y Cobro</strong> de cuotas</span>
              </label>
            </div>
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
          <Button type="submit" size="sm" :disabled="isSubmitting || !form.cci">
            <LoaderCircle v-if="isSubmitting" class="size-3.5 mr-1.5 animate-spin" />
            Guardar Cuenta
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
