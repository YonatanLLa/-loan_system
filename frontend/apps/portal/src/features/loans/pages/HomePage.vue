<script setup lang="ts">
import { onMounted, watch } from 'vue'
import { RouterLink } from 'vue-router'
import {
  AlertCircle,
  AlertTriangle,
  ArrowRight,
  Calendar,
  CheckCircle2,
  Clock,
  Coins,
  CreditCard,
  FilePlus,
  FileText,
  HandCoins,
  Receipt,
  ShieldAlert,
  Sparkles,
  TrendingUp,
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

import { useSessionStore } from '@/app/stores/session'
import { useClientLoans, useLoanSchedule } from '../composables'

// FE-CP-10: Pantalla de Inicio - Próximo vencimiento, días restantes/atraso, saldo insoluto y mora del día.

const session = useSessionStore()
const {
  loans,
  activeLoan,
  hasLoans,
  hasOverdueLoans,
  isLoading: loansLoading,
  loadLoans,
} = useClientLoans()

const {
  schedule,
  nextInstallment,
  totalMoraAccrued,
  dueCountdownText,
  isLoading: scheduleLoading,
  loadSchedule,
} = useLoanSchedule()

watch(activeLoan, (loan) => {
  if (loan?.id) {
    void loadSchedule(loan.id)
  }
})

onMounted(() => {
  void loadLoans()
})
</script>

<template>
  <div class="space-y-4 py-2">
    <!-- Saludo al Cliente -->
    <div class="flex items-center justify-between">
      <div>
        <span class="text-[11px] text-muted-foreground block">Bienvenido de nuevo,</span>
        <h2 class="text-xl font-bold leading-tight text-foreground">
          {{ session.user?.fullName || 'Prestatario' }}
        </h2>
      </div>

      <div class="size-10 rounded-full bg-primary/10 flex items-center justify-center text-primary font-bold text-sm">
        {{ session.user?.fullName?.charAt(0) || 'U' }}
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loansLoading" class="space-y-3 p-2">
      <Skeleton class="h-44 w-full rounded-2xl" />
      <Skeleton class="h-24 w-full rounded-2xl" />
    </div>

    <!-- Si no tiene préstamos activos -->
    <Card v-else-if="!hasLoans" class="shadow-md border-border overflow-hidden">
      <CardContent class="p-6 text-center space-y-4">
        <div class="mx-auto size-14 rounded-2xl bg-primary/10 flex items-center justify-center text-primary">
          <Sparkles class="size-7" />
        </div>
        <div class="space-y-1">
          <h3 class="font-bold text-base text-foreground">Aún no tienes préstamos activos</h3>
          <p class="text-xs text-muted-foreground max-w-xs mx-auto">
            Simula tu crédito en segundos con desembolso inmediato a tu cuenta o billetera digital.
          </p>
        </div>
        <Button as-child class="h-10 text-xs font-semibold px-6 shadow-md">
          <RouterLink to="/solicitar">
            <FilePlus class="size-4 mr-2" />
            Solicitar Mi Préstamo Ahora
          </RouterLink>
        </Button>
      </CardContent>
    </Card>

    <!-- Tarjeta Principal del Préstamo Activo (FE-CP-10) -->
    <template v-else-if="activeLoan">
      <Card
        class="shadow-xl overflow-hidden border-2 transition-all"
        :class="
          activeLoan.status === 'EN_MORA'
            ? 'border-destructive/40 bg-gradient-to-b from-destructive/5 to-background'
            : 'border-primary/30 bg-gradient-to-b from-primary/5 to-background'
        "
      >
        <CardHeader class="pb-2">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-1.5 text-xs font-semibold">
              <span class="font-mono text-primary">#{{ activeLoan.id }}</span>
              <span class="text-muted-foreground">·</span>
              <span class="text-foreground">{{ activeLoan.productName }}</span>
            </div>
            <StatusBadge kind="loan" :status="activeLoan.status" />
          </div>
        </CardHeader>

        <CardContent class="space-y-4 pt-1 text-xs">
          <!-- Alerta de mora si está vencido -->
          <div
            v-if="activeLoan.status === 'EN_MORA' || Number(totalMoraAccrued) > 0"
            class="flex items-center gap-2 p-2.5 rounded-lg bg-destructive/10 border border-destructive/20 text-destructive text-[11px]"
          >
            <ShieldAlert class="size-4 shrink-0" />
            <div class="leading-tight">
              <span class="font-bold">Atención: Cuota vencida en mora.</span>
              <span class="block text-[10px] text-destructive/80">
                Mora acumulada del día: S/ {{ totalMoraAccrued }} (evita intereses moratorios adicionales).
              </span>
            </div>
          </div>

          <!-- Próximo Vencimiento y Cuota Inmediata -->
          <div class="rounded-xl border bg-background/80 p-4 space-y-3">
            <div class="flex items-start justify-between">
              <div>
                <span class="text-[10px] text-muted-foreground block font-medium">Próximo Vencimiento:</span>
                <span class="text-sm font-bold text-foreground">
                  {{ nextInstallment?.dueDate ? formatBusinessDate(nextInstallment.dueDate) : 'Sin cuotas pendientes' }}
                </span>
              </div>

              <Badge
                variant="outline"
                class="text-[10px] font-semibold"
                :class="
                  nextInstallment?.status === 'OVERDUE'
                    ? 'border-destructive text-destructive bg-destructive/10'
                    : 'border-primary text-primary bg-primary/10'
                "
              >
                <Clock class="size-3 mr-1" />
                {{ dueCountdownText }}
              </Badge>
            </div>

            <div class="pt-2 border-t">
              <span class="text-[10px] text-muted-foreground block font-medium">Monto de la Cuota:</span>
              <div class="text-3xl font-extrabold font-mono text-primary tracking-tight mt-0.5">
                <MoneyText :amount="nextInstallment?.totalDue ?? '0.00'" />
              </div>
            </div>
          </div>

          <!-- Saldo Insoluto y Progreso -->
          <div class="grid grid-cols-2 gap-2 text-center text-xs">
            <div class="p-3 rounded-lg border bg-background/60">
              <span class="text-[10px] text-muted-foreground block">Saldo Insoluto Total:</span>
              <span class="text-sm font-bold font-mono text-foreground">
                <MoneyText :amount="activeLoan.outstandingPrincipal" />
              </span>
            </div>

            <div class="p-3 rounded-lg border bg-background/60">
              <span class="text-[10px] text-muted-foreground block">Monto Original:</span>
              <span class="text-sm font-semibold font-mono text-muted-foreground">
                <MoneyText :amount="activeLoan.principal" />
              </span>
            </div>
          </div>

          <!-- Botón Principal: Pagar Cuota -->
          <div class="space-y-2 pt-1">
            <Button
              as-child
              class="w-full h-11 text-xs font-semibold shadow-lg bg-primary hover:bg-primary/90"
            >
              <RouterLink :to="{ path: '/pagar', query: { loanId: activeLoan.id } }">
                <Wallet class="size-4 mr-2" />
                <span>Pagar Cuota Inmediata</span>
                <ArrowRight class="size-4 ml-auto" />
              </RouterLink>
            </Button>

            <Button
              as-child
              variant="outline"
              size="sm"
              class="w-full h-9 text-xs"
            >
              <RouterLink :to="{ path: '/prestamos', query: { loanId: activeLoan.id } }">
                <FileText class="size-3.5 mr-1.5" />
                Ver Cronograma Completo de Cuotas (FE-CP-11)
              </RouterLink>
            </Button>
          </div>
        </CardContent>
      </Card>

      <!-- Accesos Rápidos del Portal -->
      <div class="grid grid-cols-2 gap-2 text-xs">
        <RouterLink
          to="/solicitar"
          class="p-3 rounded-xl border bg-card hover:bg-muted/20 transition-all flex items-center gap-2.5 shadow-xs"
        >
          <div class="size-8 rounded-lg bg-primary/10 flex items-center justify-center text-primary shrink-0">
            <Coins class="size-4" />
          </div>
          <div>
            <span class="font-semibold block text-foreground">Nuevo Crédito</span>
            <span class="text-[10px] text-muted-foreground">Simular al instante</span>
          </div>
        </RouterLink>

        <RouterLink
          to="/solicitudes"
          class="p-3 rounded-xl border bg-card hover:bg-muted/20 transition-all flex items-center gap-2.5 shadow-xs"
        >
          <div class="size-8 rounded-lg bg-emerald-500/10 flex items-center justify-center text-emerald-600 shrink-0">
            <Clock class="size-4" />
          </div>
          <div>
            <span class="font-semibold block text-foreground">Mis Solicitudes</span>
            <span class="text-[10px] text-muted-foreground">Línea de tiempo</span>
          </div>
        </RouterLink>
      </div>
    </template>
  </div>
</template>
