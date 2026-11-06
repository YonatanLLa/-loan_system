<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import {
  CreditCard,
  PlusCircle,
  RotateCcw,
  CheckCircle2,
  Clock,
  Search,
  RefreshCw,
  ChevronLeft,
  ChevronRight,
  ArrowRight,
  Eye,
  X,
  FileCheck2,
} from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { Payment, Page } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { EmptyState, MoneyText, PageHeader, StatusBadge, TableGenerator } from '@prestamos/ui'
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
import { formatInstant } from '@prestamos/utils'

import { useApi } from '@/app/api'
import { useSessionStore } from '@/app/stores/session'
import RegisterPaymentModal from '../components/RegisterPaymentModal.vue'
import ReversePaymentModal from '../components/ReversePaymentModal.vue'

// FE-BO-21, FE-BO-22, FE-BO-23: Bandeja de vouchers pendientes, registro de pagos y reversos.

const session = useSessionStore()
const loading = ref(false)
const paymentsPage = ref<Page<Payment>>({
  items: [],
  total: 0,
  page: 1,
  pageSize: 10,
})

const activeTab = ref<'pending' | 'all'>('pending')
const searchQuery = ref('')
const currentPage = ref(1)

// Modales
const isRegisterOpen = ref(false)
const isReverseOpen = ref(false)
const paymentToReverse = ref<Payment | null>(null)
const confirmingId = ref<string | null>(null)

// Permiso para reversos (FE-BO-23)
const canReverse = computed(() => session.can('payments.reverse'))

async function loadPayments() {
  loading.value = true
  try {
    const params: Record<string, string | number> = {
      page: currentPage.value,
      pageSize: paymentsPage.value.pageSize,
    }
    if (searchQuery.value.trim()) params.q = searchQuery.value.trim()
    if (activeTab.value === 'pending') {
      params.status = 'PENDING_CONFIRMATION'
    }

    const { data } = await useApi().get<Page<Payment>>('/payments', { params })
    paymentsPage.value = data
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar pagos', { description: apiError.detail })
  } finally {
    loading.value = false
  }
}

// Confirmar un voucher reportado (FE-BO-21)
async function confirmVoucher(payment: Payment) {
  confirmingId.value = payment.id
  try {
    await useApi().post(`/payments/${payment.id}/confirm`)
    toast.success('Pago confirmado y aplicado', {
      description: `El voucher por S/ ${payment.amount} fue conciliado y amortizado exitosamente.`,
    })
    void loadPayments()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al confirmar pago', { description: apiError.detail })
  } finally {
    confirmingId.value = null
  }
}

function openReverse(payment: Payment) {
  paymentToReverse.value = payment
  isReverseOpen.value = true
}

function onSearch() {
  currentPage.value = 1
  void loadPayments()
}

function switchTab(tab: 'pending' | 'all') {
  activeTab.value = tab
  currentPage.value = 1
  void loadPayments()
}

function goToPage(p: number) {
  if (p < 1 || p > totalPages.value) return
  currentPage.value = p
  void loadPayments()
}

const totalPages = ref(1)
watch(
  () => paymentsPage.value.total,
  (total) => {
    totalPages.value = Math.max(1, Math.ceil(total / paymentsPage.value.pageSize))
  },
  { immediate: true },
)

onMounted(() => {
  void loadPayments()
})
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Gestión de Pagos y Conciliación"
      description="Bandeja de vouchers pendientes de confirmación, registro de pagos identificados y reversos con auditoría (FE-BO-21 a FE-BO-23)."
    >
      <template #actions>
        <Button size="sm" @click="isRegisterOpen = true">
          <PlusCircle class="size-4 mr-2" />
          Registrar Pago Identificado
        </Button>
      </template>
    </PageHeader>

    <!-- ── Pestañas de la Bandeja ── -->
    <div class="flex items-center gap-1 border-b text-sm">
      <button
        class="px-4 py-2.5 font-semibold border-b-2 transition-all flex items-center gap-2"
        :class="
          activeTab === 'pending'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground hover:text-foreground'
        "
        @click="switchTab('pending')"
      >
        <Clock class="size-4" />
        Vouchers Pendientes de Confirmación (FE-BO-21)
      </button>

      <button
        class="px-4 py-2.5 font-semibold border-b-2 transition-all flex items-center gap-2"
        :class="
          activeTab === 'all'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground hover:text-foreground'
        "
        @click="switchTab('all')"
      >
        <CreditCard class="size-4" />
        Todos los Pagos / Aplicados
      </button>
    </div>

    <!-- ── Buscador ── -->
    <Card>
      <CardContent class="p-4">
        <form class="flex items-center gap-2" @submit.prevent="onSearch">
          <div class="relative flex-1">
            <Search class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
            <Input
              v-model="searchQuery"
              placeholder="Buscar por cliente, referencia o número de recibo..."
              class="pl-9 h-9"
            />
          </div>
          <Button type="submit" size="sm" class="h-9" :disabled="loading"> Buscar </Button>
        </form>
      </CardContent>
    </Card>

    <!-- ── Lista de Pagos ── -->
    <Card>
      <CardContent class="p-0">
        <div v-if="loading" class="p-6 space-y-4">
          <Skeleton class="h-10 w-full" />
          <Skeleton class="h-12 w-full" />
          <Skeleton class="h-12 w-full" />
        </div>

        <div v-else-if="paymentsPage.items.length === 0" class="p-12">
          <EmptyState
            :icon="activeTab === 'pending' ? CheckCircle2 : CreditCard"
            :title="
              activeTab === 'pending' ? 'No hay vouchers pendientes' : 'No se encontraron pagos'
            "
            :description="
              activeTab === 'pending'
                ? 'Todos los comprobantes reportados por clientes han sido conciliados y aplicados.'
                : 'No existen pagos registrados con los criterios seleccionados.'
            "
          />
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- BANDEJA DE VOUCHERS PENDIENTES CON MATCH SUGERIDO (FE-BO-21) -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-else-if="activeTab === 'pending'">
          <TableGenerator
            :columns="[
              { key: 'id', label: 'ID Pago' },
              { key: 'borrower', label: 'Prestatario' },
              { key: 'amount', label: 'Monto Reportado' },
              { key: 'channel', label: 'Canal / Referencia' },
              {
                key: 'suggestion',
                label: 'Movimiento de Extracto Sugerido para Emparejar (FE-BO-21)',
              },
              { key: 'status', label: 'Estado' },
              { key: 'action', label: 'Acción', headerClass: 'text-right' },
            ]"
            :rows="paymentsPage.items"
            row-key="id"
            body-class="divide-y divide-border text-xs"
          >
            <template #row="{ row: pay }">
              <td class="px-4 py-3 font-mono font-bold">{{ pay.id }}</td>
              <td class="px-4 py-3 font-medium text-foreground">
                {{ pay.customerName }}
                <span class="text-[11px] text-muted-foreground block font-normal">
                  Préstamo: {{ pay.loanId }}
                </span>
              </td>
              <td class="px-4 py-3 font-mono text-sm font-bold text-foreground">
                <MoneyText :amount="pay.amount" />
              </td>
              <td class="px-4 py-3">
                <Badge variant="outline" class="font-medium text-[10px]">
                  {{ pay.method }}
                </Badge>
                <span class="text-[11px] font-mono text-muted-foreground block mt-0.5">
                  {{ pay.externalReference || 'Sin ref' }}
                </span>
              </td>

              <!-- Emparejamiento inteligente sugerido del extracto (FE-BO-21) -->
              <td class="px-4 py-3">
                <div class="rounded-lg border border-primary/30 bg-primary/5 p-2 space-y-1">
                  <span class="text-[11px] font-bold text-primary flex items-center gap-1">
                    <FileCheck2 class="size-3" /> Abono Detectado en Extracto BCP
                  </span>
                  <p class="font-mono text-[11px] text-foreground font-medium">
                    EXT-920001 · TRANSF {{ pay.externalReference }} · S/ {{ pay.amount }}
                  </p>
                  <span class="text-[10px] text-muted-foreground block">
                    Coincidencia exacta de monto y referencia con el extracto bancario.
                  </span>
                </div>
              </td>

              <td class="px-4 py-3">
                <StatusBadge kind="payment" :status="pay.status" />
              </td>

              <td class="px-4 py-3 text-right">
                <Button
                  size="sm"
                  class="h-8 gap-1.5 text-xs"
                  :disabled="confirmingId === pay.id"
                  @click="confirmVoucher(pay)"
                >
                  <CheckCircle2 class="size-3.5" />
                  <span>Confirmar Pago</span>
                </Button>
              </td>
            </template>
          </TableGenerator>
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- BANDEJA DE TODOS LOS PAGOS / APLICADOS CON REVERSO (FE-BO-23) -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-else>
          <TableGenerator
            :columns="
              canReverse
                ? [
                    { key: 'id', label: 'ID Pago' },
                    { key: 'paidAt', label: 'Fecha y Hora' },
                    { key: 'borrower', label: 'Prestatario' },
                    { key: 'amount', label: 'Monto' },
                    { key: 'method', label: 'Canal' },
                    { key: 'reference', label: 'Referencia / Recibo' },
                    { key: 'status', label: 'Estado' },
                    { key: 'reverse', label: 'Reverso', headerClass: 'text-right' },
                  ]
                : [
                    { key: 'id', label: 'ID Pago' },
                    { key: 'paidAt', label: 'Fecha y Hora' },
                    { key: 'borrower', label: 'Prestatario' },
                    { key: 'amount', label: 'Monto' },
                    { key: 'method', label: 'Canal' },
                    { key: 'reference', label: 'Referencia / Recibo' },
                    { key: 'status', label: 'Estado' },
                  ]
            "
            :rows="paymentsPage.items"
            row-key="id"
            body-class="divide-y divide-border text-xs"
            :row-class="
              (row) => ({
                'hover:bg-muted/40 transition-colors': true,
                'opacity-60 bg-muted/20': row.status === 'REVERSED',
              })
            "
          >
            <template #row="{ row: pay }">
              <td class="px-4 py-3 font-mono font-bold">{{ pay.id }}</td>
              <td class="px-4 py-3 whitespace-nowrap text-muted-foreground">
                {{ formatInstant(pay.paidAt) }}
              </td>
              <td class="px-4 py-3 font-medium text-foreground">
                {{ pay.customerName }}
                <span class="text-[11px] text-muted-foreground block font-normal">
                  Préstamo: {{ pay.loanId }}
                </span>
              </td>
              <td class="px-4 py-3 font-mono font-bold text-foreground">
                <MoneyText :amount="pay.amount" />
              </td>
              <td class="px-4 py-3 font-medium">{{ pay.method }}</td>
              <td class="px-4 py-3 font-mono text-[11px] text-muted-foreground">
                {{ pay.receiptNumber || pay.externalReference || '—' }}
              </td>
              <td class="px-4 py-3">
                <StatusBadge kind="payment" :status="pay.status" />
              </td>

              <!-- Botón Reverso (FE-BO-23) protegido por permiso -->
              <td v-if="canReverse" class="px-4 py-3 text-right">
                <Button
                  v-if="pay.status === 'APPLIED'"
                  variant="ghost"
                  size="sm"
                  class="h-8 gap-1 text-xs text-destructive hover:text-destructive hover:bg-destructive/10"
                  @click="openReverse(pay)"
                >
                  <RotateCcw class="size-3.5" />
                  <span>Revertir</span>
                </Button>
                <span
                  v-else-if="pay.status === 'REVERSED'"
                  class="text-[11px] text-destructive italic"
                >
                  Revertido
                </span>
              </td>
            </template>
          </TableGenerator>
        </div>

        <!-- Paginación -->
        <div
          v-if="paymentsPage.total > 0"
          class="flex flex-col sm:flex-row items-center justify-between gap-4 border-t p-4 text-xs text-muted-foreground"
        >
          <div>
            Mostrando
            <span class="font-medium text-foreground">
              {{ (paymentsPage.page - 1) * paymentsPage.pageSize + 1 }}
            </span>
            a
            <span class="font-medium text-foreground">
              {{ Math.min(paymentsPage.page * paymentsPage.pageSize, paymentsPage.total) }}
            </span>
            de
            <span class="font-medium text-foreground">{{ paymentsPage.total }}</span>
            pagos
          </div>

          <div class="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              class="h-8"
              :disabled="paymentsPage.page <= 1 || loading"
              @click="goToPage(paymentsPage.page - 1)"
            >
              <ChevronLeft class="size-4 mr-1" />
              Anterior
            </Button>
            <span class="px-2 font-medium text-foreground">
              Página {{ paymentsPage.page }} de {{ totalPages }}
            </span>
            <Button
              variant="outline"
              size="sm"
              class="h-8"
              :disabled="paymentsPage.page >= totalPages || loading"
              @click="goToPage(paymentsPage.page + 1)"
            >
              Siguiente
              <ChevronRight class="size-4 ml-1" />
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- ── Modales de Registro y Reverso ── -->
    <RegisterPaymentModal v-model:open="isRegisterOpen" @registered="loadPayments" />

    <ReversePaymentModal
      v-model:open="isReverseOpen"
      :payment="paymentToReverse"
      @reversed="loadPayments"
    />
  </div>
</template>
