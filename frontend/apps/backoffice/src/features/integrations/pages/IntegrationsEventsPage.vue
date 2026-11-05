<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import {
  Plug,
  RefreshCw,
  RotateCcw,
  AlertTriangle,
  CheckCircle2,
  Clock,
  Layers,
  Search,
  X,
  ChevronLeft,
  ChevronRight,
  ShieldAlert,
} from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { IntegrationEvent, Page } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { EmptyState, PageHeader } from '@prestamos/ui'
import { Badge } from '@prestamos/ui/components/ui/badge'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import { formatInstant } from '@prestamos/utils'

import { useApi } from '@/app/api'

// FE-BO-31: Panel de eventos de integración en cola de mensajes fallidos (DLQ) con reintento manual.

const loading = ref(false)
const eventsPage = ref<Page<IntegrationEvent>>({
  items: [],
  total: 0,
  page: 1,
  pageSize: 10,
})

const selectedStatus = ref('')
const selectedProvider = ref('')
const currentPage = ref(1)
const retryingId = ref<string | null>(null)

async function loadEvents() {
  loading.value = true
  try {
    const params: Record<string, string | number> = {
      page: currentPage.value,
      pageSize: eventsPage.value.pageSize,
    }
    if (selectedStatus.value) params.status = selectedStatus.value

    const { data } = await useApi().get<Page<IntegrationEvent>>('/integrations/events', { params })
    eventsPage.value = data
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar eventos de integración', { description: apiError.detail })
  } finally {
    loading.value = false
  }
}

async function retryEvent(event: IntegrationEvent) {
  retryingId.value = event.id
  try {
    const { data } = await useApi().post<IntegrationEvent>(
      `/integrations/events/${event.id}/retry`,
      {},
    )
    toast.success('Evento reintentado', {
      description: `El evento ${data.externalId} fue reprocesado con estado ${data.status}.`,
    })
    void loadEvents()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al reintentar evento', { description: apiError.detail })
  } finally {
    retryingId.value = null
  }
}

function getStatusBadge(status: string) {
  switch (status) {
    case 'PROCESSED':
      return { label: 'Procesado', class: 'border-success text-success bg-success/10' }
    case 'RECEIVED':
      return { label: 'Recibido', class: 'border-info text-info bg-info/10' }
    case 'FAILED':
      return { label: 'Fallido', class: 'border-warning text-warning bg-warning/10' }
    case 'DEAD_LETTER':
      return { label: 'Dead Letter (DLQ)', class: 'border-destructive text-destructive bg-destructive/10' }
    default:
      return { label: status, class: 'border-border text-muted-foreground' }
  }
}

function goToPage(p: number) {
  if (p < 1 || p > totalPages.value) return
  currentPage.value = p
  void loadEvents()
}

const totalPages = ref(1)
watch(
  () => eventsPage.value.total,
  (total) => {
    totalPages.value = Math.max(1, Math.ceil(total / eventsPage.value.pageSize))
  },
  { immediate: true },
)

onMounted(() => {
  void loadEvents()
})
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Integraciones y Eventos en DLQ"
      description="Monitoreo de webhooks, colas de mensajes muertas (Dead Letter Queue) y reintento manual de transacciones fallidas (FE-BO-31)."
    >
      <template #actions>
        <Button variant="outline" size="sm" :disabled="loading" @click="loadEvents">
          <RefreshCw class="size-3.5 mr-1.5" :class="{ 'animate-spin': loading }" />
          Actualizar
        </Button>
      </template>
    </PageHeader>

    <!-- ── Filtros ── -->
    <Card>
      <CardContent class="p-4">
        <div class="flex flex-col sm:flex-row items-center gap-3">
          <div class="w-full sm:w-60">
            <label class="text-[11px] font-medium text-muted-foreground block mb-1">Estado del Evento</label>
            <select
              v-model="selectedStatus"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              @change="() => { currentPage = 1; loadEvents(); }"
            >
              <option value="">Todos los estados</option>
              <option value="DEAD_LETTER">Solo Dead Letter (DLQ)</option>
              <option value="FAILED">Solo Fallidos</option>
              <option value="PROCESSED">Solo Procesados</option>
              <option value="RECEIVED">Solo Recibidos</option>
            </select>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- ── Tabla de Eventos ── -->
    <Card>
      <CardContent class="p-0">
        <div v-if="loading" class="p-6 space-y-4">
          <Skeleton class="h-10 w-full" />
          <Skeleton class="h-12 w-full" />
          <Skeleton class="h-12 w-full" />
        </div>

        <div v-else-if="eventsPage.items.length === 0" class="p-12">
          <EmptyState
            :icon="CheckCircle2"
            title="Sin eventos en DLQ"
            description="Todas las integraciones con pasarelas y bancos operan de forma estable sin mensajes en cola de error."
          />
        </div>

        <div v-else class="overflow-x-auto">
          <table class="w-full text-sm text-left">
            <thead class="border-b bg-muted/60 text-xs font-medium text-muted-foreground">
              <tr>
                <th class="px-4 py-3">ID Evento</th>
                <th class="px-4 py-3">Proveedor</th>
                <th class="px-4 py-3">Tipo de Evento</th>
                <th class="px-4 py-3">External ID</th>
                <th class="px-4 py-3">Reintentos</th>
                <th class="px-4 py-3">Fecha</th>
                <th class="px-4 py-3">Error / Causa en DLQ</th>
                <th class="px-4 py-3">Estado</th>
                <th class="px-4 py-3 text-right">Acción</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-border text-xs">
              <tr
                v-for="ev in eventsPage.items"
                :key="ev.id"
                class="hover:bg-muted/40 transition-colors"
                :class="{ 'bg-destructive/5': ev.status === 'DEAD_LETTER' }"
              >
                <td class="px-4 py-3 font-mono font-bold">{{ ev.id }}</td>
                <td class="px-4 py-3 font-bold uppercase text-foreground">
                  <Badge variant="outline" class="font-mono text-[10px]">
                    {{ ev.provider }}
                  </Badge>
                </td>
                <td class="px-4 py-3 font-mono text-muted-foreground">
                  {{ ev.eventType }}
                </td>
                <td class="px-4 py-3 font-mono text-foreground font-semibold">
                  {{ ev.externalId }}
                </td>
                <td class="px-4 py-3 font-mono">
                  <span :class="ev.retryCount > 0 ? 'text-warning font-bold' : 'text-muted-foreground'">
                    {{ ev.retryCount }} veces
                  </span>
                </td>
                <td class="px-4 py-3 whitespace-nowrap text-muted-foreground">
                  {{ formatInstant(ev.createdAt) }}
                </td>

                <!-- Detalle del Error (FE-BO-31) -->
                <td class="px-4 py-3 max-w-xs">
                  <div v-if="ev.errorDetail" class="text-destructive font-medium truncate" :title="ev.errorDetail">
                    {{ ev.errorDetail }}
                  </div>
                  <span v-else class="text-muted-foreground italic">Sin errores reportados</span>
                </td>

                <td class="px-4 py-3">
                  <Badge
                    variant="outline"
                    class="text-[10px]"
                    :class="getStatusBadge(ev.status).class"
                  >
                    {{ getStatusBadge(ev.status).label }}
                  </Badge>
                </td>

                <td class="px-4 py-3 text-right">
                  <!-- Botón Reintentar (FE-BO-31) -->
                  <Button
                    v-if="ev.status === 'DEAD_LETTER' || ev.status === 'FAILED'"
                    size="sm"
                    variant="outline"
                    class="h-8 gap-1 text-xs"
                    :disabled="retryingId === ev.id"
                    @click="retryEvent(ev)"
                  >
                    <RotateCcw class="size-3.5" />
                    <span>Reintentar</span>
                  </Button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Paginación -->
        <div
          v-if="eventsPage.total > 0"
          class="flex flex-col sm:flex-row items-center justify-between gap-4 border-t p-4 text-xs text-muted-foreground"
        >
          <div>
            Mostrando
            <span class="font-medium text-foreground">
              {{ (eventsPage.page - 1) * eventsPage.pageSize + 1 }}
            </span>
            a
            <span class="font-medium text-foreground">
              {{ Math.min(eventsPage.page * eventsPage.pageSize, eventsPage.total) }}
            </span>
            de
            <span class="font-medium text-foreground">{{ eventsPage.total }}</span>
            eventos
          </div>

          <div class="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              class="h-8"
              :disabled="eventsPage.page <= 1 || loading"
              @click="goToPage(eventsPage.page - 1)"
            >
              <ChevronLeft class="size-4 mr-1" />
              Anterior
            </Button>
            <span class="px-2 font-medium text-foreground">
              Página {{ eventsPage.page }} de {{ totalPages }}
            </span>
            <Button
              variant="outline"
              size="sm"
              class="h-8"
              :disabled="eventsPage.page >= totalPages || loading"
              @click="goToPage(eventsPage.page + 1)"
            >
              Siguiente
              <ChevronRight class="size-4 ml-1" />
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
