<script setup lang="ts">
import { ref, watch } from 'vue'
import { UserPlus, AlertTriangle, LoaderCircle } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { AgingRow } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { useBusyAction } from '@prestamos/ui'
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

// FE-BO-34: asignar el préstamo a un gestor de cobranza.

export interface CollectorOption {
  id: string
  fullName: string
  email: string
  role: string
}

const selectClass =
  'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring'

const props = defineProps<{
  open: boolean
  row: AgingRow | null
  collectors: CollectorOption[]
}>()

const emit = defineEmits<{
  (e: 'update:open', value: boolean): void
  (e: 'assigned'): void
}>()

const collectorName = ref('')
const errorMsg = ref<string | null>(null)

watch(
  () => props.open,
  (open) => {
    if (!open) return
    collectorName.value = props.row?.assignedTo ?? ''
    errorMsg.value = null
  },
)

const { run: submit, isBusy } = useBusyAction(async () => {
  if (!props.row || !collectorName.value) return
  errorMsg.value = null
  try {
    await useApi().post(`/loans/${props.row.loanId}/assign-collector`, {
      collectorName: collectorName.value,
    })
    toast.success('Gestor asignado', {
      description: `${props.row.customerName} quedó a cargo de ${collectorName.value}.`,
    })
    emit('assigned')
    emit('update:open', false)
  } catch (error) {
    errorMsg.value = toApiError(error).detail
    toast.error('No se pudo asignar', { description: errorMsg.value })
  }
})
</script>

<template>
  <div
    v-if="open && row"
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
  >
    <Card class="w-full max-w-md shadow-2xl">
      <CardHeader class="border-b pb-3">
        <div class="flex items-center gap-2 text-primary">
          <UserPlus class="size-5" />
          <CardTitle class="text-lg">Asignar gestor</CardTitle>
        </div>
        <CardDescription>
          {{ row.customerName }} · {{ row.loanId }}
        </CardDescription>
      </CardHeader>
      <form @submit.prevent="submit">
        <CardContent class="space-y-4 pt-4 text-xs">
          <Alert v-if="errorMsg" variant="destructive">
            <AlertTriangle class="size-4" />
            <AlertDescription>{{ errorMsg }}</AlertDescription>
          </Alert>
          <div class="space-y-1.5">
            <label for="collector" class="font-medium">Gestor de cobranza</label>
            <select id="collector" v-model="collectorName" :class="selectClass">
              <option value="">Selecciona un gestor…</option>
              <option v-for="person in collectors" :key="person.id" :value="person.fullName">
                {{ person.fullName }}
              </option>
            </select>
          </div>
        </CardContent>
        <div class="flex justify-end gap-2 border-t bg-muted/20 p-4">
          <Button type="button" variant="ghost" size="sm" :disabled="isBusy" @click="emit('update:open', false)">
            Cancelar
          </Button>
          <Button type="submit" size="sm" :disabled="isBusy || !collectorName">
            <LoaderCircle v-if="isBusy" class="mr-1.5 size-3.5 animate-spin" />
            Asignar
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
