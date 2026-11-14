<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { MessageSquarePlus, AlertTriangle, LoaderCircle, Phone, MessageCircle } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { AgingRow, CollectionAction } from '@prestamos/api-client'
import { MoneyText, StatusBadge } from '@prestamos/ui'
import { Alert, AlertDescription } from '@prestamos/ui/components/ui/alert'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Input } from '@prestamos/ui/components/ui/input'
import { formatBusinessDate, formatInstant } from '@prestamos/utils'

import { useCollectionActions } from '../composables'

// FE-BO-33: registrar gestiones y promesas; las incumplidas quedan resaltadas.

const ACTION_TYPES = [
  { value: 'LLAMADA_TELEFONICA', label: 'Llamada' },
  { value: 'WHATSAPP', label: 'WhatsApp' },
  { value: 'SMS', label: 'SMS' },
  { value: 'EMAIL', label: 'Correo' },
  { value: 'NOTIFICACION_PUSH', label: 'Notificación' },
  { value: 'PROMESA_DE_PAGO', label: 'Promesa de pago' },
  { value: 'ACUERDO_DE_PAGO_DIGITAL', label: 'Acuerdo de pago' },
] as const

const ACTION_LABEL: Record<CollectionAction['actionType'], string> = {
  LLAMADA_TELEFONICA: 'Llamada',
  WHATSAPP: 'WhatsApp',
  EMAIL: 'Correo',
  SMS: 'SMS',
  NOTIFICACION_PUSH: 'Notificación',
  PROMESA_DE_PAGO: 'Promesa de pago',
  ACUERDO_DE_PAGO_DIGITAL: 'Acuerdo de pago',
}

const selectClass =
  'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring'

const props = defineProps<{
  open: boolean
  row: AgingRow | null
}>()

const emit = defineEmits<{
  (e: 'update:open', value: boolean): void
  (e: 'saved'): void
}>()

const {
  history,
  loading: loadingHistory,
  isSubmitting: isBusy,
  error: errorMsg,
  loadHistory,
  recordAction,
} = useCollectionActions()

const actionType = ref<CollectionAction['actionType']>('LLAMADA_TELEFONICA')
const note = ref('')
const nextActionAt = ref('')
const promiseAmount = ref('')
const promiseDate = ref('')

const isPromise = computed(
  () => actionType.value === 'PROMESA_DE_PAGO' || actionType.value === 'ACUERDO_DE_PAGO_DIGITAL',
)

const canSubmit = computed(() => {
  if (!note.value.trim()) return false
  if (isPromise.value && (!promiseAmount.value.trim() || !promiseDate.value)) return false
  return true
})

watch(
  () => [props.open, props.row?.loanId] as const,
  ([open, loanId]) => {
    if (!open) return
    actionType.value = 'LLAMADA_TELEFONICA'
    note.value = ''
    nextActionAt.value = ''
    promiseAmount.value = ''
    promiseDate.value = ''
    if (loanId) {
      void loadHistory(loanId)
    }
  },
)

async function submit() {
  if (!props.row || !canSubmit.value) return
  try {
    await recordAction({
      loanId: props.row.loanId,
      actionType: actionType.value,
      note: note.value.trim(),
      nextActionAt: nextActionAt.value || undefined,
      promiseAmount: isPromise.value ? promiseAmount.value.trim() : undefined,
      promiseDate: isPromise.value ? promiseDate.value : undefined,
    })
    toast.success('Gestión registrada', {
      description: `${ACTION_LABEL[actionType.value]} quedó en la bitácora de ${props.row.customerName}.`,
    })
    note.value = ''
    promiseAmount.value = ''
    promiseDate.value = ''
    emit('saved')
  } catch {
    // El composable gestiona el mensaje de error
  }
}
</script>

<template>
  <div
    v-if="open && row"
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
  >
    <Card class="flex max-h-[90vh] w-full max-w-2xl flex-col shadow-2xl">
      <CardHeader class="border-b pb-3">
        <div class="flex items-center gap-2 text-primary">
          <MessageSquarePlus class="size-5" />
          <CardTitle class="text-lg">Gestión de cobranza</CardTitle>
        </div>
        <CardDescription class="flex flex-wrap items-center gap-2">
          <span>{{ row.customerName }} · {{ row.loanId }}</span>
          <span class="text-muted-foreground">·</span>
          <a
            :href="`tel:${row.phone}`"
            class="inline-flex items-center gap-1 font-mono text-primary hover:underline"
            title="Llamar"
          >
            <Phone class="size-3" />
            {{ row.phone }}
          </a>
          <a
            :href="`https://wa.me/51${row.phone.replace(/\D/g, '')}`"
            target="_blank"
            rel="noopener noreferrer"
            class="inline-flex items-center gap-1 font-medium text-emerald-600 dark:text-emerald-400 hover:underline"
            title="Abrir WhatsApp"
          >
            <MessageCircle class="size-3" />
            WhatsApp
          </a>
        </CardDescription>
      </CardHeader>

      <CardContent class="space-y-4 overflow-y-auto pt-4 text-xs">
        <div class="space-y-2">
          <p class="font-semibold text-foreground">Bitácora</p>
          <p v-if="loadingHistory" class="text-muted-foreground">Cargando gestiones…</p>
          <p v-else-if="history.length === 0" class="text-muted-foreground">
            Todavía no hay contactos registrados para este préstamo.
          </p>
          <ul v-else class="space-y-2">
            <li
              v-for="action in history"
              :key="action.id"
              class="rounded-lg border p-3"
              :class="
                action.status === 'PROMESA_INCUMPLIDA'
                  ? 'border-destructive/40 bg-destructive/10'
                  : 'bg-muted/30'
              "
            >
              <div class="flex flex-wrap items-center justify-between gap-2">
                <span class="font-semibold text-foreground">{{ ACTION_LABEL[action.actionType] }}</span>
                <StatusBadge kind="collection" :status="action.status" />
              </div>
              <p class="mt-1 text-foreground">{{ action.note }}</p>
              <p
                v-if="action.status === 'PROMESA_INCUMPLIDA'"
                class="mt-1 font-medium text-destructive"
              >
                Promesa incumplida
                <template v-if="action.promiseAmount">
                  de <MoneyText :amount="action.promiseAmount" />
                </template>
                <template v-if="action.promiseDate">
                  para el {{ formatBusinessDate(action.promiseDate) }}
                </template>
              </p>
              <p v-else-if="action.promiseAmount || action.promiseDate" class="mt-1 text-muted-foreground">
                <template v-if="action.promiseAmount">
                  Monto <MoneyText :amount="action.promiseAmount" />
                </template>
                <template v-if="action.promiseDate">
                  · {{ formatBusinessDate(action.promiseDate) }}
                </template>
              </p>
              <p class="mt-1 text-[11px] text-muted-foreground">
                {{ action.createdBy }} · {{ formatInstant(action.createdAt) }}
              </p>
            </li>
          </ul>
        </div>

        <form class="space-y-3 border-t pt-4" @submit.prevent="submit">
          <p class="font-semibold text-foreground">Nueva gestión</p>
          <Alert v-if="errorMsg" variant="destructive">
            <AlertTriangle class="size-4" />
            <AlertDescription>{{ errorMsg }}</AlertDescription>
          </Alert>

          <div class="grid gap-3 sm:grid-cols-2">
            <div class="space-y-1.5">
              <label for="actionType" class="font-medium">Tipo de contacto</label>
              <select id="actionType" v-model="actionType" :class="selectClass">
                <option v-for="option in ACTION_TYPES" :key="option.value" :value="option.value">
                  {{ option.label }}
                </option>
              </select>
            </div>
            <div class="space-y-1.5">
              <label for="nextActionAt" class="font-medium">Próximo seguimiento</label>
              <Input id="nextActionAt" v-model="nextActionAt" type="datetime-local" />
            </div>
          </div>

          <div v-if="isPromise" class="grid gap-3 sm:grid-cols-2">
            <div class="space-y-1.5">
              <label for="promiseAmount" class="font-medium">Monto prometido (S/)</label>
              <Input id="promiseAmount" v-model="promiseAmount" placeholder="520.00" />
            </div>
            <div class="space-y-1.5">
              <label for="promiseDate" class="font-medium">Fecha de la promesa</label>
              <Input id="promiseDate" v-model="promiseDate" type="date" />
            </div>
          </div>

          <div class="space-y-1.5">
            <div class="flex items-center justify-between">
              <label for="actionNote" class="font-medium">Detalle del contacto</label>
              <div class="flex flex-wrap gap-1">
                <button
                  type="button"
                  class="rounded border bg-muted/40 px-2 py-0.5 text-[10px] text-muted-foreground hover:bg-muted hover:text-foreground"
                  @click="note = 'Cliente no contesta la llamada. Se envía recordatorio por WhatsApp.'"
                >
                  Sin respuesta
                </button>
                <button
                  type="button"
                  class="rounded border bg-muted/40 px-2 py-0.5 text-[10px] text-muted-foreground hover:bg-muted hover:text-foreground"
                  @click="note = 'Cliente manifiesta problemas temporales de liquidez y solicita facilidad.'"
                >
                  Falta liquidez
                </button>
                <button
                  type="button"
                  class="rounded border bg-muted/40 px-2 py-0.5 text-[10px] text-muted-foreground hover:bg-muted hover:text-foreground"
                  @click="note = 'Compromiso de pago acordado con el titular.'"
                >
                  Compromiso
                </button>
              </div>
            </div>
            <textarea
              id="actionNote"
              v-model="note"
              rows="3"
              placeholder="Qué dijo el cliente y qué se acordó"
              class="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            />
          </div>

          <div class="flex justify-end gap-2">
            <Button type="button" variant="ghost" size="sm" :disabled="isBusy" @click="emit('update:open', false)">
              Cerrar
            </Button>
            <Button type="submit" size="sm" :disabled="isBusy || !canSubmit">
              <LoaderCircle v-if="isBusy" class="mr-1.5 size-3.5 animate-spin" />
              Registrar gestión
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  </div>
</template>
