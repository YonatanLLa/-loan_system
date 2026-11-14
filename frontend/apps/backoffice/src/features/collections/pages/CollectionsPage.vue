<script setup lang="ts">
import { onMounted, ref } from 'vue'
import {
  PhoneCall,
  Search,
  ChevronLeft,
  ChevronRight,
  MessageSquarePlus,
  UserPlus,
  ShieldAlert,
  Receipt,
  AlertTriangle,
  Phone,
  MessageCircle,
} from '@lucide/vue'

import type { AgingBucket, AgingRow } from '@prestamos/api-client'
import { EmptyState, PageHeader, TableGenerator } from '@prestamos/ui'
import { Badge } from '@prestamos/ui/components/ui/badge'
import { Button } from '@prestamos/ui/components/ui/button'
import { Card, CardContent } from '@prestamos/ui/components/ui/card'
import { Input } from '@prestamos/ui/components/ui/input'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'

import AssignCollectorDialog from '../components/AssignCollectorDialog.vue'
import CollectionActionDialog from '../components/CollectionActionDialog.vue'
import WriteOffDialog from '../components/WriteOffDialog.vue'
import { useAging, useCollectionsCatalogs, useReceivables } from '../composables'
import { BUCKET_LABEL, agingColumns, receivableColumns } from '../utils/collectionsColumns'

// FE-BO-32 a FE-BO-36: aging, gestiones, asignación, castigo y cuentas por cobrar.

const BUCKETS: { id: AgingBucket | ''; label: string }[] = [
  { id: '', label: 'Todos' },
  { id: 'PREVENTIVO', label: 'Preventivo' },
  { id: 'TRAMO_1', label: '1–30' },
  { id: 'TRAMO_2', label: '31–60' },
  { id: 'TRAMO_3', label: '61–90' },
  { id: 'TRAMO_4', label: '> 90' },
]

const selectClass =
  'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring'

const activeTab = ref<'aging' | 'receivables'>('aging')

// Composables para lógica reactiva y peticiones a la API
const {
  pageData: agingPage,
  totalPages: agingPages,
  currentPage: agingPageNumber,
  bucketCounts,
  loading: agingLoading,
  bucket,
  assignedTo,
  productName,
  minAmount,
  maxAmount,
  query: agingQuery,
  applyFilters: applyAgingFilters,
  chooseBucket,
  setPage: setAgingPage,
  refresh: refreshAging,
} = useAging()

const {
  pageData: receivablesPage,
  totalPages: receivablePages,
  currentPage: receivablePageNumber,
  loading: receivableLoading,
  type: receivableType,
  query: receivableQuery,
  applyFilters: applyReceivableFilters,
  setPage: setReceivablePage,
  loadReceivables,
} = useReceivables()

const {
  collectors,
  products,
  loadCatalogs,
} = useCollectionsCatalogs()

const actionOpen = ref(false)
const assignOpen = ref(false)
const writeOffOpen = ref(false)
const selected = ref<AgingRow | null>(null)

function openAction(row: AgingRow) {
  selected.value = row
  actionOpen.value = true
}

function openAssign(row: AgingRow) {
  selected.value = row
  assignOpen.value = true
}

function openWriteOff(row: AgingRow) {
  selected.value = row
  writeOffOpen.value = true
}

async function refreshAfterMutation() {
  await refreshAging()
}

onMounted(() => {
  void loadCatalogs()
  void refreshAging()
  void loadReceivables()
})
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Cobranza"
      description="Bandeja de aging, gestiones, promesas de pago, asignación de gestores, castigo y cuentas por cobrar."
    />

    <div class="flex items-center gap-1 border-b text-sm">
      <button
        type="button"
        class="flex items-center gap-2 border-b-2 px-4 py-2.5 font-semibold transition-all"
        :class="
          activeTab === 'aging'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground'
        "
        @click="activeTab = 'aging'"
      >
        <PhoneCall class="size-4" />
        Aging
      </button>
      <button
        type="button"
        class="flex items-center gap-2 border-b-2 px-4 py-2.5 font-semibold transition-all"
        :class="
          activeTab === 'receivables'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground'
        "
        @click="activeTab = 'receivables'"
      >
        <Receipt class="size-4" />
        Cuentas por cobrar
      </button>
    </div>

    <template v-if="activeTab === 'aging'">
      <div class="flex flex-wrap gap-2">
        <Button
          v-for="item in BUCKETS"
          :key="item.id || 'all'"
          type="button"
          size="sm"
          :variant="bucket === item.id ? 'default' : 'outline'"
          @click="chooseBucket(item.id)"
        >
          {{ item.label }}
          <span v-if="bucketCounts[item.id] != null" class="ml-1.5 text-[11px] opacity-80">
            {{ bucketCounts[item.id] }}
          </span>
        </Button>
      </div>

      <Card>
        <CardContent class="p-4">
          <form class="grid gap-3 md:grid-cols-6" @submit.prevent="applyAgingFilters">
            <div class="relative md:col-span-2">
              <Search
                class="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
              />
              <Input
                v-model="agingQuery"
                class="h-9 pl-9"
                placeholder="Cliente, teléfono o préstamo"
              />
            </div>
            <select v-model="assignedTo" :class="selectClass" aria-label="Gestor">
              <option value="">Todos los gestores</option>
              <option v-for="person in collectors" :key="person.id" :value="person.fullName">
                {{ person.fullName }}
              </option>
            </select>
            <select v-model="productName" :class="selectClass" aria-label="Producto">
              <option value="">Todos los productos</option>
              <option v-for="product in products" :key="product.id" :value="product.name">
                {{ product.name }}
              </option>
            </select>
            <Input v-model="minAmount" class="h-9" placeholder="Monto mín." inputmode="decimal" />
            <div class="flex gap-2">
              <Input v-model="maxAmount" class="h-9" placeholder="Monto máx." inputmode="decimal" />
              <Button type="submit" size="sm" class="h-9" :disabled="agingLoading">Filtrar</Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardContent class="p-0">
          <div v-if="agingLoading" class="space-y-3 p-6">
            <Skeleton class="h-10 w-full" />
            <Skeleton class="h-12 w-full" />
            <Skeleton class="h-12 w-full" />
          </div>
          <div v-else-if="agingPage.items.length === 0" class="p-8">
            <EmptyState
              :icon="PhoneCall"
              title="Sin créditos en este tramo"
              description="No hay préstamos que coincidan con el tramo, el gestor, el producto o el monto."
            />
          </div>
          <div v-else>
            <TableGenerator
              :columns="agingColumns"
              :rows="agingPage.items"
              row-key="loanId"
              body-class="divide-y text-xs"
              :row-class="
                (row) => ({
                  'hover:bg-muted/40': true,
                  'bg-destructive/5': row.hasBrokenPromise === true,
                })
              "
            >
              <template #cell-customer="{ row }">
                <RouterLink
                  :to="{ name: 'customer-detail', params: { id: row.customerId } }"
                  class="font-medium text-foreground hover:underline"
                >
                  {{ row.customerName }}
                </RouterLink>
                <span class="mt-0.5 flex flex-wrap items-center gap-1.5 text-[11px] text-muted-foreground">
                  <a
                    :href="`tel:${row.phone}`"
                    class="inline-flex items-center gap-1 font-mono hover:text-primary hover:underline"
                    title="Llamar"
                  >
                    <Phone class="size-3" />
                    {{ row.phone }}
                  </a>
                  <span>·</span>
                  <a
                    :href="`https://wa.me/51${row.phone.replace(/\D/g, '')}`"
                    target="_blank"
                    rel="noopener noreferrer"
                    class="inline-flex items-center gap-1 font-medium text-emerald-600 dark:text-emerald-400 hover:underline"
                    title="Abrir WhatsApp"
                  >
                    <MessageCircle class="size-3" />
                    WhatsApp
                  </a>
                  <span>·</span>
                  <RouterLink
                    :to="{ name: 'loan-detail', params: { id: row.loanId } }"
                    class="font-mono hover:underline"
                  >
                    {{ row.loanId }}
                  </RouterLink>
                </span>
                <span class="mt-1 block text-[11px] text-muted-foreground">{{
                  row.productName
                }}</span>
                <Badge
                  v-if="row.hasBrokenPromise"
                  variant="outline"
                  class="mt-1 border-destructive/40 bg-destructive/10 text-destructive"
                >
                  <AlertTriangle class="mr-1 size-3" />
                  Promesa incumplida
                </Badge>
              </template>

              <template #cell-bucket="{ row }">
                <Badge variant="outline">{{ BUCKET_LABEL[row.bucket] }}</Badge>
              </template>

              <template #cell-actions="{ row }">
                <div class="flex justify-end gap-1">
                  <Button size="sm" variant="outline" class="h-8" @click="openAction(row)">
                    <MessageSquarePlus class="size-3.5" />
                    <span class="sr-only">Registrar gestión</span>
                  </Button>
                  <Button size="sm" variant="outline" class="h-8" @click="openAssign(row)">
                    <UserPlus class="size-3.5" />
                    <span class="sr-only">Asignar gestor</span>
                  </Button>
                  <Button
                    v-if="row.daysLate > 90"
                    size="sm"
                    variant="outline"
                    class="h-8 text-destructive"
                    @click="openWriteOff(row)"
                  >
                    <ShieldAlert class="size-3.5" />
                    <span class="sr-only">Castigar</span>
                  </Button>
                </div>
              </template>
            </TableGenerator>
          </div>
          <div
            v-if="agingPage.total > 0"
            class="flex items-center justify-between border-t p-4 text-xs text-muted-foreground"
          >
            <span>{{ agingPage.total }} créditos</span>
            <div class="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                class="h-8"
                :disabled="agingPage.page <= 1 || agingLoading"
                @click="setAgingPage(agingPageNumber - 1)"
              >
                <ChevronLeft class="size-4" />
              </Button>
              <span>Página {{ agingPage.page }} de {{ agingPages }}</span>
              <Button
                variant="outline"
                size="sm"
                class="h-8"
                :disabled="agingPage.page >= agingPages || agingLoading"
                @click="setAgingPage(agingPageNumber + 1)"
              >
                <ChevronRight class="size-4" />
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </template>

    <template v-else>
      <Card>
        <CardContent class="p-4">
          <form class="flex flex-wrap items-center gap-2" @submit.prevent="applyReceivableFilters">
            <div class="relative min-w-64 flex-1">
              <Search
                class="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
              />
              <Input
                v-model="receivableQuery"
                class="h-9 pl-9"
                placeholder="Cliente, préstamo o producto"
              />
            </div>
            <select
              v-model="receivableType"
              :class="selectClass + ' w-auto'"
              aria-label="Estado de la cuota"
            >
              <option value="">Próximos vencimientos y vencidas</option>
              <option value="VENCIDA">Solo cuotas vencidas</option>
              <option value="PENDIENTE">Solo por vencer</option>
            </select>
            <Button type="submit" size="sm" class="h-9" :disabled="receivableLoading">Filtrar</Button>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardContent class="p-0">
          <div v-if="receivableLoading" class="space-y-3 p-6">
            <Skeleton class="h-10 w-full" />
            <Skeleton class="h-12 w-full" />
          </div>
          <div v-else-if="receivablesPage.items.length === 0" class="p-8">
            <EmptyState
              :icon="Receipt"
              title="Sin cuotas en esta vista"
              description="No hay vencimientos ni saldos pendientes con ese criterio."
            />
          </div>
          <div v-else>
            <TableGenerator
              :columns="receivableColumns"
              :rows="receivablesPage.items"
              :row-key="(item) => `${item.loanId}-${item.installmentNumber}`"
              body-class="divide-y text-xs"
            >
              <template #cell-customer="{ row: item }">
                <span class="font-medium text-foreground">{{ item.customerName }}</span>
                <span class="mt-0.5 flex flex-wrap items-center gap-1.5 text-[11px] text-muted-foreground">
                  <a
                    :href="`tel:${item.phone}`"
                    class="inline-flex items-center gap-1 font-mono hover:text-primary hover:underline"
                    title="Llamar"
                  >
                    <Phone class="size-3" />
                    {{ item.phone }}
                  </a>
                  <span>·</span>
                  <a
                    :href="`https://wa.me/51${item.phone.replace(/\D/g, '')}`"
                    target="_blank"
                    rel="noopener noreferrer"
                    class="inline-flex items-center gap-1 font-medium text-emerald-600 dark:text-emerald-400 hover:underline"
                    title="Abrir WhatsApp"
                  >
                    <MessageCircle class="size-3" />
                    WhatsApp
                  </a>
                  <span>·</span>
                  <RouterLink
                    :to="{ name: 'loan-detail', params: { id: item.loanId } }"
                    class="font-mono hover:underline"
                  >
                    {{ item.loanId }}
                  </RouterLink>
                  <span>·</span>
                  <span>{{ item.productName }}</span>
                </span>
              </template>
            </TableGenerator>
          </div>
          <div
            v-if="receivablesPage.total > 0"
            class="flex items-center justify-between border-t p-4 text-xs text-muted-foreground"
          >
            <span>{{ receivablesPage.total }} cuotas</span>
            <div class="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                class="h-8"
                :disabled="receivablesPage.page <= 1 || receivableLoading"
                @click="setReceivablePage(receivablePageNumber - 1)"
              >
                <ChevronLeft class="size-4" />
              </Button>
              <span>Página {{ receivablesPage.page }} de {{ receivablePages }}</span>
              <Button
                variant="outline"
                size="sm"
                class="h-8"
                :disabled="receivablesPage.page >= receivablePages || receivableLoading"
                @click="setReceivablePage(receivablePageNumber + 1)"
              >
                <ChevronRight class="size-4" />
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </template>

    <CollectionActionDialog
      v-model:open="actionOpen"
      :row="selected"
      @saved="refreshAfterMutation"
    />
    <AssignCollectorDialog
      v-model:open="assignOpen"
      :row="selected"
      :collectors="collectors"
      @assigned="refreshAfterMutation"
    />
    <WriteOffDialog
      v-model:open="writeOffOpen"
      :row="selected"
      :approvers="collectors"
      @written-off="refreshAfterMutation"
    />
  </div>
</template>
