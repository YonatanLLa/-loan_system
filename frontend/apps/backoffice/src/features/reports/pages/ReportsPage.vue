<script setup lang="ts">
import { computed, onMounted } from 'vue'
import {
  ArrowDownToLine,
  Clock,
  Download,
  FileCheck2,
  FileText,
  HandCoins,
  QrCode,
  Receipt,
  Search,
  TrendingUp,
  Users,
} from '@lucide/vue'

import {
  MoneyText,
  PageHeader,
  SummaryGrid,
  TableGenerator,
  type SummaryItem,
} from '@prestamos/ui'
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
import { formatBusinessDate } from '@prestamos/utils'

import { useReports } from '../composables'
import {
  portfolioColumns,
  agingReportColumns,
  analystProductivityColumns,
  collectorProductivityColumns,
  statementMovementColumns,
} from '../utils/reportsColumns'

// FE-BO-39 y FE-BO-40: Reportes ejecutivos, exportación y estado de cuenta.

const {
  activeTab,
  // Cartera
  portfolioPage,
  portfolioQuery,
  portfolioLoading,
  loadPortfolio,
  exportPortfolioCsv,
  // Aging
  agingSummary,
  agingLoading,
  loadAging,
  exportAgingCsv,
  // Flujo de Caja
  cashflow,
  cashflowFrom,
  cashflowTo,
  cashflowLoading,
  loadCashflow,
  exportCashflowCsv,
  // Productividad
  productivity,
  loadProductivity,
  // Estado de Cuenta
  statementSearchId,
  statement,
  statementLoading,
  loadStatement,
} = useReports()

const statementSummaryItems = computed<SummaryItem[]>(() => {
  if (!statement.value) return []
  return [
    { label: 'Capital Inicial', money: statement.value.principal, valueClass: 'text-foreground' },
    {
      label: 'Saldo Insoluto',
      money: statement.value.outstandingPrincipal,
      valueClass: 'text-primary',
    },
    { label: 'Total Amortizado', money: statement.value.totalPaid, valueClass: 'text-success' },
    {
      label: 'Próximo Vencimiento',
      value: statement.value.nextDueDate
        ? formatBusinessDate(statement.value.nextDueDate)
        : 'Al día',
      valueClass: 'font-semibold text-foreground',
    },
  ]
})

onMounted(() => {
  void loadPortfolio()
  void loadAging()
  void loadCashflow()
  void loadProductivity()
})
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Centro de Reportes"
      description="Reportes operativos, cartera consolidada, flujo de caja, productividad y estados de cuenta (FE-BO-39 y FE-BO-40)."
    />

    <!-- Selector de Pestañas -->
    <div class="flex flex-wrap items-center gap-1 border-b text-sm">
      <button
        type="button"
        class="flex items-center gap-2 border-b-2 px-4 py-2.5 font-semibold transition-all"
        :class="
          activeTab === 'portfolio'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground'
        "
        @click="activeTab = 'portfolio'"
      >
        <FileText class="size-4" />
        Cartera de Créditos
      </button>

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
        <Clock class="size-4" />
        Aging y Morosidad
      </button>

      <button
        type="button"
        class="flex items-center gap-2 border-b-2 px-4 py-2.5 font-semibold transition-all"
        :class="
          activeTab === 'cashflow'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground'
        "
        @click="activeTab = 'cashflow'"
      >
        <TrendingUp class="size-4" />
        Flujo de Caja
      </button>

      <button
        type="button"
        class="flex items-center gap-2 border-b-2 px-4 py-2.5 font-semibold transition-all"
        :class="
          activeTab === 'productivity'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground'
        "
        @click="activeTab = 'productivity'"
      >
        <Users class="size-4" />
        Productividad
      </button>

      <button
        type="button"
        class="flex items-center gap-2 border-b-2 px-4 py-2.5 font-semibold transition-all"
        :class="
          activeTab === 'statement'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground'
        "
        @click="activeTab = 'statement'"
      >
        <Receipt class="size-4" />
        Estado de Cuenta (FE-BO-40)
      </button>
    </div>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- TAB 1: CARTERA DE CRÉDITOS                                -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <template v-if="activeTab === 'portfolio'">
      <Card>
        <CardContent class="p-4 flex flex-wrap items-center justify-between gap-3">
          <form class="flex items-center gap-2 flex-1 max-w-md" @submit.prevent="loadPortfolio(1)">
            <div class="relative w-full">
              <Search class="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                v-model="portfolioQuery"
                placeholder="Buscar por cliente o producto..."
                class="pl-9 h-9 text-xs"
              />
            </div>
            <Button type="submit" size="sm" class="h-9" :disabled="portfolioLoading">Filtrar</Button>
          </form>

          <Button
            variant="outline"
            size="sm"
            class="h-9"
            :disabled="portfolioLoading || portfolioPage.items.length === 0"
            @click="exportPortfolioCsv"
          >
            <Download class="size-3.5 mr-1.5" />
            Descargar CSV
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardContent class="p-0">
          <div v-if="portfolioLoading" class="p-6 space-y-3">
            <Skeleton class="h-10 w-full" />
            <Skeleton class="h-12 w-full" />
          </div>
          <div v-else-if="portfolioPage.items.length === 0" class="p-8 text-center text-xs text-muted-foreground">
            No se encontraron préstamos con los criterios ingresados.
          </div>
          <div v-else>
            <TableGenerator
              :columns="portfolioColumns"
              :rows="portfolioPage.items"
              row-key="id"
              body-class="divide-y text-xs"
            >
              <template #cell-id="{ row: loan }">
                <RouterLink
                  :to="{ name: 'loan-detail', params: { id: loan.id } }"
                  class="text-primary hover:underline font-mono font-semibold"
                >
                  {{ loan.id }}
                </RouterLink>
              </template>
            </TableGenerator>
          </div>
        </CardContent>
      </Card>
    </template>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- TAB 2: AGING Y MOROSIDAD                                  -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <template v-else-if="activeTab === 'aging'">
      <div class="flex justify-end mb-2">
        <Button
          variant="outline"
          size="sm"
          :disabled="agingLoading || agingSummary.length === 0"
          @click="exportAgingCsv"
        >
          <Download class="size-3.5 mr-1.5" />
          Exportar Aging CSV
        </Button>
      </div>

      <Card>
        <CardContent class="p-0">
          <div v-if="agingLoading" class="p-6 space-y-3">
            <Skeleton class="h-10 w-full" />
            <Skeleton class="h-12 w-full" />
          </div>
          <div v-else>
            <TableGenerator
              :columns="agingReportColumns"
              :rows="agingSummary"
              row-key="bucket"
              body-class="divide-y text-xs"
            />
          </div>
        </CardContent>
      </Card>
    </template>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- TAB 3: FLUJO DE CAJA                                      -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <template v-else-if="activeTab === 'cashflow'">
      <Card>
        <CardContent class="p-4 flex flex-wrap items-center justify-between gap-3">
          <div class="flex items-center gap-3">
            <div class="flex items-center gap-1.5 text-xs">
              <span class="text-muted-foreground">Desde:</span>
              <Input v-model="cashflowFrom" type="date" class="h-8 text-xs w-36" />
            </div>
            <div class="flex items-center gap-1.5 text-xs">
              <span class="text-muted-foreground">Hasta:</span>
              <Input v-model="cashflowTo" type="date" class="h-8 text-xs w-36" />
            </div>
            <Button size="sm" class="h-8" :disabled="cashflowLoading" @click="loadCashflow">
              Consultar
            </Button>
          </div>

          <Button
            variant="outline"
            size="sm"
            class="h-8"
            :disabled="cashflowLoading || !cashflow"
            @click="exportCashflowCsv"
          >
            <Download class="size-3.5 mr-1.5" />
            Exportar Flujo CSV
          </Button>
        </CardContent>
      </Card>

      <div v-if="cashflow" class="grid grid-cols-1 md:grid-cols-3 gap-4">
        <!-- Ingresos -->
        <Card class="border-success/30 bg-success/5">
          <CardHeader class="pb-2">
            <CardTitle class="text-base text-success flex items-center gap-2">
              <HandCoins class="size-4" />
              Ingresos Totales Recaudados
            </CardTitle>
          </CardHeader>
          <CardContent class="space-y-2 text-xs">
            <div class="flex justify-between py-1 border-b">
              <span class="text-muted-foreground">Capital Amortizado:</span>
              <span class="font-mono font-semibold"><MoneyText :amount="cashflow.inflows.principal" /></span>
            </div>
            <div class="flex justify-between py-1 border-b">
              <span class="text-muted-foreground">Intereses Corrientes:</span>
              <span class="font-mono font-semibold"><MoneyText :amount="cashflow.inflows.interest" /></span>
            </div>
            <div class="flex justify-between py-1 border-b">
              <span class="text-muted-foreground">Penalidades de Mora:</span>
              <span class="font-mono font-semibold"><MoneyText :amount="cashflow.inflows.mora" /></span>
            </div>
            <div class="flex justify-between py-1">
              <span class="text-muted-foreground">Comisiones:</span>
              <span class="font-mono font-semibold"><MoneyText :amount="cashflow.inflows.fees" /></span>
            </div>
          </CardContent>
        </Card>

        <!-- Egresos -->
        <Card class="border-destructive/30 bg-destructive/5">
          <CardHeader class="pb-2">
            <CardTitle class="text-base text-destructive flex items-center gap-2">
              <ArrowDownToLine class="size-4" />
              Egresos de Capital
            </CardTitle>
          </CardHeader>
          <CardContent class="space-y-2 text-xs">
            <div class="flex justify-between py-1 border-b">
              <span class="text-muted-foreground">Desembolsos Confirmados:</span>
              <span class="font-mono font-semibold"><MoneyText :amount="cashflow.outflows.disbursements" /></span>
            </div>
            <div class="flex justify-between py-1">
              <span class="text-muted-foreground">Comisiones Bancarias:</span>
              <span class="font-mono font-semibold"><MoneyText :amount="cashflow.outflows.bankFees" /></span>
            </div>
          </CardContent>
        </Card>

        <!-- Balance Neto -->
        <Card>
          <CardHeader class="pb-2">
            <CardTitle class="text-base flex items-center gap-2">
              <TrendingUp class="size-4 text-primary" />
              Flujo Neto del Periodo
            </CardTitle>
          </CardHeader>
          <CardContent class="space-y-3 text-xs">
            <div class="p-4 rounded-lg bg-muted/40 text-center">
              <span class="text-xs text-muted-foreground block mb-1">Superávit / Déficit:</span>
              <span class="text-2xl font-bold font-mono" :class="Number(cashflow.net) >= 0 ? 'text-success' : 'text-destructive'">
                <MoneyText :amount="cashflow.net" />
              </span>
            </div>
            <p class="text-[11px] text-muted-foreground text-center">
              Del {{ formatBusinessDate(cashflow.from) }} al {{ formatBusinessDate(cashflow.to) }}
            </p>
          </CardContent>
        </Card>
      </div>
    </template>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- TAB 4: PRODUCTIVIDAD                                      -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <template v-else-if="activeTab === 'productivity'">
      <div v-if="productivity" class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <!-- Analistas -->
        <Card>
          <CardHeader class="pb-3">
            <CardTitle class="text-base flex items-center gap-2">
              <FileCheck2 class="size-4 text-primary" />
              Productividad de Admisión y Riesgos
            </CardTitle>
            <CardDescription class="text-xs">Evaluaciones y dictámenes por analista.</CardDescription>
          </CardHeader>
          <CardContent class="p-0">
            <TableGenerator
              :columns="analystProductivityColumns"
              :rows="productivity.analysts"
              row-key="userId"
              body-class="divide-y text-xs"
              cell-class="px-4 py-2.5"
            />
          </CardContent>
        </Card>

        <!-- Gestores de Cobranza -->
        <Card>
          <CardHeader class="pb-3">
            <CardTitle class="text-base flex items-center gap-2">
              <Users class="size-4 text-primary" />
              Productividad de Gestión de Cobranzas
            </CardTitle>
            <CardDescription class="text-xs">Acciones remotas y compromisos de pago.</CardDescription>
          </CardHeader>
          <CardContent class="p-0">
            <TableGenerator
              :columns="collectorProductivityColumns"
              :rows="productivity.collectors"
              row-key="userId"
              body-class="divide-y text-xs"
              cell-class="px-4 py-2.5"
            />
          </CardContent>
        </Card>
      </div>
    </template>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- TAB 5: ESTADO DE CUENTA (FE-BO-40)                        -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <template v-else-if="activeTab === 'statement'">
      <Card>
        <CardContent class="p-4">
          <form class="flex items-center gap-2 max-w-md" @submit.prevent="loadStatement()">
            <div class="relative w-full">
              <Search class="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                v-model="statementSearchId"
                placeholder="Código de préstamo (ej. loan-001)..."
                class="pl-9 h-9 text-xs font-mono"
              />
            </div>
            <Button type="submit" size="sm" class="h-9" :disabled="statementLoading">
              Buscar Préstamo
            </Button>
          </form>
        </CardContent>
      </Card>

      <Card v-if="statement">
        <CardHeader class="border-b pb-4">
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div>
              <CardTitle class="text-lg">Estado de Cuenta Oficial</CardTitle>
              <CardDescription>
                Crédito #{{ statement.loanId }} · Prestatario: <span class="font-semibold text-foreground">{{ statement.customerName }}</span>
              </CardDescription>
            </div>
            <div class="flex items-center gap-2">
              <div class="flex items-center gap-1.5 p-1.5 border rounded bg-muted/20 text-[10px] font-mono">
                <QrCode class="size-6 text-foreground" />
                <span>QR Certificado SBS</span>
              </div>
            </div>
          </div>
        </CardHeader>
        <CardContent class="space-y-4 pt-4">
          <SummaryGrid :items="statementSummaryItems" grid-class="grid-cols-2 sm:grid-cols-4" />

          <div class="space-y-2">
            <h4 class="font-semibold text-xs text-foreground">Movimientos Históricos Imputados</h4>
            <div class="overflow-x-auto rounded-lg border">
              <TableGenerator
                :columns="statementMovementColumns"
                :rows="statement.movements"
                :row-key="(_, idx) => idx"
                table-class="w-full text-left text-xs"
                head-class="border-b bg-muted/50 font-medium text-muted-foreground"
                header-cell-class="px-4 py-2"
                body-class="divide-y divide-border"
                cell-class="px-4 py-2.5"
                row-class="hover:bg-muted/30"
              >
                <template #cell-type="{ row: mov }">
                  <Badge
                    variant="outline"
                    :class="
                      mov.type === 'PAGO'
                        ? 'border-success text-success bg-success/10'
                        : mov.type === 'REVERSO'
                          ? 'border-destructive text-destructive bg-destructive/10'
                          : 'border-primary text-primary bg-primary/10'
                    "
                    class="text-[10px]"
                  >
                    {{ mov.type }}
                  </Badge>
                </template>
              </TableGenerator>
            </div>
          </div>
        </CardContent>
      </Card>
      <div v-else class="p-8 text-center text-xs text-muted-foreground">
        Ingresa un código de préstamo válido arriba para consultar su estado de cuenta.
      </div>
    </template>
  </div>
</template>
