<script setup lang="ts">
import { ref, onMounted } from 'vue'
import {
  Package,
  PackagePlus,
  RefreshCw,
  Pencil,
  Percent,
  Calendar,
  Layers,
  ShieldAlert,
} from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { LoanProduct } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { EmptyState, MoneyText, PageHeader } from '@prestamos/ui'
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

import { useApi } from '@/app/api'
import ProductFormSheet from '../components/ProductFormSheet.vue'

// FE-BO-11 y FE-BO-12: Catálogo de productos de crédito y administración de condiciones.

const loading = ref(false)
const products = ref<LoanProduct[]>([])
const filterStatus = ref('')

const isFormOpen = ref(false)
const productToEdit = ref<LoanProduct | null>(null)

async function loadProducts() {
  loading.value = true
  try {
    const params: Record<string, string> = {}
    if (filterStatus.value) params.status = filterStatus.value
    const { data } = await useApi().get<LoanProduct[]>('/loan-products', { params })
    products.value = data
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar productos', { description: apiError.detail })
  } finally {
    loading.value = false
  }
}

function openCreate() {
  productToEdit.value = null
  isFormOpen.value = true
}

function openEdit(p: LoanProduct) {
  productToEdit.value = p
  isFormOpen.value = true
}

function formatPercent(decimalStr: string): string {
  const val = Number(decimalStr) * 100
  return `${val.toFixed(2)}%`
}

onMounted(() => {
  void loadProducts()
})
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Catálogo de Productos de Crédito"
      description="Políticas de amortización, tasas TEA, comisiones, mora y DTI máximo permitido (FE-BO-11 y FE-BO-12)."
    >
      <template #actions>
        <Button size="sm" @click="openCreate">
          <PackagePlus class="size-4 mr-2" />
          Nuevo Producto
        </Button>
      </template>
    </PageHeader>

    <!-- ── Filtros ── -->
    <div class="flex items-center justify-between gap-4">
      <div class="flex items-center gap-2">
        <span class="text-xs font-medium text-muted-foreground">Estado:</span>
        <select
          v-model="filterStatus"
          class="flex h-8 rounded-md border border-input bg-transparent px-2.5 py-1 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
          @change="loadProducts"
        >
          <option value="">Todos los productos</option>
          <option value="ACTIVO">Solo Activos</option>
          <option value="INACTIVO">Solo Inactivos</option>
        </select>
      </div>

      <Button variant="ghost" size="sm" :disabled="loading" @click="loadProducts">
        <RefreshCw class="size-3.5 mr-1.5" :class="{ 'animate-spin': loading }" />
        Actualizar
      </Button>
    </div>

    <!-- ── Carga / Vacío / Lista de Productos ── -->
    <div v-if="loading" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
      <Skeleton class="h-64 rounded-xl" />
      <Skeleton class="h-64 rounded-xl" />
      <Skeleton class="h-64 rounded-xl" />
    </div>

    <div v-else-if="products.length === 0" class="py-12">
      <EmptyState
        :icon="Package"
        title="No hay productos configurados"
        description="Registra un nuevo producto para habilitar la originación y simulación de préstamos."
      >
        <Button size="sm" @click="openCreate">
          <PackagePlus class="size-4 mr-2" />
          Crear Producto
        </Button>
      </EmptyState>
    </div>

    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
      <Card
        v-for="p in products"
        :key="p.id"
        class="flex flex-col justify-between transition-all hover:border-primary/50"
        :class="{ 'opacity-70 bg-muted/20': p.status === 'INACTIVO' }"
      >
        <CardHeader class="pb-3">
          <div class="flex items-start justify-between gap-2">
            <div>
              <div class="flex items-center gap-2">
                <Badge variant="outline" class="font-mono text-[10px]">
                  {{ p.currency }}
                </Badge>
                <Badge
                  variant="outline"
                  :class="
                    p.status === 'ACTIVO'
                      ? 'border-success/30 bg-success/10 text-success'
                      : 'border-border bg-muted text-muted-foreground'
                  "
                  class="text-[10px]"
                >
                  {{ p.status }}
                </Badge>
              </div>
              <CardTitle class="text-lg font-bold mt-2">{{ p.name }}</CardTitle>
            </div>
            <Button
              variant="ghost"
              size="icon"
              class="size-8 text-muted-foreground hover:text-foreground shrink-0"
              title="Editar producto"
              @click="openEdit(p)"
            >
              <Pencil class="size-4" />
            </Button>
          </div>
          <CardDescription class="text-xs line-clamp-2 mt-1">
            {{ p.description || 'Sin descripción configurada.' }}
          </CardDescription>
        </CardHeader>

        <CardContent class="space-y-4 pt-0 text-xs">
          <!-- Montos y Plazos -->
          <div class="rounded-lg bg-muted/40 p-3 space-y-2 border">
            <div class="flex justify-between items-center">
              <span class="text-muted-foreground">Rango de Monto:</span>
              <span class="font-semibold text-foreground">
                <MoneyText :amount="p.minAmount" :currency="p.currency" /> –
                <MoneyText :amount="p.maxAmount" :currency="p.currency" />
              </span>
            </div>
            <div class="flex justify-between items-center">
              <span class="text-muted-foreground">Plazos permitidos:</span>
              <span class="font-semibold text-foreground">
                {{ p.minTerm }} a {{ p.maxTerm }} cuotas
              </span>
            </div>
            <div class="flex justify-between items-center">
              <span class="text-muted-foreground">Amortización:</span>
              <span class="font-medium text-primary">{{ p.amortizationMethod }}</span>
            </div>
          </div>

          <!-- Tasas y Comisiones -->
          <div class="space-y-1.5">
            <div class="flex justify-between items-center">
              <span class="text-muted-foreground flex items-center gap-1">
                <Percent class="size-3 text-primary" /> TEA Anual:
              </span>
              <span class="font-bold text-foreground">{{ formatPercent(p.annualRate) }}</span>
            </div>
            <div class="flex justify-between items-center">
              <span class="text-muted-foreground">Comisión Apertura:</span>
              <span>
                {{ formatPercent(p.openingFeeRate) }} ({{ p.openingFeeMode.toLowerCase() }})
              </span>
            </div>
            <div class="flex justify-between items-center">
              <span class="text-muted-foreground">Mora diaria:</span>
              <span>{{ formatPercent(p.moraRatePerDay) }} / día</span>
            </div>
            <div class="flex justify-between items-center">
              <span class="text-muted-foreground">DTI Máx (Auto / Manual):</span>
              <span class="font-semibold text-primary">
                {{ formatPercent(p.maxDtiAuto) }} / {{ formatPercent(p.maxDtiManual) }}
              </span>
            </div>
          </div>

          <!-- Frecuencias -->
          <div class="pt-2 border-t">
            <span class="text-[11px] text-muted-foreground block mb-1.5">Frecuencias:</span>
            <div class="flex flex-wrap gap-1">
              <Badge
                v-for="freq in p.allowedFrequencies"
                :key="freq"
                variant="outline"
                class="text-[10px] font-medium"
              >
                {{ freq }}
              </Badge>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- ── Sheet de Alta / Edición ── -->
    <ProductFormSheet
      v-model:open="isFormOpen"
      :product-to-edit="productToEdit"
      @saved="loadProducts"
    />
  </div>
</template>
