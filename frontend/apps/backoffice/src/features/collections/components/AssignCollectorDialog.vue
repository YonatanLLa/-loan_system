<script setup lang="ts">
import { ref, watch } from 'vue'
import { UserPlus, AlertTriangle, LoaderCircle } from '@lucide/vue'

import type { AgingRow } from '@prestamos/api-client'
import { Alert, AlertDescription } from '@prestamos/ui/components/ui/alert'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'

import { useAssignCollector } from '../composables'
import type { CollectorOption } from '../services/collections.service'

export type { CollectorOption }

// FE-BO-34: asignar el préstamo a un gestor de cobranza.

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
const { assign, isBusy, error: errorMsg } = useAssignCollector()

watch(
  () => props.open,
  (open) => {
    if (!open) return
    collectorName.value = props.row?.assignedTo ?? ''
    errorMsg.value = null
  },
)

async function submit() {
  if (!props.row || !collectorName.value) return
  const success = await assign(props.row.loanId, collectorName.value, props.row.customerName)
  if (success) {
    emit('assigned')
    emit('update:open', false)
  }
}
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
