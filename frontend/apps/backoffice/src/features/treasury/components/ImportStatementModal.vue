<script setup lang="ts">
import { ref } from 'vue'
import { UploadCloud, CheckCircle2, Clock, AlertTriangle, LoaderCircle, FileText } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { CompanyBankAccount } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { useBusyAction } from '@prestamos/ui'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Alert, AlertDescription, AlertTitle } from '@prestamos/ui/components/ui/alert'

import { useTreasury } from '../composables/useTreasury'

// FE-BO-29: Importar extractos bancarios con conciliación y match automático.

const props = defineProps<{
  open: boolean
  companyAccounts: CompanyBankAccount[]
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'imported'): void
}>()

const { importStatement } = useTreasury()

const selectedAccountId = ref(props.companyAccounts[0]?.id ?? 'bca-001')
const rawLines = ref(
  `EXT-920010, 2026-11-05, 520.00, ABONO TRANSFERENCIA OP-445566
EXT-920011, 2026-11-05, 300.00, YAPE PAGO CUOTA MARIA QUISPE
EXT-920012, 2026-11-04, 150.00, ABONO VENTANILLA SIN IDENTIFICAR`,
)

const importResult = ref<{
  imported: number
  matched: number
  pending: number
  duplicated: number
} | null>(null)

const { run: submitImport, isBusy } = useBusyAction(async () => {
  importResult.value = null
  try {
    // Parsear líneas simples de texto / CSV
    const parsedLines = rawLines.value
      .split('\n')
      .map((l) => l.trim())
      .filter(Boolean)
      .map((line, idx) => {
        const parts = line.split(',').map((p) => p.trim())
        return {
          operationNumber: parts[0] || `EXT-${Date.now() + idx}`,
          valueDate: parts[1] || new Date().toISOString().slice(0, 10),
          amount: parts[2] || '100.00',
          description: parts[3] || 'ABONO BANCARIO',
        }
      })

    const data = await importStatement({
      bankAccountId: selectedAccountId.value,
      lines: parsedLines,
    })

    importResult.value = data
    toast.success('Extracto importado con éxito', {
      description: `Se procesaron ${data.imported} movimientos (${data.matched} conciliados automáticamente).`,
    })
    emit('imported')
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al importar extracto', { description: apiError.detail })
  }
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
          <UploadCloud class="size-5" />
          <CardTitle class="text-lg">Importar Extracto Bancario</CardTitle>
        </div>
        <CardDescription>
          Carga masiva de movimientos bancarios con algoritmo de emparejamiento automático (FE-BO-29).
        </CardDescription>
      </CardHeader>

      <div class="p-6 space-y-4 text-xs">
        <!-- Cuenta Receptora -->
        <div class="space-y-1.5">
          <label for="acc" class="text-xs font-medium">Cuenta Bancaria del Extracto</label>
          <select
            id="acc"
            v-model="selectedAccountId"
            class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
          >
            <option v-for="a in companyAccounts" :key="a.id" :value="a.id">
              {{ a.bankName }} - {{ a.accountNumberMasked }} ({{ a.currency }})
            </option>
          </select>
        </div>

        <!-- Editor / Área de texto de extracto -->
        <div class="space-y-1.5">
          <div class="flex justify-between items-center">
            <label for="raw" class="text-xs font-medium">Líneas del Extracto (CSV / Texto plano)</label>
            <span class="text-[11px] text-muted-foreground">Formato: Ref, Fecha, Monto, Descripción</span>
          </div>
          <textarea
            id="raw"
            v-model="rawLines"
            rows="5"
            class="flex w-full rounded-md border border-input bg-transparent p-2.5 font-mono text-xs leading-relaxed shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
          ></textarea>
        </div>

        <!-- Resultado de la importación y Match Automático (FE-BO-29) -->
        <div v-if="importResult" class="rounded-xl border bg-muted/40 p-4 space-y-3">
          <h4 class="text-xs font-bold text-foreground flex items-center gap-1.5">
            <CheckCircle2 class="size-4 text-success" />
            Resultado del Match Automático (FE-BO-29)
          </h4>

          <div class="grid grid-cols-2 sm:grid-cols-4 gap-2 text-center text-xs">
            <div class="rounded-lg border bg-card p-2">
              <span class="text-muted-foreground block text-[10px]">Importados:</span>
              <span class="font-bold text-foreground text-sm">{{ importResult.imported }}</span>
            </div>
            <div class="rounded-lg border bg-success/10 border-success/30 p-2">
              <span class="text-success block text-[10px] font-semibold">Match Auto:</span>
              <span class="font-bold text-success text-sm">{{ importResult.matched }}</span>
            </div>
            <div class="rounded-lg border bg-warning/10 border-warning/30 p-2">
              <span class="text-warning block text-[10px] font-semibold">Por Conciliar:</span>
              <span class="font-bold text-warning text-sm">{{ importResult.pending }}</span>
            </div>
            <div class="rounded-lg border bg-muted p-2">
              <span class="text-muted-foreground block text-[10px]">Duplicados:</span>
              <span class="font-medium text-foreground text-sm">{{ importResult.duplicated }}</span>
            </div>
          </div>
        </div>
      </div>

      <div class="flex items-center justify-end gap-2 p-4 border-t bg-muted/20">
        <Button
          type="button"
          variant="ghost"
          size="sm"
          :disabled="isBusy"
          @click="emit('update:open', false)"
        >
          Cerrar
        </Button>
        <Button
          type="button"
          size="sm"
          :disabled="isBusy || !rawLines.trim()"
          @click="submitImport"
        >
          <LoaderCircle v-if="isBusy" class="size-3.5 mr-1.5 animate-spin" />
          <UploadCloud v-else class="size-3.5 mr-1.5" />
          Ejecutar Importación y Match
        </Button>
      </div>
    </Card>
  </div>
</template>
