<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ShieldAlert, AlertTriangle, LoaderCircle } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { AgingRow } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { MoneyText, useBusyAction } from '@prestamos/ui'
import { Alert, AlertDescription } from '@prestamos/ui/components/ui/alert'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'

import { useApi } from '@/app/api'
import { useSessionStore } from '@/app/stores/session'
import type { CollectorOption } from './AssignCollectorDialog.vue'

// FE-BO-35: castigo con justificación y aprobación de un segundo usuario.

const selectClass =
  'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring'

const props = defineProps<{
  open: boolean
  row: AgingRow | null
  approvers: CollectorOption[]
}>()

const emit = defineEmits<{
  (e: 'update:open', value: boolean): void
  (e: 'writtenOff'): void
}>()

const session = useSessionStore()
const reason = ref('')
const minuteRef = ref('')
const approvedBy = ref('')
const errorMsg = ref<string | null>(null)

const others = computed(() =>
  props.approvers.filter(
    (person) => person.fullName !== session.user?.fullName && person.email !== session.user?.email,
  ),
)

const canSubmit = computed(
  () => Boolean(reason.value.trim() && minuteRef.value.trim() && approvedBy.value),
)

watch(
  () => props.open,
  (open) => {
    if (!open) return
    reason.value = ''
    minuteRef.value = ''
    approvedBy.value = ''
    errorMsg.value = null
  },
)

const { run: submit, isBusy } = useBusyAction(async () => {
  if (!props.row || !canSubmit.value) return
  errorMsg.value = null
  try {
    await useApi().post(`/loans/${props.row.loanId}/write-off`, {
      reason: reason.value.trim(),
      minuteRef: minuteRef.value.trim(),
      approvedBy: approvedBy.value,
    })
    toast.success('Deuda castigada', {
      description: `${props.row.loanId} pasó a CASTIGADO. El historial del deudor se conserva.`,
    })
    emit('writtenOff')
    emit('update:open', false)
  } catch (error) {
    const apiError = toApiError(error)
    errorMsg.value =
      apiError.status === 409
        ? 'Este préstamo ya se encuentra castigado.'
        : apiError.detail
    toast.error(apiError.title, { description: errorMsg.value })
  }
})
</script>

<template>
  <div
    v-if="open && row"
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
  >
    <Card class="w-full max-w-lg shadow-2xl">
      <CardHeader class="border-b pb-3">
        <div class="flex items-center gap-2 text-destructive">
          <ShieldAlert class="size-5" />
          <CardTitle class="text-lg">Castigo de deuda</CardTitle>
        </div>
        <CardDescription>
          Requiere justificación y el visto bueno de un segundo usuario (acta de comité).
        </CardDescription>
      </CardHeader>
      <form @submit.prevent="submit">
        <CardContent class="space-y-4 pt-4 text-xs">
          <Alert v-if="errorMsg" variant="destructive">
            <AlertTriangle class="size-4" />
            <AlertDescription>{{ errorMsg }}</AlertDescription>
          </Alert>

          <div class="space-y-1.5 rounded-lg border bg-muted/40 p-3">
            <div class="flex justify-between">
              <span class="text-muted-foreground">Cliente</span>
              <span class="font-medium text-foreground">{{ row.customerName }}</span>
            </div>
            <div class="flex justify-between">
              <span class="text-muted-foreground">Préstamo</span>
              <span class="font-mono text-foreground">{{ row.loanId }}</span>
            </div>
            <div class="flex justify-between">
              <span class="text-muted-foreground">Días de atraso</span>
              <span class="font-medium text-foreground">{{ row.daysLate }}</span>
            </div>
            <div class="flex justify-between">
              <span class="text-muted-foreground">Total exigible</span>
              <span class="font-semibold text-foreground"><MoneyText :amount="row.totalDue" /></span>
            </div>
          </div>

          <div class="space-y-1.5">
            <label for="writeOffReason" class="font-semibold">Justificación</label>
            <textarea
              id="writeOffReason"
              v-model="reason"
              rows="3"
              placeholder="Por qué la deuda se considera irrecuperable"
              class="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            />
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <div class="space-y-1.5">
              <label for="minuteRef" class="font-semibold">Acta de comité</label>
              <input
                id="minuteRef"
                v-model="minuteRef"
                placeholder="ACTA-2026-014"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              />
            </div>
            <div class="space-y-1.5">
              <label for="approvedBy" class="font-semibold">Segundo aprobador</label>
              <select id="approvedBy" v-model="approvedBy" :class="selectClass">
                <option value="">Selecciona…</option>
                <option v-for="person in others" :key="person.id" :value="person.fullName">
                  {{ person.fullName }}
                </option>
              </select>
            </div>
          </div>
        </CardContent>
        <div class="flex justify-end gap-2 border-t bg-muted/20 p-4">
          <Button type="button" variant="ghost" size="sm" :disabled="isBusy" @click="emit('update:open', false)">
            Cancelar
          </Button>
          <Button type="submit" variant="destructive" size="sm" :disabled="isBusy || !canSubmit">
            <LoaderCircle v-if="isBusy" class="mr-1.5 size-3.5 animate-spin" />
            Registrar castigo
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
