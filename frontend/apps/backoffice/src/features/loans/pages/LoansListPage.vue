<script setup lang="ts">
import { ref, onMounted } from 'vue'
import {
  HandCoins,
  Search,
  ArrowUpRight,
  ChevronLeft,
  ChevronRight,
  Eye,
  X,
} from '@lucide/vue'

import type { Loan } from '@prestamos/api-client'
import { EmptyState, PageHeader, TableGenerator, type TableColumn } from '@prestamos/ui'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
} from '@prestamos/ui/components/ui/card'
import { Input } from '@prestamos/ui/components/ui/input'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'

import { useLoans } from '../composables/useLoans'
import DisbursementModal from '../components/DisbursementModal.vue'

// FE-BO-18 y FE-BO-19: Directorio de préstamos de la cartera y desembolsos digitales.

const {
  loading,
  loansPage,
  searchQuery,
  selectedStatus,
  totalPages,
  loadLoans,
  onSearch,
  resetFilters,
  goToPage,
} = useLoans()

const isDisburseOpen = ref(false)

const loanColumns: TableColumn<Loan>[] = [
  { key: 'id', label: 'Código', kind: 'mono', cellClass: 'text-xs font-bold text-foreground' },
  { key: 'customerName', label: 'Prestatario', cellClass: 'font-medium text-foreground' },
  { key: 'productName', label: 'Producto', cellClass: 'text-xs text-muted-foreground font-medium' },
  { key: 'principal', label: 'Capital Otorgado', kind: 'money', cellClass: 'text-xs' },
  {
    key: 'outstandingPrincipal',
    label: 'Saldo Insoluto',
    kind: 'money',
    cellClass: 'text-xs font-bold text-foreground',
  },
  { key: 'totalPaid', label: 'Total Pagado', kind: 'money', cellClass: 'text-xs text-success' },
  { key: 'firstDueDate', label: 'Primer Vencimiento', kind: 'date', cellClass: 'text-xs text-muted-foreground' },
  { key: 'status', label: 'Estado', kind: 'status', statusKind: 'loan' },
  { key: 'action', label: 'Acción', align: 'right' },
]

onMounted(() => {
  void loadLoans()
})
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Cartera de Préstamos"
      description="Supervisión de préstamos vigentes, cronogramas de cuotas y ejecución de desembolsos digitales (FE-BO-18)."
    >
      <template #actions>
        <Button size="sm" @click="isDisburseOpen = true">
          <ArrowUpRight class="size-4 mr-2" />
          Ejecutar Desembolso
        </Button>
      </template>
    </PageHeader>

    <!-- ── Filtros ── -->
    <Card>
      <CardContent class="p-4">
        <form class="flex flex-col sm:flex-row items-center gap-3" @submit.prevent="onSearch">
          <div class="relative flex-1 w-full">
            <Search class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
            <Input
              v-model="searchQuery"
              placeholder="Buscar por cliente, ID de préstamo o producto..."
              class="pl-9 h-9"
            />
          </div>

          <div class="w-full sm:w-48">
            <select
              v-model="selectedStatus"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              @change="onSearch"
            >
              <option value="">Todos los estados</option>
              <option value="VIGENTE">Vigente (al día)</option>
              <option value="EN_MORA">En Mora</option>
              <option value="PAGADO">Cancelado / Pagado</option>
              <option value="PENDIENTE_DESEMBOLSO">Pendiente Desembolso</option>
              <option value="CASTIGADO">Castigado</option>
            </select>
          </div>

          <div class="flex items-center gap-2 w-full sm:w-auto shrink-0 justify-end">
            <Button
              v-if="searchQuery || selectedStatus"
              type="button"
              variant="ghost"
              size="sm"
              class="h-9"
              @click="resetFilters"
            >
              <X class="size-4 mr-1" />
              Limpiar
            </Button>
            <Button type="submit" size="sm" class="h-9" :disabled="loading">
              <Search class="size-4 mr-1.5" />
              Buscar
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>

    <!-- ── Tabla de Préstamos ── -->
    <Card>
      <CardContent class="p-0">
        <div v-if="loading" class="p-6 space-y-4">
          <Skeleton class="h-10 w-full" />
          <Skeleton class="h-12 w-full" />
          <Skeleton class="h-12 w-full" />
          <Skeleton class="h-12 w-full" />
        </div>

        <div v-else-if="loansPage.items.length === 0" class="p-12">
          <EmptyState
            :icon="HandCoins"
            title="No se encontraron préstamos"
            description="No existen créditos que coincidan con los criterios de búsqueda seleccionados."
          >
            <Button size="sm" @click="isDisburseOpen = true">
              <ArrowUpRight class="size-4 mr-2" />
              Nuevo Desembolso
            </Button>
          </EmptyState>
        </div>

        <div v-else>
          <TableGenerator
            :columns="loanColumns"
            :rows="loansPage.items"
            row-key="id"
          >
            <template #cell-customerName="{ row: loan }">
              <RouterLink
                :to="{ name: 'customer-detail', params: { id: loan.customerId } }"
                class="hover:underline flex items-center gap-1 text-primary font-medium"
              >
                {{ loan.customerName }}
              </RouterLink>
            </template>
            <template #cell-action="{ row: loan }">
              <Button variant="ghost" size="sm" class="h-8 gap-1 text-xs" as-child>
                <RouterLink :to="{ name: 'loan-detail', params: { id: loan.id } }">
                  <Eye class="size-3.5" />
                  <span>Ver Detalle</span>
                </RouterLink>
              </Button>
            </template>
          </TableGenerator>
        </div>

        <!-- Paginación -->
        <div
          v-if="loansPage.total > 0"
          class="flex flex-col sm:flex-row items-center justify-between gap-4 border-t p-4 text-xs text-muted-foreground"
        >
          <div>
            Mostrando
            <span class="font-medium text-foreground">
              {{ (loansPage.page - 1) * loansPage.pageSize + 1 }}
            </span>
            a
            <span class="font-medium text-foreground">
              {{ Math.min(loansPage.page * loansPage.pageSize, loansPage.total) }}
            </span>
            de
            <span class="font-medium text-foreground">{{ loansPage.total }}</span>
            préstamos
          </div>

          <div class="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              class="h-8"
              :disabled="loansPage.page <= 1 || loading"
              @click="goToPage(loansPage.page - 1)"
            >
              <ChevronLeft class="size-4 mr-1" />
              Anterior
            </Button>
            <span class="px-2 font-medium text-foreground">
              Página {{ loansPage.page }} de {{ totalPages }}
            </span>
            <Button
              variant="outline"
              size="sm"
              class="h-8"
              :disabled="loansPage.page >= totalPages || loading"
              @click="goToPage(loansPage.page + 1)"
            >
              Siguiente
              <ChevronRight class="size-4 ml-1" />
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- ── Modal para Ejecutar Desembolso ── -->
    <DisbursementModal v-model:open="isDisburseOpen" @disbursed="loadLoans" />
  </div>
</template>
