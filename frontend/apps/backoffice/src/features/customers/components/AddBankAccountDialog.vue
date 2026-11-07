<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { Building2, LoaderCircle } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { CustomerBankAccount } from '@prestamos/api-client'
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

import { useCustomers } from '../composables/useCustomers'

// FE-BO-09: Cuentas bancarias del cliente: alta de cuenta o billetera digital.

const props = defineProps<{
  open: boolean
  customerId: string
  customerName: string
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'added', account: CustomerBankAccount): void
}>()

const { createBankAccount } = useCustomers()
const isSubmitting = ref(false)
const errorMsg = ref<string | null>(null)

const form = ref({
  bankName: 'BCP',
  accountType: 'CUENTA_AHORRO' as CustomerBankAccount['accountType'],
  cci: '',
  phoneWallet: '',
})

const isWallet = computed(() => form.value.accountType === 'BILLETERA_DIGITAL')

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      errorMsg.value = null
      form.value = {
        bankName: 'BCP',
        accountType: 'CUENTA_AHORRO',
        cci: '',
        phoneWallet: '',
      }
    }
  },
)

function onAccountTypeChange() {
  if (isWallet.value) {
    if (form.value.bankName !== 'Yape' && form.value.bankName !== 'Plin') {
      form.value.bankName = 'Yape'
    }
  } else {
    if (form.value.bankName === 'Yape' || form.value.bankName === 'Plin') {
      form.value.bankName = 'BCP'
    }
  }
}

async function onSubmit() {
  errorMsg.value = null

  if (isWallet.value) {
    if (!/^9\d{8}$/.test(form.value.phoneWallet)) {
      errorMsg.value = 'El número de celular para la billetera debe tener 9 dígitos y empezar con 9.'
      return
    }
  } else {
    if (!/^\d{20}$/.test(form.value.cci)) {
      errorMsg.value = 'El Código de Cuenta Interbancario (CCI) debe contener exactamente 20 dígitos numéricos.'
      return
    }
  }

  isSubmitting.value = true
  try {
    const payload = {
      bankName: form.value.bankName,
      accountType: form.value.accountType,
      cci: isWallet.value ? undefined : form.value.cci,
      phoneWallet: isWallet.value ? form.value.phoneWallet : undefined,
    }

    const data = await createBankAccount(props.customerId, payload)

    toast.success('Cuenta registrada', {
      description: `Se agregó la cuenta ${data.bankName} (${data.accountNumberMasked}) para desembolsos.`,
    })

    emit('added', data)
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
    <Card class="w-full max-w-md shadow-xl border-border animate-in fade-in-0 zoom-in-95 duration-150">
      <CardHeader class="pb-3">
        <div class="flex items-center gap-2 text-primary">
          <Building2 class="size-5" />
          <CardTitle class="text-lg">Nueva Cuenta Bancaria</CardTitle>
        </div>
        <CardDescription>
          Registra una cuenta de desembolso para <strong>{{ customerName }}</strong> (FE-BO-09).
        </CardDescription>
      </CardHeader>

      <form @submit.prevent="onSubmit">
        <CardContent class="space-y-4">
          <div v-if="errorMsg" class="p-3 bg-destructive/10 text-destructive text-xs rounded-md">
            {{ errorMsg }}
          </div>

          <div class="space-y-1.5">
            <label for="accountType" class="text-xs font-medium">Tipo de Instrumento</label>
            <select
              id="accountType"
              v-model="form.accountType"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              @change="onAccountTypeChange"
            >
              <option value="CUENTA_AHORRO">Cuenta de Ahorro</option>
              <option value="CUENTA_CORRIENTE">Cuenta Corriente</option>
              <option value="BILLETERA_DIGITAL">Billetera Digital (Yape / Plin)</option>
              <option value="TARJETA_DEBITO">Tarjeta de Débito</option>
            </select>
          </div>

          <div class="space-y-1.5">
            <label for="bankName" class="text-xs font-medium">Entidad Financiera</label>
            <select
              id="bankName"
              v-model="form.bankName"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <template v-if="isWallet">
                <option value="Yape">Yape (BCP)</option>
                <option value="Plin">Plin (Interbank / BBVA / Scotiabank)</option>
              </template>
              <template v-else>
                <option value="BCP">Banco de Crédito del Perú (BCP)</option>
                <option value="Interbank">Interbank</option>
                <option value="BBVA">BBVA Perú</option>
                <option value="Scotiabank">Scotiabank</option>
                <option value="Banco de la Nación">Banco de la Nación</option>
                <option value="BanBif">BanBif</option>
                <option value="Pichincha">Banco Pichincha</option>
              </template>
            </select>
          </div>

          <!-- Si es billetera digital: Teléfono -->
          <div v-if="isWallet" class="space-y-1.5">
            <label for="phoneWallet" class="text-xs font-medium">Número de Celular Asociado</label>
            <Input
              id="phoneWallet"
              v-model="form.phoneWallet"
              placeholder="Ej. 987654321"
              maxlength="9"
            />
            <p class="text-[11px] text-muted-foreground">
              Debe coincidir con la titularidad del cliente para ser validada.
            </p>
          </div>

          <!-- Si es cuenta bancaria: CCI -->
          <div v-else class="space-y-1.5">
            <label for="cci" class="text-xs font-medium">CCI (Código de Cuenta Interbancario)</label>
            <Input
              id="cci"
              v-model="form.cci"
              placeholder="20 dígitos numéricos (ej. 002190...)"
              maxlength="20"
            />
            <p class="text-[11px] text-muted-foreground">
              20 dígitos. Se guardará de forma enmascarada (**** 1234).
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
          <Button type="submit" size="sm" :disabled="isSubmitting">
            <LoaderCircle v-if="isSubmitting" class="size-3.5 mr-1.5 animate-spin" />
            Guardar Cuenta
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
