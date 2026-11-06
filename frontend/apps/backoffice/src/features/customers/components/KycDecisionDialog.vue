<script setup lang="ts">
import { ref, computed } from 'vue'
import { CheckCircle2, XCircle, AlertTriangle, LoaderCircle } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { KycStatus } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'

import { useApi } from '@/app/api'

// FE-BO-08: Dictamen de expediente KYC: aprobar o rechazar con notas obligatorias si se rechaza.

const props = defineProps<{
  open: boolean
  customerId: string
  customerName: string
  currentStatus: KycStatus
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'decided', status: KycStatus, notes?: string): void
}>()

const decision = ref<'VERIFIED' | 'REJECTED'>('VERIFIED')
const notes = ref('')
const isSubmitting = ref(false)
const errorMsg = ref<string | null>(null)

const isReject = computed(() => decision.value === 'REJECTED')

async function onSubmit() {
  errorMsg.value = null

  if (isReject.value && !notes.value.trim()) {
    errorMsg.value = 'Para rechazar el expediente KYC debes indicar obligatoriamente el motivo.'
    return
  }

  isSubmitting.value = true
  try {
    await useApi().patch(`/customers/${props.customerId}/kyc`, {
      status: decision.value,
      notes: notes.value.trim() || undefined,
    })

    toast.success(
      decision.value === 'VERIFIED' ? 'KYC Aprobado' : 'KYC Rechazado',
      {
        description: `El estado del expediente de ${props.customerName} ha sido actualizado.`,
      },
    )

    emit('decided', decision.value, notes.value)
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
        <div class="flex items-center gap-2">
          <CheckCircle2 v-if="!isReject" class="size-5 text-success" />
          <XCircle v-else class="size-5 text-destructive" />
          <CardTitle class="text-lg">Dictamen de Expediente KYC</CardTitle>
        </div>
        <CardDescription>
          Resolución para el cliente <strong>{{ customerName }}</strong> (FE-BO-08).
        </CardDescription>
      </CardHeader>

      <form @submit.prevent="onSubmit">
        <CardContent class="space-y-4">
          <div class="flex gap-2 p-1 bg-muted rounded-lg">
            <button
              type="button"
              class="flex-1 py-1.5 px-3 rounded-md text-xs font-semibold transition-all flex items-center justify-center gap-1.5"
              :class="
                decision === 'VERIFIED'
                  ? 'bg-background shadow-xs text-foreground'
                  : 'text-muted-foreground hover:text-foreground'
              "
              @click="decision = 'VERIFIED'"
            >
              <CheckCircle2 class="size-3.5 text-success" />
              Aprobar KYC
            </button>
            <button
              type="button"
              class="flex-1 py-1.5 px-3 rounded-md text-xs font-semibold transition-all flex items-center justify-center gap-1.5"
              :class="
                decision === 'REJECTED'
                  ? 'bg-background shadow-xs text-destructive'
                  : 'text-muted-foreground hover:text-foreground'
              "
              @click="decision = 'REJECTED'"
            >
              <XCircle class="size-3.5 text-destructive" />
              Rechazar KYC
            </button>
          </div>

          <div v-if="errorMsg" class="p-3 bg-destructive/10 text-destructive text-xs rounded-md flex items-start gap-2">
            <AlertTriangle class="size-4 shrink-0 mt-0.5" />
            <span>{{ errorMsg }}</span>
          </div>

          <div class="space-y-1.5">
            <label for="kycNotes" class="text-xs font-medium">
              {{ isReject ? 'Motivo del Rechazo (Obligatorio)' : 'Observaciones / Notas (Opcional)' }}
            </label>
            <textarea
              id="kycNotes"
              v-model="notes"
              rows="3"
              placeholder="Ej. Documento nítido verificado con Reniec / Selfie ilegible..."
              class="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              :class="{ 'border-destructive': isReject && !notes.trim() }"
            ></textarea>
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
            :variant="isReject ? 'destructive' : 'default'"
            :disabled="isSubmitting || (isReject && !notes.trim())"
          >
            <LoaderCircle v-if="isSubmitting" class="size-3.5 mr-1.5 animate-spin" />
            {{ isReject ? 'Confirmar Rechazo' : 'Aprobar Expediente' }}
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
