<script setup lang="ts">
import { ref, onMounted } from 'vue'
import {
  FileText,
  FilePlus,
  Search,
  ChevronLeft,
  ChevronRight,
  Eye,
  X,
  Clock,
} from '@lucide/vue'

import { EmptyState, MoneyText, PageHeader, StatusBadge, TableGenerator } from '@prestamos/ui'
import { Badge } from '@prestamos/ui/components/ui/badge'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
} from '@prestamos/ui/components/ui/card'
import { Input } from '@prestamos/ui/components/ui/input'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import { formatInstant } from '@prestamos/utils'

import { useOrigination } from '../composables/useOrigination'
import CreateApplicationSheet from '../components/CreateApplicationSheet.vue'

// FE-BO-16: Bandeja de solicitudes de crédito ordenada por antigüedad con filtros por estado.

const {
  loading,
  applicationsPage,
  searchQuery,
  selectedStatus,
  totalPages,
  loadApplications,
  onSearch,
  resetFilters,
  goToPage,
} = useOrigination()

const isCreateOpen = ref(false)

function getRecommendationBadge(rec?: string) {
  switch (rec) {
    case 'APROBABLE':
      return { label: 'Aprobable', class: 'border-success text-success bg-success/10' }
    case 'REVISION_MANUAL':
      return { label: 'Revisión Manual', class: 'border-warning text-warning bg-warning/10' }
    case 'RECHAZO_SUGERIDO':
      return {
        label: 'Rechazo Sugerido',
        class: 'border-destructive text-destructive bg-destructive/10',
      }
    default:
      return { label: 'Sin evaluar', class: 'border-border text-muted-foreground' }
  }
}

function formatDti(dti?: string): string {
  if (!dti) return '—'
  const val = Number(dti) * 100
  return `${val.toFixed(1)}%`
}

onMounted(() => {
  void loadApplications()
})
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Bandeja de Solicitudes de Crédito"
      description="Originación, solicitudes pendientes ordenadas por antigüedad y evaluación de riesgo crediticio (FE-BO-16)."
    >
      <template #actions>
        <Button size="sm" @click="isCreateOpen = true">
          <FilePlus class="size-4 mr-2" />
          Nueva Solicitud
        </Button>
      </template>
    </PageHeader>

    <!-- ── Filtros y Pestañas de Estado ── -->
    <Card>
      <CardContent class="p-4 space-y-3">
        <!-- Pestañas de bandeja -->
        <div class="flex flex-wrap items-center gap-1.5 pb-1 border-b text-xs">
          <button
            class="px-3 py-1.5 rounded-md font-semibold transition-all flex items-center gap-1.5"
            :class="
              selectedStatus === 'SOLICITADO,EN_EVALUACION'
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground hover:text-foreground'
            "
            @click="
              selectedStatus = 'SOLICITADO,EN_EVALUACION';
              onSearch();
            "
          >
            <Clock class="size-3.5" />
            Pendientes de Evaluación
          </button>
          <button
            class="px-3 py-1.5 rounded-md font-semibold transition-all"
            :class="
              selectedStatus === ''
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground hover:text-foreground'
            "
            @click="
              selectedStatus = '';
              onSearch();
            "
          >
            Todas las Solicitudes
          </button>
          <button
            class="px-3 py-1.5 rounded-md font-semibold transition-all"
            :class="
              selectedStatus === 'APROBADO,APROBADO_CON_CONDICIONES'
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground hover:text-foreground'
            "
            @click="
              selectedStatus = 'APROBADO,APROBADO_CON_CONDICIONES';
              onSearch();
            "
          >
            Aprobadas
          </button>
          <button
            class="px-3 py-1.5 rounded-md font-semibold transition-all"
            :class="
              selectedStatus === 'RECHAZADO'
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground hover:text-foreground'
            "
            @click="
              selectedStatus = 'RECHAZADO';
              onSearch();
            "
          >
            Rechazadas
          </button>
        </div>

        <!-- Buscador -->
        <form class="flex items-center gap-2" @submit.prevent="onSearch">
          <div class="relative flex-1">
            <Search class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
            <Input
              v-model="searchQuery"
              placeholder="Buscar por cliente, ID de solicitud o producto..."
              class="pl-9 h-9"
            />
          </div>
          <Button type="submit" size="sm" class="h-9" :disabled="loading"> Buscar </Button>
          <Button
            v-if="searchQuery"
            type="button"
            variant="ghost"
            size="sm"
            class="h-9"
            @click="resetFilters"
          >
            <X class="size-4" />
          </Button>
        </form>
      </CardContent>
    </Card>

    <!-- ── Tabla de Solicitudes ── -->
    <Card>
      <CardContent class="p-0">
        <div v-if="loading" class="p-6 space-y-4">
          <Skeleton class="h-10 w-full" />
          <Skeleton class="h-12 w-full" />
          <Skeleton class="h-12 w-full" />
          <Skeleton class="h-12 w-full" />
        </div>

        <div v-else-if="applicationsPage.items.length === 0" class="p-12">
          <EmptyState
            :icon="FileText"
            title="Bandeja de solicitudes vacía"
            description="No hay solicitudes de crédito que coincidan con los filtros seleccionados."
          >
            <Button size="sm" @click="isCreateOpen = true">
              <FilePlus class="size-4 mr-2" />
              Ingresar Solicitud
            </Button>
          </EmptyState>
        </div>

        <div v-else>
          <TableGenerator
            :columns="[
              { key: 'id', label: 'ID' },
              { key: 'createdAt', label: 'Fecha y Hora' },
              { key: 'customer', label: 'Cliente' },
              { key: 'product', label: 'Producto' },
              { key: 'amount', label: 'Monto / Plazo' },
              { key: 'dti', label: 'DTI Motor' },
              { key: 'recommendation', label: 'Recomendación' },
              { key: 'status', label: 'Estado' },
              { key: 'action', label: 'Acción', headerClass: 'text-right' },
            ]"
            :rows="applicationsPage.items"
            row-key="id"
          >
            <template #row="{ row: app }">
              <td class="px-4 py-3 font-mono text-xs font-bold">
                {{ app.id }}
              </td>
              <td class="px-4 py-3 text-xs whitespace-nowrap text-muted-foreground">
                {{ formatInstant(app.createdAt) }}
              </td>
              <td class="px-4 py-3 font-medium text-foreground">
                <RouterLink
                  :to="{ name: 'customer-detail', params: { id: app.customerId } }"
                  class="hover:underline flex items-center gap-1 text-primary"
                >
                  {{ app.customerName }}
                </RouterLink>
                <span class="text-[11px] text-muted-foreground block font-normal">
                  Canal: {{ app.originatedFrom }}
                </span>
              </td>
              <td class="px-4 py-3 text-xs font-medium">
                {{ app.productName }}
              </td>
              <td class="px-4 py-3 text-xs font-mono">
                <div class="font-bold text-foreground">
                  <MoneyText :amount="app.requestedAmount" />
                </div>
                <div class="text-[11px] text-muted-foreground">
                  {{ app.term }} cuotas {{ app.frequency.toLowerCase() }}
                </div>
              </td>
              <td class="px-4 py-3 text-xs font-mono font-semibold">
                <span
                  :class="
                    Number(app.dtiRatio ?? 0) > 0.5
                      ? 'text-destructive font-bold'
                      : Number(app.dtiRatio ?? 0) > 0.35
                        ? 'text-warning font-bold'
                        : 'text-success'
                  "
                >
                  {{ formatDti(app.dtiRatio) }}
                </span>
              </td>
              <td class="px-4 py-3">
                <Badge
                  variant="outline"
                  class="text-[10px]"
                  :class="getRecommendationBadge(app.systemRecommendation).class"
                >
                  {{ getRecommendationBadge(app.systemRecommendation).label }}
                </Badge>
              </td>
              <td class="px-4 py-3">
                <StatusBadge kind="application" :status="app.status" />
              </td>
              <td class="px-4 py-3 text-right">
                <Button variant="outline" size="sm" class="h-8 gap-1 text-xs" as-child>
                  <RouterLink :to="{ name: 'application-detail', params: { id: app.id } }">
                    <Eye class="size-3.5" />
                    <span>Evaluar</span>
                  </RouterLink>
                </Button>
              </td>
            </template>
          </TableGenerator>
        </div>

        <!-- Paginación -->
        <div
          v-if="applicationsPage.total > 0"
          class="flex flex-col sm:flex-row items-center justify-between gap-4 border-t p-4 text-xs text-muted-foreground"
        >
          <div>
            Mostrando
            <span class="font-medium text-foreground">
              {{ (applicationsPage.page - 1) * applicationsPage.pageSize + 1 }}
            </span>
            a
            <span class="font-medium text-foreground">
              {{
                Math.min(applicationsPage.page * applicationsPage.pageSize, applicationsPage.total)
              }}
            </span>
            de
            <span class="font-medium text-foreground">{{ applicationsPage.total }}</span>
            solicitudes
          </div>

          <div class="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              class="h-8"
              :disabled="applicationsPage.page <= 1 || loading"
              @click="goToPage(applicationsPage.page - 1)"
            >
              <ChevronLeft class="size-4 mr-1" />
              Anterior
            </Button>
            <span class="px-2 font-medium text-foreground">
              Página {{ applicationsPage.page }} de {{ totalPages }}
            </span>
            <Button
              variant="outline"
              size="sm"
              class="h-8"
              :disabled="applicationsPage.page >= totalPages || loading"
              @click="goToPage(applicationsPage.page + 1)"
            >
              Siguiente
              <ChevronRight class="size-4 ml-1" />
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- ── Sheet para crear solicitud con simulador ── -->
    <CreateApplicationSheet v-model:open="isCreateOpen" @created="loadApplications" />
  </div>
</template>
