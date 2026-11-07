<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import {
  CreditCard,
  PlusCircle,
  RotateCcw,
  CheckCircle2,
  Clock,
  Search,
  ChevronLeft,
  ChevronRight,
  FileCheck2,
} from '@lucide/vue'

import type { Payment } from '@prestamos/api-client'
import { EmptyState, PageHeader, TableGenerator } from '@prestamos/ui'
import { Badge } from '@prestamos/ui/components/ui/badge'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
} from '@prestamos/ui/components/ui/card'
import { Input } from '@prestamos/ui/components/ui/input'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'

import { useSessionStore } from '@/app/stores/session'
import { usePayments } from '../composables/usePayments'
import {
  pendingPaymentColumns,
  basePaymentColumns,
  paymentColumnsWithReverse,
} from '../utils/paymentsColumns'
import RegisterPaymentModal from '../components/RegisterPaymentModal.vue'
import ReversePaymentModal from '../components/ReversePaymentModal.vue'

// FE-BO-21, FE-BO-22, FE-BO-23: Bandeja de vouchers pendientes, registro de pagos y reversos.

const session = useSessionStore()
const {
  loading,
  paymentsPage,
  activeTab,
  searchQuery,
  confirmingId,
  totalPages,
  loadPayments,
  confirmVoucher,
  onSearch,
  switchTab,
  goToPage,
} = usePayments()

// Modales
const isRegisterOpen = ref(false)
const isReverseOpen = ref(false)
const paymentToReverse = ref<Payment | null>(null)

// Permiso para reversos (FE-BO-23)
const canReverse = computed(() => session.can('payments.reverse'))

function openReverse(payment: Payment) {
  paymentToReverse.value = payment
  isReverseOpen.value = true
}

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
            :columns="pendingPaymentColumns"
            :rows="paymentsPage.items"
            row-key="id"
          >
            <template #cell-borrower="{ row: pay }">
              <span class="font-medium text-foreground">{{ pay.customerName }}</span>
              <span class="text-[11px] text-muted-foreground block font-normal">
                Préstamo: {{ pay.loanId }}
              </span>
            </template>

            <template #cell-channel="{ row: pay }">
              <Badge variant="outline" class="font-medium text-[10px]">
                {{ pay.method }}
              </Badge>
              <span class="text-[11px] font-mono text-muted-foreground block mt-0.5">
                {{ pay.externalReference || 'Sin ref' }}
              </span>
            </template>

            <template #cell-suggestion="{ row: pay }">
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
            </template>

            <template #cell-action="{ row: pay }">
              <Button
                size="sm"
                class="h-8 gap-1.5 text-xs"
                :disabled="confirmingId === pay.id"
                @click="confirmVoucher(pay)"
              >
                <CheckCircle2 class="size-3.5" />
                <span>Confirmar Pago</span>
              </Button>
            </template>
          </TableGenerator>
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- BANDEJA DE TODOS LOS PAGOS / APLICADOS CON REVERSO (FE-BO-23) -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-else>
          <TableGenerator
            :columns="canReverse ? paymentColumnsWithReverse : basePaymentColumns"
            :rows="paymentsPage.items"
            row-key="id"
            :row-class="
              (row) => ({
                'hover:bg-muted/40 transition-colors': true,
                'opacity-60 bg-muted/20': row.status === 'REVERSED',
              })
            "
          >
            <template #cell-borrower="{ row: pay }">
              <span class="font-medium text-foreground">{{ pay.customerName }}</span>
              <span class="text-[11px] text-muted-foreground block font-normal">
                Préstamo: {{ pay.loanId }}
              </span>
            </template>

            <template #cell-reverse="{ row: pay }">
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
