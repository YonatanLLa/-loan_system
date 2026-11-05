<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import {
  FileText,
  Filter,
  RefreshCw,
  Search,
  ShieldCheck,
  ChevronLeft,
  ChevronRight,
  Eye,
  X,
  ArrowRight,
} from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { AuditLogEntry, Page } from '@prestamos/api-client'
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
import { Input } from '@prestamos/ui/components/ui/input'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@prestamos/ui/components/ui/sheet'
import { formatInstant } from '@prestamos/utils'

import { useApi } from '@/app/api'

// FE-BO-05: Bitácora de auditoría con filtros por entidad, fechas, usuario y acción, y vista "antes / después" del JSON.

const loading = ref(false)
const auditPage = ref<Page<AuditLogEntry>>({
  items: [],
  total: 0,
  page: 1,
  pageSize: 15,
})

// Opciones dinámicas de filtros
const availableEntities = ref<string[]>([])
const availableActions = ref<string[]>([])

// Filtros reactivos
const filters = ref({
  entity: '',
  action: '',
  user: '',
  from: '',
  to: '',
  page: 1,
})

// Registro seleccionado para ver detalle "antes / después"
const selectedEntry = ref<AuditLogEntry | null>(null)
const isDetailOpen = ref(false)

function openDetail(entry: AuditLogEntry) {
  selectedEntry.value = entry
  isDetailOpen.value = true
}

function closeDetail() {
  isDetailOpen.value = false
  selectedEntry.value = null
}

async function loadCatalogs() {
  try {
    const [entitiesRes, actionsRes] = await Promise.all([
      useApi().get<string[]>('/audit/entities'),
      useApi().get<string[]>('/audit/actions'),
    ])
    availableEntities.value = entitiesRes.data
    availableActions.value = actionsRes.data
  } catch {
    // Si falla cargar catálogos dinámicos se mantiene la lista vacía para entrada libre
  }
}

async function loadAuditLogs() {
  loading.value = true
  try {
    const params: Record<string, string | number> = {
      page: filters.value.page,
      pageSize: auditPage.value.pageSize,
    }
    if (filters.value.entity) params.entity = filters.value.entity
    if (filters.value.action) params.action = filters.value.action
    if (filters.value.user.trim()) params.user = filters.value.user.trim()
    if (filters.value.from) params.from = filters.value.from
    if (filters.value.to) params.to = filters.value.to

    const { data } = await useApi().get<Page<AuditLogEntry>>('/audit', { params })
    auditPage.value = data
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar bitácora', { description: apiError.detail })
  } finally {
    loading.value = false
  }
}

function onSearch() {
  filters.value.page = 1
  void loadAuditLogs()
}

function resetFilters() {
  filters.value = {
    entity: '',
    action: '',
    user: '',
    from: '',
    to: '',
    page: 1,
  }
  void loadAuditLogs()
}

function goToPage(p: number) {
  if (p < 1 || p > totalPages.value) return
  filters.value.page = p
  void loadAuditLogs()
}

const totalPages = ref(1)
watch(
  () => auditPage.value.total,
  (total) => {
    totalPages.value = Math.max(1, Math.ceil(total / auditPage.value.pageSize))
  },
  { immediate: true },
)

function getActionBadgeVariant(action: string) {
  switch (action.toUpperCase()) {
    case 'CREATE':
    case 'REGISTER':
      return 'border-success/30 bg-success/10 text-success'
    case 'UPDATE':
    case 'EDIT':
      return 'border-warning/30 bg-warning/10 text-warning'
    case 'DELETE':
    case 'REJECT':
    case 'REVERSE':
      return 'border-destructive/30 bg-destructive/10 text-destructive'
    case 'LOGIN':
      return 'border-info/30 bg-info/10 text-info'
    default:
      return 'border-border bg-muted text-muted-foreground'
  }
}

function formatJson(val: unknown): string {
  if (val === null || val === undefined) return 'Sin datos'
  try {
    return JSON.stringify(val, null, 2)
  } catch {
    return String(val)
  }
}

onMounted(async () => {
  await loadCatalogs()
  await loadAuditLogs()
})
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Bitácora de auditoría"
      description="Registro inmutable de trazabilidad de acciones, cambios de estado y accesos al sistema (FE-BO-05)."
    >
      <template #actions>
        <Button variant="outline" size="sm" :disabled="loading" @click="loadAuditLogs">
          <RefreshCw class="size-4 mr-2" :class="{ 'animate-spin': loading }" />
          Actualizar
        </Button>
      </template>
    </PageHeader>

    <!-- ── Filtros de búsqueda ── -->
    <Card>
      <CardHeader class="pb-3">
        <CardTitle class="text-base flex items-center gap-2">
          <Filter class="size-4 text-muted-foreground" />
          Filtros de auditoría
        </CardTitle>
        <CardDescription>
          Filtra por entidad modificada, tipo de acción, usuario responsable o rango de fechas.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form class="grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-5" @submit.prevent="onSearch">
          <!-- Entidad -->
          <div class="space-y-1.5">
            <label for="entity" class="text-xs font-medium text-muted-foreground">Entidad</label>
            <select
              id="entity"
              v-model="filters.entity"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs transition-colors focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="">Todas las entidades</option>
              <option v-for="ent in availableEntities" :key="ent" :value="ent">
                {{ ent }}
              </option>
            </select>
          </div>

          <!-- Acción -->
          <div class="space-y-1.5">
            <label for="action" class="text-xs font-medium text-muted-foreground">Acción</label>
            <select
              id="action"
              v-model="filters.action"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs transition-colors focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="">Todas las acciones</option>
              <option v-for="act in availableActions" :key="act" :value="act">
                {{ act }}
              </option>
            </select>
          </div>

          <!-- Usuario -->
          <div class="space-y-1.5">
            <label for="user" class="text-xs font-medium text-muted-foreground">Usuario / IP</label>
            <Input
              id="user"
              v-model="filters.user"
              placeholder="Nombre o correo..."
              class="h-9"
            />
          </div>

          <!-- Fecha Desde -->
          <div class="space-y-1.5">
            <label for="from" class="text-xs font-medium text-muted-foreground">Desde</label>
            <Input
              id="from"
              v-model="filters.from"
              type="date"
              class="h-9"
            />
          </div>

          <!-- Fecha Hasta -->
          <div class="space-y-1.5">
            <label for="to" class="text-xs font-medium text-muted-foreground">Hasta</label>
            <Input
              id="to"
              v-model="filters.to"
              type="date"
              class="h-9"
            />
          </div>

          <!-- Botones de acción -->
          <div class="flex items-center gap-2 sm:col-span-2 md:col-span-3 lg:col-span-5 justify-end pt-1">
            <Button type="button" variant="ghost" size="sm" @click="resetFilters">
              <X class="size-4 mr-1" />
              Limpiar
            </Button>
            <Button type="submit" size="sm" :disabled="loading">
              <Search class="size-4 mr-1" />
              Filtrar registros
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>

    <!-- ── Tabla de resultados ── -->
    <Card>
      <CardContent class="p-0">
        <!-- Estado de carga -->
        <div v-if="loading" class="p-6 space-y-4">
          <Skeleton class="h-10 w-full" />
          <Skeleton class="h-12 w-full" />
          <Skeleton class="h-12 w-full" />
          <Skeleton class="h-12 w-full" />
        </div>

        <!-- Sin resultados -->
        <div v-else-if="auditPage.items.length === 0" class="p-12">
          <EmptyState
            :icon="ShieldCheck"
            title="Sin eventos de auditoría"
            description="No se encontraron registros que coincidan con los criterios de búsqueda seleccionados."
          >
            <Button variant="outline" size="sm" @click="resetFilters">
              Restablecer filtros
            </Button>
          </EmptyState>
        </div>

        <!-- Lista de registros -->
        <div v-else class="overflow-x-auto">
          <table class="w-full text-sm text-left">
            <thead class="border-b bg-muted/50 text-xs font-medium text-muted-foreground">
              <tr>
                <th class="px-4 py-3">ID</th>
                <th class="px-4 py-3">Fecha y Hora</th>
                <th class="px-4 py-3">Usuario / IP</th>
                <th class="px-4 py-3">Entidad</th>
                <th class="px-4 py-3">ID Entidad</th>
                <th class="px-4 py-3">Acción</th>
                <th class="px-4 py-3 text-right">Comparar</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-border">
              <tr
                v-for="entry in auditPage.items"
                :key="entry.id"
                class="hover:bg-muted/40 transition-colors"
              >
                <td class="px-4 py-3 font-mono text-xs text-muted-foreground">
                  #{{ entry.id }}
                </td>
                <td class="px-4 py-3 whitespace-nowrap text-xs">
                  {{ formatInstant(entry.createdAt) }}
                </td>
                <td class="px-4 py-3">
                  <div class="font-medium text-foreground leading-tight">{{ entry.userName }}</div>
                  <div class="text-xs font-mono text-muted-foreground">{{ entry.userIp }}</div>
                </td>
                <td class="px-4 py-3 font-medium">
                  <Badge variant="outline" class="font-mono text-xs">
                    {{ entry.entityName }}
                  </Badge>
                </td>
                <td class="px-4 py-3 font-mono text-xs text-muted-foreground">
                  {{ entry.entityId }}
                </td>
                <td class="px-4 py-3">
                  <span
                    class="inline-flex items-center rounded-md border px-2 py-0.5 text-xs font-semibold"
                    :class="getActionBadgeVariant(entry.action)"
                  >
                    {{ entry.action }}
                  </span>
                </td>
                <td class="px-4 py-3 text-right">
                  <Button
                    variant="ghost"
                    size="sm"
                    class="h-8 gap-1.5 text-xs"
                    @click="openDetail(entry)"
                  >
                    <Eye class="size-3.5" />
                    <span>Ver cambio</span>
                  </Button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- ── Paginación ── -->
        <div
          v-if="auditPage.total > 0"
          class="flex flex-col sm:flex-row items-center justify-between gap-4 border-t p-4 text-xs text-muted-foreground"
        >
          <div>
            Mostrando
            <span class="font-medium text-foreground">
              {{ (auditPage.page - 1) * auditPage.pageSize + 1 }}
            </span>
            a
            <span class="font-medium text-foreground">
              {{ Math.min(auditPage.page * auditPage.pageSize, auditPage.total) }}
            </span>
            de
            <span class="font-medium text-foreground">{{ auditPage.total }}</span>
            eventos
          </div>

          <div class="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              class="h-8"
              :disabled="auditPage.page <= 1 || loading"
              @click="goToPage(auditPage.page - 1)"
            >
              <ChevronLeft class="size-4 mr-1" />
              Anterior
            </Button>
            <span class="px-2 font-medium text-foreground">
              Página {{ auditPage.page }} de {{ totalPages }}
            </span>
            <Button
              variant="outline"
              size="sm"
              class="h-8"
              :disabled="auditPage.page >= totalPages || loading"
              @click="goToPage(auditPage.page + 1)"
            >
              Siguiente
              <ChevronRight class="size-4 ml-1" />
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- ── Sheet de Vista "Antes / Después" (JSON) ── -->
    <Sheet :open="isDetailOpen" @update:open="(val) => (val ? null : closeDetail())">
      <SheetContent side="right" class="w-full sm:max-w-2xl overflow-y-auto">
        <SheetHeader class="border-b pb-4">
          <div class="flex items-center gap-2">
            <span
              class="inline-flex items-center rounded-md border px-2 py-0.5 text-xs font-semibold"
              :class="getActionBadgeVariant(selectedEntry?.action ?? '')"
            >
              {{ selectedEntry?.action }}
            </span>
            <SheetTitle class="text-base font-semibold">
              Cambio en {{ selectedEntry?.entityName }} #{{ selectedEntry?.entityId }}
            </SheetTitle>
          </div>
          <SheetDescription class="text-xs">
            Registrado por
            <strong class="text-foreground">{{ selectedEntry?.userName }}</strong>
            el {{ selectedEntry ? formatInstant(selectedEntry.createdAt) : '' }} desde la IP {{ selectedEntry?.userIp }}.
          </SheetDescription>
        </SheetHeader>

        <div v-if="selectedEntry" class="space-y-6 pt-4">
          <!-- Metadatos rápidos -->
          <div class="grid grid-cols-2 gap-3 text-xs rounded-lg border p-3 bg-muted/30">
            <div>
              <span class="text-muted-foreground block">Entidad afectada:</span>
              <span class="font-mono font-medium">{{ selectedEntry.entityName }}</span>
            </div>
            <div>
              <span class="text-muted-foreground block">Identificador (ID):</span>
              <span class="font-mono font-medium">{{ selectedEntry.entityId }}</span>
            </div>
            <div>
              <span class="text-muted-foreground block">Operación:</span>
              <span class="font-medium">{{ selectedEntry.action }}</span>
            </div>
            <div>
              <span class="text-muted-foreground block">ID Registro Auditoría:</span>
              <span class="font-mono font-medium">#{{ selectedEntry.id }}</span>
            </div>
          </div>

          <!-- Comparativa Antes vs Después -->
          <div class="space-y-4">
            <h4 class="text-sm font-semibold flex items-center gap-2">
              <FileText class="size-4 text-primary" />
              Comparativa de Estado (JSON)
            </h4>

            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <!-- Estado Antes -->
              <div class="space-y-2 rounded-lg border border-border/80 bg-background p-3">
                <div class="flex items-center justify-between">
                  <span class="text-xs font-semibold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
                    <span class="size-2 rounded-full bg-destructive"></span>
                    Estado Anterior
                  </span>
                  <Badge v-if="!selectedEntry.stateBefore" variant="outline" class="text-[10px]">
                    Sin estado previo (Creación)
                  </Badge>
                </div>
                <pre class="max-h-80 overflow-auto rounded bg-muted/60 p-2.5 font-mono text-[11px] leading-relaxed text-foreground/90 whitespace-pre-wrap">{{ formatJson(selectedEntry.stateBefore) }}</pre>
              </div>

              <!-- Estado Después -->
              <div class="space-y-2 rounded-lg border border-border/80 bg-background p-3">
                <div class="flex items-center justify-between">
                  <span class="text-xs font-semibold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
                    <span class="size-2 rounded-full bg-success"></span>
                    Estado Posterior
                  </span>
                  <Badge v-if="!selectedEntry.stateAfter" variant="outline" class="text-[10px]">
                    Sin estado posterior (Eliminación)
                  </Badge>
                </div>
                <pre class="max-h-80 overflow-auto rounded bg-muted/60 p-2.5 font-mono text-[11px] leading-relaxed text-foreground/90 whitespace-pre-wrap">{{ formatJson(selectedEntry.stateAfter) }}</pre>
              </div>
            </div>
          </div>

          <div class="pt-4 border-t flex justify-end">
            <Button variant="outline" size="sm" @click="closeDetail">
              Cerrar visor
            </Button>
          </div>
        </div>
      </SheetContent>
    </Sheet>
  </div>
</template>
