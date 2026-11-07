<script setup lang="ts">
import { onMounted } from 'vue'
import {
  AlertTriangle,
  ArrowUpRight,
  Banknote,
  Building2,
  Calendar,
  CheckCircle2,
  Clock,
  DollarSign,
  HandCoins,
  Layers,
  Percent,
  RefreshCw,
  ShieldAlert,
  ShieldCheck,
  TrendingUp,
  Users,
} from '@lucide/vue'

import { MoneyText, PageHeader, StatusBadge } from '@prestamos/ui'
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

import { useDashboardKpis } from '../composables'

// FE-BO-37 y FE-BO-38: Dashboard de KPIs ejecutivos, cartera, mora y originación.

const { kpis, loading, error, loadKpis, refresh } = useDashboardKpis()

onMounted(() => {
  void loadKpis()
})
</script>

<template>
  <div class="space-y-6">
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <PageHeader
        title="Dashboard Ejecutivo"
        description="Monitor en tiempo real de cartera, morosidad, recaudación, desembolsos y tesorería."
      />
      <div class="flex items-center gap-2">
        <Button variant="outline" size="sm" :disabled="loading" @click="refresh">
          <RefreshCw class="size-3.5 mr-1.5" :class="{ 'animate-spin': loading }" />
          Actualizar KPIs
        </Button>
      </div>
    </div>

    <!-- Estado de Carga -->
    <div v-if="loading && !kpis" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      <Skeleton v-for="i in 4" :key="i" class="h-28 w-full" />
    </div>

    <!-- Error al Cargar -->
    <Card v-else-if="error && !kpis" class="border-destructive/40 bg-destructive/5 p-6 text-center">
      <AlertTriangle class="size-8 mx-auto text-destructive mb-2" />
      <p class="font-semibold text-sm text-foreground">No se pudieron cargar los KPIs</p>
      <p class="text-xs text-muted-foreground mt-1">{{ error }}</p>
      <Button size="sm" class="mt-4" @click="refresh">Reintentar</Button>
    </Card>

    <template v-else-if="kpis">
      <!-- ── BLOQUE 1: KPIs Principales (FE-BO-37) ── -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <!-- Tarjeta 1: Cartera Total Activa -->
        <Card class="relative overflow-hidden">
          <CardHeader class="pb-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-muted-foreground">Cartera Total</span>
              <Banknote class="size-4 text-primary" />
            </div>
            <CardTitle class="text-2xl font-bold">
              <MoneyText :amount="kpis.portfolio.total" />
            </CardTitle>
          </CardHeader>
          <CardContent class="text-[11px] text-muted-foreground space-y-1">
            <div class="flex justify-between">
              <span>Vigente:</span>
              <span class="font-semibold text-success"><MoneyText :amount="kpis.portfolio.vigente" /></span>
            </div>
            <div class="flex justify-between">
              <span>En mora:</span>
              <span class="font-semibold text-destructive"><MoneyText :amount="kpis.portfolio.enMora" /></span>
            </div>
          </CardContent>
        </Card>

        <!-- Tarjeta 2: Índice de Morosidad -->
        <Card class="relative overflow-hidden">
          <CardHeader class="pb-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-muted-foreground">Índice de Morosidad</span>
              <ShieldAlert class="size-4 text-destructive" />
            </div>
            <div class="flex items-baseline gap-2">
              <CardTitle class="text-2xl font-bold text-destructive">
                {{ kpis.delinquencyRate }}%
              </CardTitle>
              <Badge
                variant="outline"
                :class="Number(kpis.delinquencyRate) > 5 ? 'border-destructive text-destructive bg-destructive/10' : 'border-success text-success bg-success/10'"
                class="text-[10px]"
              >
                {{ Number(kpis.delinquencyRate) > 5 ? 'Alerta' : 'Saludable' }}
              </Badge>
            </div>
          </CardHeader>
          <CardContent class="text-[11px] text-muted-foreground">
            <span>Cartera castigada: </span>
            <span class="font-semibold text-foreground"><MoneyText :amount="kpis.portfolio.castigada" /></span>
          </CardContent>
        </Card>

        <!-- Tarjeta 3: Recaudación del Día -->
        <Card class="relative overflow-hidden">
          <CardHeader class="pb-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-muted-foreground">Cobranza del Día</span>
              <HandCoins class="size-4 text-emerald-600" />
            </div>
            <CardTitle class="text-2xl font-bold text-emerald-600">
              <MoneyText :amount="kpis.collections.collectedToday" />
            </CardTitle>
          </CardHeader>
          <CardContent class="text-[11px] text-muted-foreground">
            <span>Meta esperada hoy: </span>
            <span class="font-semibold text-foreground"><MoneyText :amount="kpis.collections.expectedToday" /></span>
          </CardContent>
        </Card>

        <!-- Tarjeta 4: Desembolsos del Día -->
        <Card class="relative overflow-hidden">
          <CardHeader class="pb-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-muted-foreground">Desembolsos Hoy</span>
              <ArrowUpRight class="size-4 text-primary" />
            </div>
            <CardTitle class="text-2xl font-bold">
              <MoneyText :amount="kpis.disbursementsToday.amount" />
            </CardTitle>
          </CardHeader>
          <CardContent class="text-[11px] text-muted-foreground">
            <span>Operaciones confirmadas: </span>
            <span class="font-semibold text-foreground">{{ kpis.disbursementsToday.count }} créditos</span>
          </CardContent>
        </Card>
      </div>

      <!-- ── BLOQUE 2: Tesorería y Originación (FE-BO-37 / FE-BO-38) ── -->
      <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
        <!-- Tesorería -->
        <Card>
          <CardHeader class="pb-3">
            <CardTitle class="text-base flex items-center gap-2">
              <Building2 class="size-4 text-primary" />
              Saldos Bancarios
            </CardTitle>
            <CardDescription class="text-xs">Disponibilidad en cuentas corrientes.</CardDescription>
          </CardHeader>
          <CardContent class="space-y-3 text-xs">
            <div class="flex items-center justify-between p-3 rounded-lg border bg-muted/30">
              <span class="text-muted-foreground">Saldo Total en Bancos:</span>
              <span class="font-bold text-foreground text-sm"><MoneyText :amount="kpis.bankBalances.total" /></span>
            </div>
            <div class="flex items-center justify-between p-3 rounded-lg border bg-warning/5 border-warning/20">
              <span class="text-muted-foreground">Desembolsos en Tránsito:</span>
              <span class="font-bold text-warning text-sm"><MoneyText :amount="kpis.bankBalances.inTransit" /></span>
            </div>
          </CardContent>
        </Card>

        <!-- Tasa de Aprobación de Originación -->
        <Card>
          <CardHeader class="pb-3">
            <CardTitle class="text-base flex items-center gap-2">
              <ShieldCheck class="size-4 text-success" />
              Admisión de Crédito
            </CardTitle>
            <CardDescription class="text-xs">Tasa de aprobación del motor de riesgo (FE-BO-38).</CardDescription>
          </CardHeader>
          <CardContent class="space-y-3 text-xs">
            <div class="flex items-center justify-between p-3 rounded-lg border bg-muted/30">
              <span class="text-muted-foreground">Tasa de Aprobación:</span>
              <span class="text-xl font-bold text-success">{{ kpis.approvalRate }}%</span>
            </div>
            <p class="text-[11px] text-muted-foreground">
              Calculada sobre todas las solicitudes evaluadas con dictamen definitivo.
            </p>
          </CardContent>
        </Card>

        <!-- Distribución de Mora por Aging -->
        <Card>
          <CardHeader class="pb-3">
            <CardTitle class="text-base flex items-center gap-2">
              <Clock class="size-4 text-destructive" />
              Aging de Mora
            </CardTitle>
            <CardDescription class="text-xs">Concentración de deuda vencida por tramo.</CardDescription>
          </CardHeader>
          <CardContent class="space-y-2 text-xs">
            <div
              v-for="bar in kpis.agingChart"
              :key="bar.bucket"
              class="flex items-center justify-between py-1 border-b text-[11px]"
            >
              <span class="font-medium text-foreground">{{ bar.bucket.replace(/_/g, ' ') }}:</span>
              <span class="font-semibold text-destructive"><MoneyText :amount="bar.amount" /></span>
            </div>
          </CardContent>
        </Card>
      </div>

      <!-- ── BLOQUE 3: Evolución Mensual ── -->
      <Card>
        <CardHeader class="pb-3">
          <CardTitle class="text-base flex items-center gap-2">
            <TrendingUp class="size-4 text-primary" />
            Evolución Mensual (Colocación vs Recaudación)
          </CardTitle>
          <CardDescription class="text-xs">Comparativo histórico de los últimos meses.</CardDescription>
        </CardHeader>
        <CardContent class="p-0">
          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs">
              <thead class="border-b bg-muted/50 text-muted-foreground font-medium">
                <tr>
                  <th class="px-4 py-2.5">Mes</th>
                  <th class="px-4 py-2.5">Desembolsado</th>
                  <th class="px-4 py-2.5">Recaudado</th>
                  <th class="px-4 py-2.5 text-right">Balance Neto</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-border">
                <tr v-for="m in kpis.monthly" :key="m.month" class="hover:bg-muted/30">
                  <td class="px-4 py-2.5 font-semibold text-foreground">{{ m.month }}</td>
                  <td class="px-4 py-2.5 font-mono text-primary"><MoneyText :amount="m.disbursed" /></td>
                  <td class="px-4 py-2.5 font-mono text-emerald-600 font-semibold"><MoneyText :amount="m.collected" /></td>
                  <td class="px-4 py-2.5 font-mono text-right font-bold">
                    <MoneyText :amount="(Number(m.collected) - Number(m.disbursed)).toFixed(2)" />
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>
    </template>
  </div>
</template>
