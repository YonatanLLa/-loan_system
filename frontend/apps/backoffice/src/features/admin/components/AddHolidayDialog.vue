<script setup lang="ts">
import { ref, watch } from 'vue'
import { CalendarDays, LoaderCircle } from '@lucide/vue'

import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Input } from '@prestamos/ui/components/ui/input'
import { Label } from '@prestamos/ui/components/ui/label'

import type { CreateHolidayPayload } from '../services/admin.service'

const props = defineProps<{
  open: boolean
  isBusy: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'submit', payload: CreateHolidayPayload): void
}>()

const date = ref('')
const name = ref('')
const isRecurring = ref(false)

watch(
  () => props.open,
  (val) => {
    if (val) {
      date.value = ''
      name.value = ''
      isRecurring.value = false
    }
  },
)

function handleSubmit() {
  if (!date.value || !name.value.trim()) return
  emit('submit', {
    date: date.value,
    name: name.value.trim(),
    isRecurring: isRecurring.value,
  })
}
</script>

<template>
  <div
    v-if="open"
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
  >
    <Card class="w-full max-w-md shadow-2xl">
      <CardHeader class="border-b pb-3">
        <div class="flex items-center gap-2 text-primary">
          <CalendarDays class="size-5" />
          <CardTitle class="text-lg">Registrar Feriado Nacional</CardTitle>
        </div>
        <CardDescription class="text-xs">
          Los feriados se toman en cuenta automáticamente para el cálculo de fechas de vencimiento sin mora.
        </CardDescription>
      </CardHeader>

      <form @submit.prevent="handleSubmit">
        <CardContent class="space-y-4 pt-4 text-xs">
          <div class="space-y-1.5">
            <Label for="holidayDate" class="font-medium">Fecha del feriado</Label>
            <Input id="holidayDate" v-model="date" type="date" required class="h-9 text-xs" />
          </div>

          <div class="space-y-1.5">
            <Label for="holidayName" class="font-medium">Nombre de la festividad / efeméride</Label>
            <Input
              id="holidayName"
              v-model="name"
              placeholder="Ej. Fiestas Patrias, Año Nuevo, Santa Rosa de Lima"
              required
              class="h-9 text-xs"
            />
          </div>

          <div class="flex items-center gap-2 pt-1">
            <input
              id="isRecurring"
              v-model="isRecurring"
              type="checkbox"
              class="size-4 rounded border-input text-primary focus:ring-primary"
            />
            <Label for="isRecurring" class="font-normal text-xs cursor-pointer">
              Feriado recurrente (se repite anualmente)
            </Label>
          </div>
        </CardContent>

        <div class="flex justify-end gap-2 border-t bg-muted/20 p-4">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            :disabled="isBusy"
            @click="emit('update:open', false)"
          >
            Cancelar
          </Button>
          <Button type="submit" size="sm" :disabled="isBusy || !date || !name.trim()">
            <LoaderCircle v-if="isBusy" class="mr-1.5 size-3.5 animate-spin" />
            Guardar Feriado
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
