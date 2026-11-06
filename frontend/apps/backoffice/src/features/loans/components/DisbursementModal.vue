<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { ArrowUpRight, Building2, CheckCircle2, LoaderCircle, AlertTriangle } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type {
  CompanyBankAccount,
  CustomerBankAccount,
  LoanApplication,
} from '@prestamos/api-client'
import { newIdempotencyKey, toApiError } from '@prestamos/api-client'
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

import { useApi } from '@/app/api'

// FE-BO-19: Iniciar desembolso digital con Idempotency-Key y botón protegido contra doble clic.

const props = defineProps<{
  open: boolean
  preselectedApplicationId?: string
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'disbursed'): void
}>()

const applications = ref<LoanApplication[]>([])
const companyAccounts = ref<CompanyBankAccount[]>([])
const customerAccounts = ref<CustomerBankAccount[]>([])

const selectedAppId = ref(props.preselectedApplicationId ?? '')
const selectedCompanyAccId = ref('')
const selectedCustomerAccId = ref('')
const errorMsg = ref<string | null>(null)

const selectedApp = computed(() =>
  applications.value.find((a) => a.id === selectedAppId.value) || null,
)

async function loadData() {
  try {
    const [appRes, compRes] = await Promise.all([
      useApi().get<{ items: LoanApplication[] }>('/loan-applications?status=APROBADO,APROBADO_CON_CONDICIONES'),
      useApi().get<CompanyBankAccount[]>('/treasury/bank-accounts'),
    ])
    applications.value = appRes.data.items
    companyAccounts.value = compRes.data.filter((a) => a.isActive && a.isDisbursement)

    if (companyAccounts.value.length > 0 && !selectedCompanyAccId.value) {
      selectedCompanyAccId.value = companyAccounts.value[0]?.id ?? ''
    }

    if (props.preselectedApplicationId) {
      selectedAppId.value = props.preselectedApplicationId
    } else if (applications.value.length > 0 && !selectedAppId.value) {
      selectedAppId.value = applications.value[0]?.id ?? ''
    }

    if (selectedApp.value) {
      await loadCustomerAccounts(selectedApp.value.customerId)
    }
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar datos para desembolso', { description: apiError.detail })
  }
}

async function loadCustomerAccounts(customerId: string) {
  try {
    const { data } = await useApi().get<CustomerBankAccount[]>(
      `/customers/${customerId}/bank-accounts`,
    )
    customerAccounts.value = data.filter((a) => a.isVerified)
    const primary = customerAccounts.value.find((a) => a.isPrimary)
    selectedCustomerAccId.value = primary?.id ?? customerAccounts.value[0]?.id ?? ''
  } catch {
    customerAccounts.value = []
  }
}

const { run: submitDisbursement, isBusy } = useBusyAction(async () => {
  errorMsg.value = null

  if (!selectedAppId.value || !selectedCompanyAccId.value || !selectedCustomerAccId.value) {
    errorMsg.value = 'Completa todos los campos obligatorios para el desembolso.'
    return
  }

  try {
    // FE-BO-19: Idempotency-Key generada en el cliente para evitar desembolsos duplicados por reintentos de red
    const idempotencyKey = newIdempotencyKey()

    await useApi().post(
      '/loans',
      {
        applicationId: selectedAppId.value,
        companyBankAccountId: selectedCompanyAccId.value,
        customerBankAccountId: selectedCustomerAccId.value,
      },
      {
        headers: { 'Idempotency-Key': idempotencyKey },
      },
    )

    toast.success('Desembolso iniciado', {
      description: 'La orden de transferencia bancaria fue transmitida exitosamente.',
    })
    emit('disbursed')
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
          <ArrowUpRight class="size-5" />
          <CardTitle class="text-lg">Iniciar Desembolso 100% Digital</CardTitle>
        </div>
        <CardDescription>
          Genera el préstamo formal y emite la transferencia bancaria con Idempotency-Key (FE-BO-19).
        </CardDescription>
      </CardHeader>

      <form @submit.prevent="submitDisbursement">
        <CardContent class="space-y-4 pt-4">
          <Alert v-if="errorMsg" variant="destructive">
            <AlertTriangle class="size-4" />
            <AlertDescription>{{ errorMsg }}</AlertDescription>
          </Alert>

          <!-- Solicitud Aprobada -->
          <div class="space-y-1.5">
            <label for="appSelect" class="text-xs font-medium">Solicitud Aprobada a Desembolsar</label>
            <select
              id="appSelect"
              v-model="selectedAppId"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              @change="selectedApp && loadCustomerAccounts(selectedApp.customerId)"
            >
              <option value="">Selecciona una solicitud...</option>
              <option v-for="app in applications" :key="app.id" :value="app.id">
                #{{ app.id }} - {{ app.customerName }} (S/ {{ app.approvedAmount || app.requestedAmount }})
              </option>
            </select>
          </div>

          <!-- Cuenta de Origen (Fintech) -->
          <div class="space-y-1.5">
            <label for="compAccount" class="text-xs font-medium">Cuenta de Origen (Fintech)</label>
            <select
              id="compAccount"
              v-model="selectedCompanyAccId"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option v-for="acc in companyAccounts" :key="acc.id" :value="acc.id">
                {{ acc.bankName }} - {{ acc.accountNumberMasked }} ({{ acc.currency }})
              </option>
            </select>
          </div>

          <!-- Cuenta de Destino (Cliente) -->
          <div class="space-y-1.5">
            <label for="custAccount" class="text-xs font-medium">Cuenta Destino del Cliente (Verificada)</label>
            <div v-if="customerAccounts.length === 0" class="text-xs text-destructive p-2.5 bg-destructive/10 rounded-md">
              El cliente no tiene cuentas bancarias verificadas activas. Valida una cuenta en su Ficha 360 antes de desembolsar.
            </div>
            <select
              v-else
              id="custAccount"
              v-model="selectedCustomerAccId"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option v-for="acc in customerAccounts" :key="acc.id" :value="acc.id">
                {{ acc.bankName }} ({{ acc.accountNumberMasked }}) - {{ acc.isPrimary ? 'PRIMARIA' : 'SECUNDARIA' }}
              </option>
            </select>
          </div>

          <!-- Resumen de monto -->
          <div v-if="selectedApp" class="p-3 rounded-lg border bg-muted/40 space-y-1 text-xs">
            <div class="flex justify-between items-center">
              <span class="text-muted-foreground">Monto Neto a Transferir:</span>
              <span class="text-base font-bold text-primary">
                <MoneyText :amount="selectedApp.approvedAmount || selectedApp.requestedAmount" />
              </span>
            </div>
            <p class="text-[11px] text-muted-foreground pt-1">
              Protección anti doble-clic activa con Idempotency-Key para garantizar una sola transferencia.
            </p>
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
            :disabled="isBusy || !selectedAppId || !selectedCustomerAccId"
          >
            <LoaderCircle v-if="isBusy" class="size-3.5 mr-1.5 animate-spin" />
            <ArrowUpRight v-else class="size-3.5 mr-1.5" />
            Ejecutar Desembolso
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
