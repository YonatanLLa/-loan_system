<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import {
  ChevronLeft,
  ChevronRight,
  ExternalLink,
  Eye,
  FileText,
  Filter,
  RefreshCw,
  Search,
  ShieldCheck,
  X,
} from '@lucide/vue'

import type { AuditLogEntry } from '@prestamos/api-client'
import { EmptyState, PageHeader, TableGenerator } from '@prestamos/ui'
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
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@prestamos/ui/components/ui/sheet'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import { formatInstant } from '@prestamos/utils'

import { PERMISSIONS } from '@/app/permissions'
import { useSessionStore } from '@/app/stores/session'

import {
  AUDIT_ACTION_OPTIONS,
  AUDIT_ENTITY_SUGGESTIONS,
  useAuditLog,
} from '../composables/useAuditLog'
import { auditLogColumns } from '../utils/auditColumns'

const route = useRoute()
const session = useSessionStore()

const canReadApprovals = computed(() => session.can(PERMISSIONS.approvalsRead))

function queryString(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

const {
  loading,
  auditPage,
  filters,
  approvalRequestIdError,
  totalPages,
  loadAuditLogs,
  onSearch,
  resetFilters,
  goToPage,
} = useAuditLog({
  entity: queryString(route.query.entity),
  entityId: queryString(route.query.entityId),
  approvalRequestId: queryString(route.query.approvalRequestId),
})

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

function displayActor(entry: AuditLogEntry | null): string {
  if (!entry) return 'Sistema'
  return entry.userName || entry.userEmail || entry.userId || 'Sistema'
}

function getActionBadgeVariant(action: string) {
  switch (action.toUpperCase()) {
    case 'INSERT':
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

onMounted(() => {
  void loadAuditLogs()
})
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Bitácora de auditoría"
      description="Registro inmutable de trazabilidad de cambios de estado, usuarios y aprobaciones vinculadas."
    >
      <template #actions>
        <Button variant="outline" size="sm" :disabled="loading" @click="loadAuditLogs">
          <RefreshCw class="mr-2 size-4" :class="{ 'animate-spin': loading }" />
          Actualizar
        </Button>
      </template>
    </PageHeader>

    <!-- ── Filtros de búsqueda ── -->
    <Card>
      <CardHeader class="pb-3">
        <CardTitle class="flex items-center gap-2 text-base">
          <Filter class="size-4 text-muted-foreground" />
          Filtros de auditoría
        </CardTitle>
        <CardDescription>
          Consulta directamente <code class="text-xs">GET /api/audit-logs</code> por entidad, ID,
          acción, usuario, aprobación o rango de fechas.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form
          class="grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4"
          @submit.prevent="onSearch"
        >
          <!-- Entidad -->
          <div class="space-y-1.5">
            <label for="entity" class="text-xs font-medium text-muted-foreground">Entidad</label>
            <Input
              id="entity"
              v-model="filters.entity"
              list="audit-entity-suggestions"
              placeholder="Ej. users, approval_requests..."
              class="h-9"
            />
            <datalist id="audit-entity-suggestions">
              <option v-for="ent in AUDIT_ENTITY_SUGGESTIONS" :key="ent" :value="ent" />
            </datalist>
          </div>

          <!-- ID de Entidad -->
          <div class="space-y-1.5">
            <label for="entityId" class="text-xs font-medium text-muted-foreground">
              ID de Entidad
            </label>
            <Input
              id="entityId"
              v-model="filters.entityId"
              placeholder="UUID o identificador..."
              class="h-9 font-mono text-xs"
            />
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
              <option v-for="act in AUDIT_ACTION_OPTIONS" :key="act" :value="act">
                {{ act }}
              </option>
            </select>
          </div>

          <!-- Usuario (nombre, correo o UUID) -->
          <div class="space-y-1.5">
            <label for="user" class="text-xs font-medium text-muted-foreground">
              Usuario (nombre, correo o UUID)
            </label>
            <Input
              id="user"
              v-model="filters.user"
              placeholder="Nombre, correo o UUID..."
              class="h-9"
            />
          </div>

          <!-- ID Aprobación -->
          <div class="space-y-1.5">
            <label for="approvalRequestId" class="text-xs font-medium text-muted-foreground">
              ID de Aprobación (UUID)
            </label>
            <Input
              id="approvalRequestId"
              v-model="filters.approvalRequestId"
              placeholder="UUID de solicitud..."
              class="h-9 font-mono text-xs"
              :aria-invalid="!!approvalRequestIdError"
            />
            <p v-if="approvalRequestIdError" class="text-xs text-destructive">
              {{ approvalRequestIdError }}
            </p>
          </div>

          <!-- Fecha Desde -->
          <div class="space-y-1.5">
            <label for="from" class="text-xs font-medium text-muted-foreground">Desde</label>
            <Input id="from" v-model="filters.from" type="date" class="h-9" />
          </div>

          <!-- Fecha Hasta -->
          <div class="space-y-1.5">
            <label for="to" class="text-xs font-medium text-muted-foreground">Hasta</label>
            <Input id="to" v-model="filters.to" type="date" class="h-9" />
          </div>

          <!-- Botones de acción -->
          <div class="flex items-end justify-end gap-2">
            <Button type="button" variant="ghost" size="sm" @click="resetFilters">
              <X class="mr-1 size-4" />
              Limpiar
            </Button>
            <Button type="submit" size="sm" :disabled="loading">
              <Search class="mr-1 size-4" />
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
        <div v-if="loading" class="space-y-4 p-6">
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
            <Button variant="outline" size="sm" @click="resetFilters">Restablecer filtros</Button>
          </EmptyState>
        </div>

        <!-- Lista de registros -->
        <div v-else>
          <TableGenerator :columns="auditLogColumns" :rows="auditPage.items" row-key="id">
            <template #cell-user="{ row: entry }">
              <div class="font-medium leading-tight text-foreground">
                {{ displayActor(entry) }}
              </div>
              <div v-if="entry.userEmail && entry.userName" class="text-xs text-muted-foreground">
                {{ entry.userEmail }}
              </div>
              <div class="font-mono text-xs text-muted-foreground">
                {{ entry.userIp ?? 'Sin IP' }}
              </div>
            </template>

            <template #cell-entity="{ row: entry }">
              <Badge variant="outline" class="font-mono text-xs">
                {{ entry.entityName }}
              </Badge>
            </template>

            <template #cell-action="{ row: entry }">
              <span
                class="inline-flex items-center rounded-md border px-2 py-0.5 text-xs font-semibold"
                :class="getActionBadgeVariant(entry.action)"
              >
                {{ entry.action }}
              </span>
            </template>

            <template #cell-compare="{ row: entry }">
              <Button
                variant="ghost"
                size="sm"
                class="h-8 gap-1.5 text-xs"
                @click="openDetail(entry)"
              >
                <Eye class="size-3.5" />
                <span>Ver cambio</span>
              </Button>
            </template>
          </TableGenerator>
        </div>

        <!-- ── Paginación ── -->
        <div
          v-if="auditPage.total > 0"
          class="flex flex-col items-center justify-between gap-4 border-t p-4 text-xs text-muted-foreground sm:flex-row"
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
              <ChevronLeft class="mr-1 size-4" />
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
              <ChevronRight class="ml-1 size-4" />
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- ── Sheet de Vista "Antes / Después" (JSON) ── -->
    <Sheet :open="isDetailOpen" @update:open="(val) => (val ? null : closeDetail())">
      <SheetContent side="right" class="w-full overflow-y-auto sm:max-w-2xl">
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
            <strong class="text-foreground">{{ displayActor(selectedEntry) }}</strong>
            el {{ selectedEntry ? formatInstant(selectedEntry.createdAt) : '' }} desde la IP
            {{ selectedEntry?.userIp ?? 'Sin IP' }}.
          </SheetDescription>
        </SheetHeader>

        <div v-if="selectedEntry" class="space-y-6 pt-4">
          <!-- Metadatos rápidos -->
          <div class="grid grid-cols-2 gap-3 rounded-lg border bg-muted/30 p-3 text-xs">
            <div>
              <span class="block text-muted-foreground">Entidad afectada:</span>
              <span class="font-mono font-medium">{{ selectedEntry.entityName }}</span>
            </div>
            <div>
              <span class="block text-muted-foreground">Identificador (ID):</span>
              <span class="font-mono font-medium">{{ selectedEntry.entityId }}</span>
            </div>
            <div>
              <span class="block text-muted-foreground">Operación:</span>
              <span class="font-medium">{{ selectedEntry.action }}</span>
            </div>
            <div>
              <span class="block text-muted-foreground">ID Registro Auditoría:</span>
              <span class="font-mono font-medium">#{{ selectedEntry.id }}</span>
            </div>
            <div v-if="selectedEntry.userEmail || selectedEntry.userId">
              <span class="block text-muted-foreground">Usuario / Correo:</span>
              <span class="font-medium">
                {{ selectedEntry.userEmail ?? selectedEntry.userId }}
              </span>
            </div>
            <div v-if="selectedEntry.userAgent">
              <span class="block text-muted-foreground">User-Agent:</span>
              <span class="line-clamp-2 font-mono text-[11px]">{{ selectedEntry.userAgent }}</span>
            </div>
            <div v-if="selectedEntry.approvalRequestId" class="col-span-2">
              <span class="block text-muted-foreground">Solicitud de aprobación vinculada:</span>
              <RouterLink
                v-if="canReadApprovals"
                :to="{ name: 'approvals', query: { id: selectedEntry.approvalRequestId } }"
                class="inline-flex items-center gap-1 font-mono font-medium text-primary hover:underline"
                @click="closeDetail"
              >
                {{ selectedEntry.approvalRequestId }}
                <ExternalLink class="size-3.5" aria-hidden="true" />
              </RouterLink>
              <span v-else class="font-mono font-medium">
                {{ selectedEntry.approvalRequestId }}
              </span>
            </div>
          </div>

          <!-- Comparativa Antes vs Después -->
          <div class="space-y-4">
            <h4 class="flex items-center gap-2 text-sm font-semibold">
              <FileText class="size-4 text-primary" />
              Comparativa de Estado (JSON)
            </h4>

            <div class="grid grid-cols-1 gap-4 md:grid-cols-2">
              <!-- Estado Antes -->
              <div class="space-y-2 rounded-lg border border-border/80 bg-background p-3">
                <div class="flex items-center justify-between">
                  <span
                    class="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground"
                  >
                    <span class="size-2 rounded-full bg-destructive"></span>
                    Estado Anterior
                  </span>
                  <Badge v-if="!selectedEntry.stateBefore" variant="outline" class="text-[10px]">
                    Sin estado previo
                  </Badge>
                </div>
                <pre
                  class="max-h-80 overflow-auto whitespace-pre-wrap rounded bg-muted/60 p-2.5 font-mono text-[11px] leading-relaxed text-foreground/90"
                  >{{ formatJson(selectedEntry.stateBefore) }}</pre
                >
              </div>

              <!-- Estado Después -->
              <div class="space-y-2 rounded-lg border border-border/80 bg-background p-3">
                <div class="flex items-center justify-between">
                  <span
                    class="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground"
                  >
                    <span class="size-2 rounded-full bg-success"></span>
                    Estado Posterior
                  </span>
                  <Badge v-if="!selectedEntry.stateAfter" variant="outline" class="text-[10px]">
                    Sin estado posterior
                  </Badge>
                </div>
                <pre
                  class="max-h-80 overflow-auto whitespace-pre-wrap rounded bg-muted/60 p-2.5 font-mono text-[11px] leading-relaxed text-foreground/90"
                  >{{ formatJson(selectedEntry.stateAfter) }}</pre
                >
              </div>
            </div>
          </div>

          <div class="flex justify-end border-t pt-4">
            <Button variant="outline" size="sm" @click="closeDetail">Cerrar visor</Button>
          </div>
        </div>
      </SheetContent>
    </Sheet>
  </div>
</template>
