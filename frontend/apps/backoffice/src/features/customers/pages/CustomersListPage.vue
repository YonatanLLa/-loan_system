<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import {
  Users,
  Search,
  UserPlus,
  RefreshCw,
  ChevronLeft,
  ChevronRight,
  Eye,
  Pencil,
  X,
  ShieldCheck,
} from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { Customer, Page } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { EmptyState, MoneyText, PageHeader, StatusBadge, TableGenerator } from '@prestamos/ui'
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
import { formatInstant } from '@prestamos/utils'

import { useApi } from '@/app/api'
import CustomerFormSheet from '../components/CustomerFormSheet.vue'

// FE-BO-06: Lista y búsqueda de clientes por documento o nombre; alta y edición.

const loading = ref(false)
const customersPage = ref<Page<Customer>>({
  items: [],
  total: 0,
  page: 1,
  pageSize: 10,
})

const searchQuery = ref('')
const selectedKycStatus = ref('')
const currentPage = ref(1)

// Modal de Alta / Edición
const isFormOpen = ref(false)
const customerToEdit = ref<Customer | null>(null)

function openCreate() {
  customerToEdit.value = null
  isFormOpen.value = true
}

function openEdit(customer: Customer) {
  customerToEdit.value = customer
  isFormOpen.value = true
}

async function loadCustomers() {
  loading.value = true
  try {
    const params: Record<string, string | number> = {
      page: currentPage.value,
      pageSize: customersPage.value.pageSize,
    }
    if (searchQuery.value.trim()) params.q = searchQuery.value.trim()
    if (selectedKycStatus.value) params.kycStatus = selectedKycStatus.value

    const { data } = await useApi().get<Page<Customer>>('/customers', { params })
    customersPage.value = data
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar clientes', { description: apiError.detail })
  } finally {
    loading.value = false
  }
}

function onSearch() {
  currentPage.value = 1
  void loadCustomers()
}

function resetFilters() {
  searchQuery.value = ''
  selectedKycStatus.value = ''
  currentPage.value = 1
  void loadCustomers()
}

function goToPage(p: number) {
  if (p < 1 || p > totalPages.value) return
  currentPage.value = p
  void loadCustomers()
}

const totalPages = ref(1)
watch(
  () => customersPage.value.total,
  (total) => {
    totalPages.value = Math.max(1, Math.ceil(total / customersPage.value.pageSize))
  },
  { immediate: true },
)

function onCustomerSaved() {
  void loadCustomers()
}

onMounted(() => {
  void loadCustomers()
})
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Clientes y Expedientes KYC"
      description="Directorio de prestatarios, alta con validación de documento y acceso a Ficha 360 (FE-BO-06)."
    >
      <template #actions>
        <Button size="sm" @click="openCreate">
          <UserPlus class="size-4 mr-2" />
          Nuevo Cliente
        </Button>
      </template>
    </PageHeader>

    <!-- ── Filtros y Barra de Búsqueda ── -->
    <Card>
      <CardContent class="p-4">
        <form class="flex flex-col sm:flex-row items-center gap-3" @submit.prevent="onSearch">
          <div class="relative flex-1 w-full">
            <Search class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
            <Input
              v-model="searchQuery"
              placeholder="Buscar por documento, nombre o correo..."
              class="pl-9 h-9"
            />
          </div>

          <div class="w-full sm:w-48">
            <select
              v-model="selectedKycStatus"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              @change="onSearch"
            >
              <option value="">Todos los estados KYC</option>
              <option value="PENDING">Pendiente</option>
              <option value="VERIFIED">Verificado</option>
              <option value="REJECTED">Rechazado</option>
              <option value="EXPIRED">Vencido</option>
            </select>
          </div>

          <div class="flex items-center gap-2 w-full sm:w-auto shrink-0 justify-end">
            <Button
              v-if="searchQuery || selectedKycStatus"
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

    <!-- ── Tabla de Clientes ── -->
    <Card>
      <CardContent class="p-0">
        <!-- Cargando -->
        <div v-if="loading" class="p-6 space-y-4">
          <Skeleton class="h-10 w-full" />
          <Skeleton class="h-12 w-full" />
          <Skeleton class="h-12 w-full" />
          <Skeleton class="h-12 w-full" />
        </div>

        <!-- Sin resultados -->
        <div v-else-if="customersPage.items.length === 0" class="p-12">
          <EmptyState
            :icon="Users"
            title="No se encontraron clientes"
            description="Intenta buscar con otros términos o registra un nuevo cliente en el sistema."
          >
            <Button size="sm" @click="openCreate">
              <UserPlus class="size-4 mr-2" />
              Alta de Cliente
            </Button>
          </EmptyState>
        </div>

        <!-- Lista -->
        <div v-else>
          <TableGenerator
            :columns="[
              { key: 'document', label: 'Documento' },
              { key: 'customer', label: 'Cliente' },
              { key: 'contact', label: 'Contacto' },
              { key: 'income', label: 'Ingreso Neto' },
              { key: 'kyc', label: 'Estado KYC' },
              { key: 'createdAt', label: 'Registro' },
              { key: 'actions', label: 'Acciones', headerClass: 'text-right' },
            ]"
            :rows="customersPage.items"
            row-key="id"
          >
            <template #row="{ row: customer }">
              <td class="px-4 py-3 font-mono text-xs">
                <span class="font-semibold text-foreground">{{ customer.docType }}:</span>
                {{ customer.docNumber }}
              </td>
              <td class="px-4 py-3">
                <div class="font-medium text-foreground">{{ customer.fullName }}</div>
                <div class="text-xs text-muted-foreground">
                  {{ customer.occupation || customer.personType }}
                </div>
              </td>
              <td class="px-4 py-3 text-xs">
                <div>{{ customer.phone }}</div>
                <div class="text-muted-foreground truncate max-w-[180px]">{{ customer.email }}</div>
              </td>
              <td class="px-4 py-3 text-xs font-medium">
                <MoneyText :amount="customer.monthlyNetIncome" />
              </td>
              <td class="px-4 py-3">
                <StatusBadge kind="kyc" :status="customer.kycStatus" />
              </td>
              <td class="px-4 py-3 text-xs text-muted-foreground whitespace-nowrap">
                {{ formatInstant(customer.createdAt) }}
              </td>
              <td class="px-4 py-3 text-right">
                <div class="flex items-center justify-end gap-1">
                  <Button variant="ghost" size="sm" class="h-8 gap-1 text-xs" as-child>
                    <RouterLink :to="{ name: 'customer-detail', params: { id: customer.id } }">
                      <Eye class="size-3.5" />
                      <span>Ficha 360</span>
                    </RouterLink>
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    class="size-8 text-muted-foreground hover:text-foreground"
                    title="Editar datos del cliente"
                    @click="openEdit(customer)"
                  >
                    <Pencil class="size-3.5" />
                  </Button>
                </div>
              </td>
            </template>
          </TableGenerator>
        </div>

        <!-- Paginación -->
        <div
          v-if="customersPage.total > 0"
          class="flex flex-col sm:flex-row items-center justify-between gap-4 border-t p-4 text-xs text-muted-foreground"
        >
          <div>
            Mostrando
            <span class="font-medium text-foreground">
              {{ (customersPage.page - 1) * customersPage.pageSize + 1 }}
            </span>
            a
            <span class="font-medium text-foreground">
              {{ Math.min(customersPage.page * customersPage.pageSize, customersPage.total) }}
            </span>
            de
            <span class="font-medium text-foreground">{{ customersPage.total }}</span>
            clientes
          </div>

          <div class="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              class="h-8"
              :disabled="customersPage.page <= 1 || loading"
              @click="goToPage(customersPage.page - 1)"
            >
              <ChevronLeft class="size-4 mr-1" />
              Anterior
            </Button>
            <span class="px-2 font-medium text-foreground">
              Página {{ customersPage.page }} de {{ totalPages }}
            </span>
            <Button
              variant="outline"
              size="sm"
              class="h-8"
              :disabled="customersPage.page >= totalPages || loading"
              @click="goToPage(customersPage.page + 1)"
            >
              Siguiente
              <ChevronRight class="size-4 ml-1" />
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- ── Sheet de Alta / Edición ── -->
    <CustomerFormSheet
      v-model:open="isFormOpen"
      :customer-to-edit="customerToEdit"
      @saved="onCustomerSaved"
    />
  </div>
</template>
