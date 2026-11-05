<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
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
} from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { AgingBucket, AgingRow, LoanProduct, Page, ReceivableItem } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { EmptyState, MoneyText, PageHeader, StatusBadge } from '@prestamos/ui'
import { Badge } from '@prestamos/ui/components/ui/badge'
import { Button } from '@prestamos/ui/components/ui/button'
import { Card, CardContent } from '@prestamos/ui/components/ui/card'
import { Input } from '@prestamos/ui/components/ui/input'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import { formatBusinessDate } from '@prestamos/utils'

import { useApi } from '@/app/api'
import AssignCollectorDialog, { type CollectorOption } from '../components/AssignCollectorDialog.vue'
import CollectionActionDialog from '../components/CollectionActionDialog.vue'
import WriteOffDialog from '../components/WriteOffDialog.vue'

// FE-BO-32 a FE-BO-36: aging, gestiones, asignación, castigo y cuentas por cobrar.

const BUCKETS: { id: AgingBucket | ''; label: string }[] = [
  { id: '', label: 'Todos' },
  { id: 'PREVENTIVO', label: 'Preventivo' },
  { id: 'TRAMO_1', label: '1–30' },
  { id: 'TRAMO_2', label: '31–60' },
  { id: 'TRAMO_3', label: '61–90' },
  { id: 'TRAMO_4', label: '> 90' },
]

const BUCKET_LABEL: Record<AgingBucket, string> = {
  PREVENTIVO: 'Preventivo',
  TRAMO_1: '1–30 días',
  TRAMO_2: '31–60 días',
  TRAMO_3: '61–90 días',
  TRAMO_4: 'Más de 90 días',
}

const selectClass =
  'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring'

const activeTab = ref<'aging' | 'receivables'>('aging')
const loading = ref(false)

const agingPage = ref<Page<AgingRow>>({ items: [], total: 0, page: 1, pageSize: 10 })
const bucket = ref<AgingBucket | ''>('')
const assignedTo = ref('')
const productName = ref('')
const minAmount = ref('')
const maxAmount = ref('')
const agingQuery = ref('')
const agingPageNumber = ref(1)
const bucketCounts = ref<Record<string, number>>({})

const receivablesPage = ref<Page<ReceivableItem>>({ items: [], total: 0, page: 1, pageSize: 10 })
const receivableType = ref<'' | 'VENCIDA' | 'PENDIENTE'>('')
const receivableQuery = ref('')
const receivablePageNumber = ref(1)

const collectors = ref<CollectorOption[]>([])
const products = ref<LoanProduct[]>([])

const actionOpen = ref(false)
const assignOpen = ref(false)
const writeOffOpen = ref(false)
const selected = ref<AgingRow | null>(null)

const agingPages = computed(() =>
  Math.max(1, Math.ceil(agingPage.value.total / agingPage.value.pageSize)),
)
const receivablePages = computed(() =>
  Math.max(1, Math.ceil(receivablesPage.value.total / receivablesPage.value.pageSize)),
)

async function loadCatalogs() {
  try {
    const [people, catalog] = await Promise.all([
      useApi().get<CollectorOption[]>('/collections/collectors'),
      useApi().get<LoanProduct[]>('/loan-products', { params: { status: 'ACTIVO' } }),
    ])
    collectors.value = people.data
    products.value = catalog.data
  } catch (error) {
    toast.error('No se pudieron cargar los filtros', { description: toApiError(error).detail })
  }
}

async function loadBucketCounts() {
  try {
    const { data } = await useApi().get<Page<AgingRow>>('/collections/aging', {
      params: { page: 1, pageSize: 100 },
    })
    const counts: Record<string, number> = { '': data.total }
    for (const row of data.items) counts[row.bucket] = (counts[row.bucket] ?? 0) + 1
    bucketCounts.value = counts
  } catch {
    bucketCounts.value = {}
  }
}

async function loadAging() {
  loading.value = true
  try {
    const params: Record<string, string | number> = {
      page: agingPageNumber.value,
      pageSize: agingPage.value.pageSize,
    }
    if (bucket.value) params.bucket = bucket.value
    if (assignedTo.value) params.assignedTo = assignedTo.value
    if (productName.value) params.productName = productName.value
    if (minAmount.value.trim()) params.minAmount = minAmount.value.trim()
    if (maxAmount.value.trim()) params.maxAmount = maxAmount.value.trim()
    if (agingQuery.value.trim()) params.q = agingQuery.value.trim()
    const { data } = await useApi().get<Page<AgingRow>>('/collections/aging', { params })
    agingPage.value = data
  } catch (error) {
    toast.error('Error al cargar el aging', { description: toApiError(error).detail })
  } finally {
    loading.value = false
  }
}

async function loadReceivables() {
  loading.value = true
  try {
    const params: Record<string, string | number> = {
      page: receivablePageNumber.value,
      pageSize: receivablesPage.value.pageSize,
    }
    if (receivableType.value) params.type = receivableType.value
    if (receivableQuery.value.trim()) params.q = receivableQuery.value.trim()
    const { data } = await useApi().get<Page<ReceivableItem>>('/collections/receivables', { params })
    receivablesPage.value = data
  } catch (error) {
    toast.error('Error al cargar cuentas por cobrar', { description: toApiError(error).detail })
  } finally {
    loading.value = false
  }
}

function applyAgingFilters() {
  agingPageNumber.value = 1
  void loadAging()
}

function chooseBucket(next: AgingBucket | '') {
  bucket.value = next
  applyAgingFilters()
}

function applyReceivableFilters() {
  receivablePageNumber.value = 1
  void loadReceivables()
}

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
  await Promise.all([loadAging(), loadBucketCounts()])
}

onMounted(() => {
  void loadCatalogs()
  void loadAging()
  void loadBucketCounts()
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
        :class="activeTab === 'aging' ? 'border-primary text-primary' : 'border-transparent text-muted-foreground'"
        @click="activeTab = 'aging'"
      >
        <PhoneCall class="size-4" />
        Aging
      </button>
      <button
        type="button"
        class="flex items-center gap-2 border-b-2 px-4 py-2.5 font-semibold transition-all"
        :class="activeTab === 'receivables' ? 'border-primary text-primary' : 'border-transparent text-muted-foreground'"
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
              <Search class="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input v-model="agingQuery" class="h-9 pl-9" placeholder="Cliente, teléfono o préstamo" />
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
              <Button type="submit" size="sm" class="h-9" :disabled="loading">Filtrar</Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardContent class="p-0">
          <div v-if="loading" class="space-y-3 p-6">
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
          <div v-else class="overflow-x-auto">
            <table class="w-full text-left text-sm">
              <thead class="border-b bg-muted/60 text-xs font-medium text-muted-foreground">
                <tr>
                  <th class="px-4 py-3">Cliente</th>
                  <th class="px-4 py-3">Tramo</th>
                  <th class="px-4 py-3">Atraso</th>
                  <th class="px-4 py-3">Cuotas vencidas</th>
                  <th class="px-4 py-3">Capital impago</th>
                  <th class="px-4 py-3">Total exigible</th>
                  <th class="px-4 py-3">Gestor</th>
                  <th class="px-4 py-3 text-right">Acciones</th>
                </tr>
              </thead>
              <tbody class="divide-y text-xs">
                <tr
                  v-for="row in agingPage.items"
                  :key="row.loanId"
                  class="hover:bg-muted/40"
                  :class="row.hasBrokenPromise ? 'bg-destructive/5' : ''"
                >
                  <td class="px-4 py-3">
                    <RouterLink
                      :to="{ name: 'customer-detail', params: { id: row.customerId } }"
                      class="font-medium text-foreground hover:underline"
                    >
                      {{ row.customerName }}
                    </RouterLink>
                    <span class="block text-[11px] text-muted-foreground">
                      {{ row.phone }} ·
                      <RouterLink :to="{ name: 'loan-detail', params: { id: row.loanId } }" class="hover:underline">
                        {{ row.loanId }}
                      </RouterLink>
                    </span>
                    <span class="mt-1 block text-[11px] text-muted-foreground">{{ row.productName }}</span>
                    <Badge
                      v-if="row.hasBrokenPromise"
                      variant="outline"
                      class="mt-1 border-destructive/40 bg-destructive/10 text-destructive"
                    >
                      <AlertTriangle class="mr-1 size-3" />
                      Promesa incumplida
                    </Badge>
                  </td>
                  <td class="px-4 py-3">
                    <Badge variant="outline">{{ BUCKET_LABEL[row.bucket] }}</Badge>
                  </td>
                  <td class="px-4 py-3 font-medium">{{ row.daysLate }} días</td>
                  <td class="px-4 py-3">{{ row.overdueInstallments }}</td>
                  <td class="px-4 py-3"><MoneyText :amount="row.overduePrincipal" /></td>
                  <td class="px-4 py-3 font-semibold"><MoneyText :amount="row.totalDue" /></td>
                  <td class="px-4 py-3">{{ row.assignedTo || 'Sin asignar' }}</td>
                  <td class="px-4 py-3">
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
                  </td>
                </tr>
              </tbody>
            </table>
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
                :disabled="agingPage.page <= 1 || loading"
                @click="agingPageNumber = agingPage.page - 1; loadAging()"
              >
                <ChevronLeft class="size-4" />
              </Button>
              <span>Página {{ agingPage.page }} de {{ agingPages }}</span>
              <Button
                variant="outline"
                size="sm"
                class="h-8"
                :disabled="agingPage.page >= agingPages || loading"
                @click="agingPageNumber = agingPage.page + 1; loadAging()"
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
              <Search class="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input v-model="receivableQuery" class="h-9 pl-9" placeholder="Cliente, préstamo o producto" />
            </div>
            <select v-model="receivableType" :class="selectClass + ' w-auto'" aria-label="Estado de la cuota">
              <option value="">Próximos vencimientos y vencidas</option>
              <option value="VENCIDA">Solo cuotas vencidas</option>
              <option value="PENDIENTE">Solo por vencer</option>
            </select>
            <Button type="submit" size="sm" class="h-9" :disabled="loading">Filtrar</Button>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardContent class="p-0">
          <div v-if="loading" class="space-y-3 p-6">
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
          <div v-else class="overflow-x-auto">
            <table class="w-full text-left text-sm">
              <thead class="border-b bg-muted/60 text-xs font-medium text-muted-foreground">
                <tr>
                  <th class="px-4 py-3">Cliente</th>
                  <th class="px-4 py-3">Cuota</th>
                  <th class="px-4 py-3">Vencimiento</th>
                  <th class="px-4 py-3">Atraso</th>
                  <th class="px-4 py-3">Saldo de la cuota</th>
                  <th class="px-4 py-3">Capital pendiente del préstamo</th>
                  <th class="px-4 py-3">Estado</th>
                </tr>
              </thead>
              <tbody class="divide-y text-xs">
                <tr v-for="item in receivablesPage.items" :key="`${item.loanId}-${item.installmentNumber}`">
                  <td class="px-4 py-3">
                    <span class="font-medium text-foreground">{{ item.customerName }}</span>
                    <span class="block text-[11px] text-muted-foreground">
                      {{ item.phone }} · {{ item.loanId }} · {{ item.productName }}
                    </span>
                  </td>
                  <td class="px-4 py-3">N.º {{ item.installmentNumber }}</td>
                  <td class="px-4 py-3">{{ formatBusinessDate(item.dueDate) }}</td>
                  <td class="px-4 py-3">{{ item.daysLate > 0 ? `${item.daysLate} días` : '—' }}</td>
                  <td class="px-4 py-3 font-semibold"><MoneyText :amount="item.amount" /></td>
                  <td class="px-4 py-3"><MoneyText :amount="item.outstandingPrincipal" /></td>
                  <td class="px-4 py-3">
                    <StatusBadge kind="receivable" :status="item.status" />
                  </td>
                </tr>
              </tbody>
            </table>
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
                :disabled="receivablesPage.page <= 1 || loading"
                @click="receivablePageNumber = receivablesPage.page - 1; loadReceivables()"
              >
                <ChevronLeft class="size-4" />
              </Button>
              <span>Página {{ receivablesPage.page }} de {{ receivablePages }}</span>
              <Button
                variant="outline"
                size="sm"
                class="h-8"
                :disabled="receivablesPage.page >= receivablePages || loading"
                @click="receivablePageNumber = receivablesPage.page + 1; loadReceivables()"
              >
                <ChevronRight class="size-4" />
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </template>

    <CollectionActionDialog v-model:open="actionOpen" :row="selected" @saved="refreshAfterMutation" />
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
