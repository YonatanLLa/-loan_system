<script setup lang="ts">
import { onMounted, watch } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import {
  Calendar,
  Coins,
  Receipt,
  Wallet,
} from '@lucide/vue'

import { MoneyText, StatusBadge } from '@prestamos/ui'
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
import { formatBusinessDate } from '@prestamos/utils'

import { useClientLoans, useLoanSchedule } from '../composables'

// FE-CP-11: Cronograma con colores (verde pagada, amarillo próxima, rojo vencida, azul parcial).
// FE-CP-15 y FE-CP-16: Enlaces a estado de cuenta y cotización de prepago total.

const route = useRoute()
const {
  loans,
  selectedLoanId,
  activeLoan,
  hasLoans,
  isLoading: loansLoading,
  loadLoans,
} = useClientLoans()

const {
  schedule,
  scheduleVersion,
  totalMoraAccrued,
  dueCountdownText,
  isLoading: scheduleLoading,
  getInstallmentColorClass,
  getInstallmentStatusLabel,
  loadSchedule,
} = useLoanSchedule()

// Sincroniza el préstamo seleccionado
watch(
  () => route.query.loanId,
  (queryId) => {
    if (queryId && typeof queryId === 'string') {
      selectedLoanId.value = queryId
    }
  },
  { immediate: true },
)

watch(selectedLoanId, (loanId) => {
  if (loanId) {
    void loadSchedule(loanId)
  }
})

onMounted(async () => {
  await loadLoans()
  if (activeLoan.value?.id) {
    void loadSchedule(activeLoan.value.id)
  }
})
</script>

<template>
  <div class="space-y-4 py-2">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div>
        <h2 class="text-xl font-bold">Mis Préstamos</h2>
        <p class="text-xs text-muted-foreground">
          Cronograma oficial de pagos y amortización (FE-CP-11).
        </p>
      </div>

      <!-- Selector de préstamos si tiene varios -->
      <div v-if="loans.length > 1">
        <select
          v-model="selectedLoanId"
          class="flex h-8 rounded-md border border-input bg-card px-2.5 py-1 text-xs shadow-xs focus:ring-1 focus:ring-primary"
        >
          <option v-for="l in loans" :key="l.id" :value="l.id">
            #{{ l.id }} ({{ l.productName }})
          </option>
        </select>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="loansLoading" class="space-y-3 p-4">
      <Skeleton class="h-28 w-full rounded-xl" />
      <Skeleton class="h-64 w-full rounded-xl" />
    </div>

    <!-- Si no tiene préstamos -->
    <div v-else-if="!hasLoans" class="py-12 text-center space-y-4">
      <div class="mx-auto size-14 rounded-2xl bg-primary/10 flex items-center justify-center text-primary">
        <Receipt class="size-7" />
      </div>
      <div class="space-y-1">
        <h3 class="font-bold text-base">No tienes préstamos registrados</h3>
        <p class="text-xs text-muted-foreground max-w-xs mx-auto">
          Solicita tu crédito digital para ver tu cronograma y pagar tus cuotas en línea.
        </p>
      </div>
      <Button as-child size="sm" class="h-9 text-xs">
        <RouterLink to="/solicitar">Solicitar Préstamo</RouterLink>
      </Button>
    </div>

    <!-- Detalle del Préstamo y Cronograma Semaforizado -->
    <template v-else-if="activeLoan">
      <!-- Tarjeta Resumen del Crédito -->
      <Card class="shadow-md border-border">
        <CardContent class="p-4 space-y-3 text-xs">
          <div class="flex items-center justify-between">
            <div>
              <span class="font-bold text-sm text-foreground">#{{ activeLoan.id }} · {{ activeLoan.productName }}</span>
              <span class="block text-[11px] text-muted-foreground font-mono">
                Versión de cronograma: v{{ scheduleVersion }}
              </span>
            </div>
            <StatusBadge kind="loan" :status="activeLoan.status" />
          </div>

          <div class="grid grid-cols-2 sm:grid-cols-3 gap-2 pt-1 text-center">
            <div class="p-2 rounded-lg bg-muted/20 border">
              <span class="text-[10px] text-muted-foreground block">Monto Inicial:</span>
              <span class="font-semibold text-foreground font-mono"><MoneyText :amount="activeLoan.principal" /></span>
            </div>
            <div class="p-2 rounded-lg bg-primary/10 border border-primary/20">
              <span class="text-[10px] text-primary font-semibold block">Saldo Insoluto:</span>
              <span class="font-bold text-primary font-mono"><MoneyText :amount="activeLoan.outstandingPrincipal" /></span>
            </div>
            <div class="p-2 rounded-lg bg-muted/20 border">
              <span class="text-[10px] text-muted-foreground block">TEA Aplicada:</span>
              <span class="font-semibold text-foreground font-mono">{{ (Number(activeLoan.annualRate) * 100).toFixed(1) }}%</span>
            </div>

            <!-- FE-CP-10: Días restantes o de atraso -->
            <div
              class="p-2 rounded-lg border"
              :class="
                activeLoan.status === 'EN_MORA'
                  ? 'bg-destructive/10 border-destructive/30'
                  : 'bg-amber-500/10 border-amber-500/30'
              "
            >
              <span
                class="text-[10px] font-semibold block"
                :class="activeLoan.status === 'EN_MORA' ? 'text-destructive' : 'text-amber-700'"
              >
                {{ activeLoan.status === 'EN_MORA' ? 'Días de Atraso:' : 'Próximo Vencimiento:' }}
              </span>
              <span
                class="font-bold text-xs"
                :class="activeLoan.status === 'EN_MORA' ? 'text-destructive' : 'text-amber-700'"
              >
                {{ dueCountdownText }}
              </span>
            </div>

            <!-- FE-CP-10: Mora acumulada del día -->
            <div
              class="p-2 rounded-lg border"
              :class="
                Number(totalMoraAccrued) > 0
                  ? 'bg-destructive/10 border-destructive/30'
                  : 'bg-muted/20 border'
              "
            >
              <span
                class="text-[10px] font-semibold block"
                :class="Number(totalMoraAccrued) > 0 ? 'text-destructive' : 'text-muted-foreground'"
              >
                Mora Acumulada:
              </span>
              <span
                class="font-bold font-mono text-xs"
                :class="Number(totalMoraAccrued) > 0 ? 'text-destructive' : 'text-muted-foreground'"
              >
                <MoneyText :amount="totalMoraAccrued" />
              </span>
            </div>

            <div class="p-2 rounded-lg bg-muted/20 border">
              <span class="text-[10px] text-muted-foreground block">Plazo de Pago:</span>
              <span class="font-semibold text-foreground">{{ activeLoan.term }} meses</span>
            </div>
          </div>

          <!-- Atajos de Autoservicio del Crédito -->
          <div class="flex flex-wrap gap-2 pt-2 border-t">
            <Button as-child size="sm" class="h-8 text-xs flex-1">
              <RouterLink :to="{ path: '/pagar', query: { loanId: activeLoan.id } }">
                <Wallet class="size-3.5 mr-1.5" />
                Pagar Cuota
              </RouterLink>
            </Button>
            <Button as-child variant="outline" size="sm" class="h-8 text-xs flex-1">
              <RouterLink :to="{ path: '/pagar', query: { loanId: activeLoan.id, mode: 'PREPAYMENT' } }">
                <Coins class="size-3.5 mr-1.5" />
                Prepago Total (FE-CP-16)
              </RouterLink>
            </Button>
          </div>
        </CardContent>
      </Card>

      <!-- ══════════════════════════════════════════════════════════ -->
      <!-- CRONOGRAMA SEMAFORIZADO POR COLORES (FE-CP-11)            -->
      <!-- ══════════════════════════════════════════════════════════ -->
      <Card class="shadow-md border-border overflow-hidden">
        <CardHeader class="pb-2 border-b bg-muted/10">
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
            <div>
              <CardTitle class="text-base font-bold flex items-center gap-2">
                <Calendar class="size-4 text-primary" />
                Cronograma Oficial de Cuotas
              </CardTitle>
              <CardDescription class="text-xs">
                Estados señalizados conforme a la normativa FE-CP-11.
              </CardDescription>
            </div>

            <!-- Leyenda de Colores (FE-CP-11) -->
            <div class="flex flex-wrap items-center gap-1.5 text-[10px]">
              <span class="flex items-center gap-1 px-1.5 py-0.5 rounded bg-success/10 text-success font-medium border border-success/30">
                <span class="size-1.5 rounded-full bg-success" />
                Pagada
              </span>
              <span class="flex items-center gap-1 px-1.5 py-0.5 rounded bg-amber-500/10 text-amber-700 font-medium border border-amber-500/30">
                <span class="size-1.5 rounded-full bg-amber-500" />
                Próxima
              </span>
              <span class="flex items-center gap-1 px-1.5 py-0.5 rounded bg-destructive/10 text-destructive font-medium border border-destructive/30">
                <span class="size-1.5 rounded-full bg-destructive" />
                Vencida
              </span>
              <span class="flex items-center gap-1 px-1.5 py-0.5 rounded bg-primary/10 text-primary font-medium border border-primary/30">
                <span class="size-1.5 rounded-full bg-primary" />
                Parcial
              </span>
            </div>
          </div>
        </CardHeader>

        <CardContent class="p-0 text-xs">
          <div v-if="scheduleLoading" class="p-6 space-y-3">
            <Skeleton class="h-12 w-full" />
            <Skeleton class="h-12 w-full" />
            <Skeleton class="h-12 w-full" />
          </div>

          <div v-else class="divide-y divide-border">
            <div
              v-for="inst in schedule"
              :key="inst.number"
              class="p-3.5 hover:bg-muted/10 transition-colors flex items-center justify-between gap-3"
            >
              <!-- Info de la Cuota -->
              <div class="flex items-center gap-3">
                <div class="size-8 rounded-full bg-muted flex items-center justify-center font-mono font-bold text-xs text-foreground shrink-0">
                  {{ inst.number }}
                </div>
                <div>
                  <span class="font-bold text-xs text-foreground block">
                    Vence el {{ formatBusinessDate(inst.dueDate) }}
                  </span>
                  <div class="text-[10px] text-muted-foreground space-x-2 font-mono">
                    <span>Cap: S/ {{ inst.principal }}</span>
                    <span>·</span>
                    <span>Int: S/ {{ inst.interest }}</span>
                    <span v-if="Number(inst.lateFee) > 0" class="text-destructive font-semibold">
                      · Mora: S/ {{ inst.lateFee }}
                    </span>
                  </div>
                </div>
              </div>

              <!-- Monto y Badge de Estado Semaforizado -->
              <div class="text-right space-y-1">
                <div class="font-bold font-mono text-sm text-foreground">
                  <MoneyText :amount="inst.totalDue" />
                </div>
                <Badge
                  variant="outline"
                  class="text-[10px] font-semibold border"
                  :class="getInstallmentColorClass(inst)"
                >
                  {{ getInstallmentStatusLabel(inst.status) }}
                </Badge>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </template>
  </div>
</template>
