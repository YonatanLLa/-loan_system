<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import {
  Landmark,
  Building2,
  ArrowUpRight,
  UploadCloud,
  CheckCircle2,
  AlertTriangle,
  RotateCcw,
  RefreshCw,
  Clock,
  ArrowRightLeft,
  Calendar,
  Layers,
  ChevronLeft,
  ChevronRight,
  ShieldAlert,
} from '@lucide/vue'
import { toast } from 'vue-sonner'

import type {
  BankStatementLine,
  CompanyBankAccount,
  DailyBalance,
  Disbursement,
  Page,
} from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { EmptyState, MoneyText, PageHeader, StatusBadge } from '@prestamos/ui'
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
import { formatBusinessDate, formatInstant } from '@prestamos/utils'

import { useApi } from '@/app/api'
import CompanyAccountModal from '../components/CompanyAccountModal.vue'
import ImportStatementModal from '../components/ImportStatementModal.vue'
import ManualMatchModal from '../components/ManualMatchModal.vue'

// FE-BO-27, FE-BO-28, FE-BO-29, FE-BO-30: Tesorería, desembolsos en tránsito, conciliación y cuadre.

const loading = ref(false)
const activeTab = ref<'accounts' | 'disbursements' | 'reconciliation' | 'balance'>('accounts')

// Cuentas de la Fintech (FE-BO-27)
const companyAccounts = ref<CompanyBankAccount[]>([])
const isAccountModalOpen = ref(false)

// Desembolsos en tránsito y fallidos (FE-BO-28)
const disbursements = ref<Disbursement[]>([])
const selectedDisbursementStatus = ref('PENDING,PROCESSING,FAILED')
const retryingDisbursementId = ref<string | null>(null)

// Conciliación de Extractos (FE-BO-29)
const statements = ref<BankStatementLine[]>([])
const isImportModalOpen = ref(false)
const isMatchModalOpen = ref(false)
const statementToMatch = ref<BankStatementLine | null>(null)

// Cuadre Diario (FE-BO-30)
const dailyBalance = ref<DailyBalance | null>(null)
const selectedBalanceAccId = ref('bca-001')
const selectedBalanceDate = ref(new Date().toISOString().slice(0, 10))
const loadingBalance = ref(false)

async function loadCompanyAccounts() {
  try {
    const { data } = await useApi().get<CompanyBankAccount[]>('/treasury/bank-accounts')
    companyAccounts.value = data
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar cuentas corporativas', { description: apiError.detail })
  }
}

async function toggleAccountActive(acc: CompanyBankAccount) {
  try {
    const { data } = await useApi().patch<CompanyBankAccount>(
      `/treasury/bank-accounts/${acc.id}`,
      { isActive: !acc.isActive },
    )
    acc.isActive = data.isActive
    toast.success(
      acc.isActive ? 'Cuenta activada' : 'Cuenta desactivada',
      { description: `La cuenta ${acc.bankName} cambió de estado.` },
    )
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('No se pudo actualizar cuenta', { description: apiError.detail })
  }
}

async function loadDisbursements() {
  try {
    const params: Record<string, string> = {}
    if (selectedDisbursementStatus.value) params.status = selectedDisbursementStatus.value
    const { data } = await useApi().get<{ items: Disbursement[] }>('/treasury/disbursements', {
      params,
    })
    disbursements.value = data.items
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar desembolsos', { description: apiError.detail })
  }
}

async function retryDisbursement(disb: Disbursement) {
  retryingDisbursementId.value = disb.id
  try {
    await useApi().post(`/disbursements/${disb.id}/retry`, {})
    toast.success('Desembolso reintentado', {
      description: `La orden de transferencia #${disb.id} volvió al estado PENDIENTE de transmisión.`,
    })
    void loadDisbursements()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al reintentar desembolso', { description: apiError.detail })
  } finally {
    retryingDisbursementId.value = null
  }
}

async function loadReconciliation() {
  try {
    const { data } = await useApi().get<{ items: BankStatementLine[] }>('/reconciliation')
    statements.value = data.items
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar extractos', { description: apiError.detail })
  }
}

async function loadDailyBalance() {
  loadingBalance.value = true
  try {
    const { data } = await useApi().get<DailyBalance>('/treasury/daily-balance', {
      params: {
        bankAccountId: selectedBalanceAccId.value,
        date: selectedBalanceDate.value,
      },
    })
    dailyBalance.value = data
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al consultar cuadre diario', { description: apiError.detail })
  } finally {
    loadingBalance.value = false
  }
}

function openMatch(line: BankStatementLine) {
  statementToMatch.value = line
  isMatchModalOpen.value = true
}

function switchTab(tab: typeof activeTab.value) {
  activeTab.value = tab
  if (tab === 'accounts') void loadCompanyAccounts()
  if (tab === 'disbursements') void loadDisbursements()
  if (tab === 'reconciliation') void loadReconciliation()
  if (tab === 'balance') void loadDailyBalance()
}

onMounted(async () => {
  loading.value = true
  await loadCompanyAccounts()
  await loadDisbursements()
  await loadReconciliation()
  await loadDailyBalance()
  loading.value = false
})
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Tesorería y Conciliación Bancaria"
      description="Cuentas de la Fintech, control de desembolsos en tránsito, conciliación de extractos y cuadre diario (FE-BO-27 a FE-BO-30)."
    />

    <!-- ── Pestañas del Módulo de Tesorería ── -->
    <div class="flex items-center gap-1 border-b text-sm">
      <button
        class="px-4 py-2.5 font-semibold border-b-2 transition-all flex items-center gap-2"
        :class="
          activeTab === 'accounts'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground hover:text-foreground'
        "
        @click="switchTab('accounts')"
      >
        <Building2 class="size-4" />
        Cuentas Propias (FE-BO-27)
        <Badge variant="outline" class="ml-1 text-[10px]">
          {{ companyAccounts.length }}
        </Badge>
      </button>

      <button
        class="px-4 py-2.5 font-semibold border-b-2 transition-all flex items-center gap-2"
        :class="
          activeTab === 'disbursements'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground hover:text-foreground'
        "
        @click="switchTab('disbursements')"
      >
        <ArrowUpRight class="size-4" />
        Desembolsos en Tránsito y Fallidos (FE-BO-28)
        <Badge
          v-if="disbursements.some((d) => d.status === 'FAILED')"
          variant="destructive"
          class="ml-1 text-[10px]"
        >
          {{ disbursements.filter((d) => d.status === 'FAILED').length }} fallidos
        </Badge>
      </button>

      <button
        class="px-4 py-2.5 font-semibold border-b-2 transition-all flex items-center gap-2"
        :class="
          activeTab === 'reconciliation'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground hover:text-foreground'
        "
        @click="switchTab('reconciliation')"
      >
        <UploadCloud class="size-4" />
        Conciliación de Extractos (FE-BO-29)
      </button>

      <button
        class="px-4 py-2.5 font-semibold border-b-2 transition-all flex items-center gap-2"
        :class="
          activeTab === 'balance'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground hover:text-foreground'
        "
        @click="switchTab('balance')"
      >
        <CheckCircle2 class="size-4" />
        Cuadre Diario (FE-BO-30)
      </button>
    </div>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- PESTAÑA 1: CUENTAS CORPORATIVAS DE LA FINTECH (FE-BO-27)   -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <div v-if="activeTab === 'accounts'" class="space-y-4">
      <div class="flex items-center justify-between">
        <div>
          <h3 class="text-base font-semibold">Cuentas Bancarias de la Empresa</h3>
          <p class="text-xs text-muted-foreground">
            Cuentas habilitadas para emitir transferencias de desembolso y/o recibir pagos de prestatarios.
          </p>
        </div>
        <Button size="sm" @click="isAccountModalOpen = true">
          <Building2 class="size-4 mr-1.5" />
          Nueva Cuenta Corporativa
        </Button>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Card
          v-for="acc in companyAccounts"
          :key="acc.id"
          class="transition-all"
          :class="{ 'opacity-70 bg-muted/20': !acc.isActive }"
        >
          <CardHeader class="pb-3">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <Badge variant="outline" class="font-mono text-xs">
                  {{ acc.currency }}
                </Badge>
                <CardTitle class="text-base font-bold">{{ acc.bankName }}</CardTitle>
              </div>
              <Badge
                variant="outline"
                :class="acc.isActive ? 'border-success text-success bg-success/10' : 'border-border text-muted-foreground'"
                class="text-[10px]"
              >
                {{ acc.isActive ? 'Activa' : 'Inactiva' }}
              </Badge>
            </div>
            <CardDescription class="text-xs font-mono pt-1">
              {{ acc.accountNumberMasked }} · CCI: {{ acc.cci || 'Sin CCI' }}
            </CardDescription>
          </CardHeader>
          <CardContent class="space-y-3 pt-0 text-xs">
            <!-- Finalidades activas -->
            <div class="flex flex-wrap gap-1.5 pt-1">
              <Badge v-if="acc.isDisbursement" variant="outline" class="border-primary/50 text-primary bg-primary/5 text-[10px]">
                Desembolsos
              </Badge>
              <Badge v-if="acc.isCollection" variant="outline" class="border-success/50 text-success bg-success/5 text-[10px]">
                Recaudación / Cobro
              </Badge>
            </div>

            <div class="pt-2 border-t flex justify-end">
              <Button
                variant="ghost"
                size="sm"
                class="h-8 text-xs"
                @click="toggleAccountActive(acc)"
              >
                {{ acc.isActive ? 'Desactivar Cuenta' : 'Activar Cuenta' }}
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- PESTAÑA 2: DESEMBOLSOS EN TRÁNSITO Y FALLIDOS (FE-BO-28)   -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <div v-if="activeTab === 'disbursements'" class="space-y-4">
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div>
          <h3 class="text-base font-semibold">Bandeja de Transferencias Interbancarias</h3>
          <p class="text-xs text-muted-foreground">
            Monitoreo en tiempo real de órdenes de desembolso con alertas por retraso bancario.
          </p>
        </div>

        <div class="flex items-center gap-2">
          <select
            v-model="selectedDisbursementStatus"
            class="flex h-8 rounded-md border border-input bg-transparent px-2.5 py-1 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            @change="loadDisbursements"
          >
            <option value="PENDING,PROCESSING,FAILED">En Tránsito y Fallidos</option>
            <option value="">Todos los Desembolsos</option>
            <option value="COMPLETED">Solo Confirmados</option>
            <option value="FAILED">Solo Fallidos</option>
          </select>

          <Button variant="outline" size="sm" class="h-8" @click="loadDisbursements">
            <RefreshCw class="size-3.5 mr-1" />
            Refrescar
          </Button>
        </div>
      </div>

      <Card>
        <CardContent class="p-0">
          <div v-if="disbursements.length === 0" class="p-12">
            <EmptyState
              :icon="CheckCircle2"
              title="No hay desembolsos pendientes ni fallidos"
              description="Todas las órdenes de transferencia han sido confirmadas por la red bancaria."
            />
          </div>

          <div v-else class="overflow-x-auto">
            <table class="w-full text-sm text-left">
              <thead class="border-b bg-muted/60 text-xs font-medium text-muted-foreground">
                <tr>
                  <th class="px-4 py-3">ID Orden</th>
                  <th class="px-4 py-3">Iniciado</th>
                  <th class="px-4 py-3">Destinatario</th>
                  <th class="px-4 py-3">Importe</th>
                  <th class="px-4 py-3">Canal</th>
                  <th class="px-4 py-3">Estado Bancario</th>
                  <th class="px-4 py-3">Detalle / Error Banco</th>
                  <th class="px-4 py-3 text-right">Acción</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-border text-xs">
                <tr
                  v-for="d in disbursements"
                  :key="d.id"
                  class="hover:bg-muted/40 transition-colors"
                  :class="{ 'bg-destructive/5': d.status === 'FAILED' }"
                >
                  <td class="px-4 py-3 font-mono font-bold">{{ d.id }}</td>
                  <td class="px-4 py-3 whitespace-nowrap text-muted-foreground">
                    {{ formatInstant(d.initiatedAt) }}
                  </td>
                  <td class="px-4 py-3 font-medium text-foreground">
                    {{ d.customerName }}
                    <span class="text-[11px] text-muted-foreground block font-normal">
                      Préstamo: {{ d.loanId }}
                    </span>
                  </td>
                  <td class="px-4 py-3 font-mono font-bold text-foreground">
                    <MoneyText :amount="d.amount" />
                  </td>
                  <td class="px-4 py-3 font-medium">{{ d.channel }}</td>
                  <td class="px-4 py-3">
                    <StatusBadge kind="disbursement" :status="d.status" />
                  </td>

                  <!-- Alertas y Motivo de Rechazo (FE-BO-28) -->
                  <td class="px-4 py-3">
                    <div v-if="d.status === 'FAILED'" class="text-destructive font-medium flex items-center gap-1.5">
                      <AlertTriangle class="size-3.5 shrink-0" />
                      <span>[{{ d.bankResponseCode }}] {{ d.bankResponseMessage || 'Cuenta rechazada por el banco' }}</span>
                    </div>
                    <div v-else-if="d.status === 'PROCESSING'" class="text-info flex items-center gap-1">
                      <Clock class="size-3.5" /> Enrutando con cámara de compensación
                    </div>
                    <div v-else-if="d.status === 'COMPLETED'" class="text-success flex items-center gap-1">
                      <CheckCircle2 class="size-3.5" /> Op: {{ d.operationNumber }}
                    </div>
                  </td>

                  <td class="px-4 py-3 text-right">
                    <!-- Reintento si falló (FE-BO-28) -->
                    <Button
                      v-if="d.status === 'FAILED'"
                      size="sm"
                      variant="outline"
                      class="h-8 gap-1 text-xs text-destructive border-destructive/40 hover:bg-destructive/10"
                      :disabled="retryingDisbursementId === d.id"
                      @click="retryDisbursement(d)"
                    >
                      <RotateCcw class="size-3.5" />
                      <span>Reintentar</span>
                    </Button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- PESTAÑA 3: CONCILIACIÓN DE EXTRACTOS (FE-BO-29)           -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <div v-if="activeTab === 'reconciliation'" class="space-y-4">
      <div class="flex items-center justify-between">
        <div>
          <h3 class="text-base font-semibold">Extractos Bancarios y Conciliación de Abonos</h3>
          <p class="text-xs text-muted-foreground">
            Líneas del extracto bancario conciliadas automáticamente o pendientes de emparejar.
          </p>
        </div>
        <Button size="sm" @click="isImportModalOpen = true">
          <UploadCloud class="size-4 mr-1.5" />
          Importar Extracto
        </Button>
      </div>

      <Card>
        <CardContent class="p-0">
          <div v-if="statements.length === 0" class="p-12">
            <EmptyState
              :icon="UploadCloud"
              title="No hay extractos cargados"
              description="Importa un archivo CSV o extracto bancario para iniciar el proceso de match automático."
            >
              <Button size="sm" @click="isImportModalOpen = true">
                <UploadCloud class="size-4 mr-1.5" />
                Importar Extracto
              </Button>
            </EmptyState>
          </div>

          <div v-else class="overflow-x-auto">
            <table class="w-full text-sm text-left">
              <thead class="border-b bg-muted/60 text-xs font-medium text-muted-foreground">
                <tr>
                  <th class="px-4 py-3">Nº Operación</th>
                  <th class="px-4 py-3">Fecha Valor</th>
                  <th class="px-4 py-3">Importe Abono / Cargo</th>
                  <th class="px-4 py-3">Descripción en Extracto</th>
                  <th class="px-4 py-3">Estado de Conciliación</th>
                  <th class="px-4 py-3 text-right">Acción</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-border text-xs">
                <tr
                  v-for="s in statements"
                  :key="s.id"
                  class="hover:bg-muted/40 transition-colors"
                >
                  <td class="px-4 py-3 font-mono font-bold">{{ s.operationNumber }}</td>
                  <td class="px-4 py-3 whitespace-nowrap">{{ formatBusinessDate(s.valueDate) }}</td>
                  <td
                    class="px-4 py-3 font-mono font-bold"
                    :class="Number(s.amount) > 0 ? 'text-success' : 'text-destructive'"
                  >
                    <MoneyText :amount="s.amount" />
                  </td>
                  <td class="px-4 py-3 text-foreground font-medium max-w-xs truncate">
                    {{ s.description }}
                  </td>
                  <td class="px-4 py-3">
                    <Badge
                      variant="outline"
                      class="text-[10px]"
                      :class="
                        s.reconciliationStatus === 'CONCILIADO_AUTOMATICO'
                          ? 'border-success text-success bg-success/10'
                          : s.reconciliationStatus === 'PENDIENTE_CONCILIACION'
                            ? 'border-warning text-warning bg-warning/10'
                            : 'border-destructive text-destructive bg-destructive/10'
                      "
                    >
                      {{ s.reconciliationStatus }}
                    </Badge>
                  </td>
                  <td class="px-4 py-3 text-right">
                    <!-- Conciliación manual para no identificados (FE-BO-29) -->
                    <Button
                      v-if="s.reconciliationStatus === 'PENDIENTE_CONCILIACION'"
                      size="sm"
                      variant="outline"
                      class="h-8 gap-1 text-xs"
                      @click="openMatch(s)"
                    >
                      <ArrowRightLeft class="size-3.5" />
                      <span>Conciliar Manual</span>
                    </Button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- PESTAÑA 4: CUADRE DIARIO POR CUENTA (FE-BO-30)            -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <div v-if="activeTab === 'balance'" class="space-y-4">
      <Card>
        <CardHeader class="pb-3">
          <CardTitle class="text-base flex items-center justify-between">
            <span>Cuadre Diario de Caja y Bancos (FE-BO-30)</span>
            <Badge
              v-if="dailyBalance"
              variant="outline"
              class="text-xs px-2.5 py-1 font-bold"
              :class="
                dailyBalance.result === 'CUADRADO'
                  ? 'border-success text-success bg-success/10'
                  : 'border-destructive text-destructive bg-destructive/10'
              "
            >
              {{ dailyBalance.result }}
            </Badge>
          </CardTitle>
          <CardDescription>
            Comparación automática entre el libro mayor financiero y el saldo reportado por el extracto.
          </CardDescription>
        </CardHeader>
        <CardContent class="space-y-6">
          <!-- Filtro de cuenta y fecha -->
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div class="space-y-1.5">
              <label for="balAcc" class="text-xs font-medium">Cuenta Corporativa</label>
              <select
                id="balAcc"
                v-model="selectedBalanceAccId"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
                @change="loadDailyBalance"
              >
                <option v-for="a in companyAccounts" :key="a.id" :value="a.id">
                  {{ a.bankName }} ({{ a.accountNumberMasked }})
                </option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label for="balDate" class="text-xs font-medium">Fecha de Cuadre</label>
              <input
                id="balDate"
                v-model="selectedBalanceDate"
                type="date"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
                @change="loadDailyBalance"
              />
            </div>

            <div class="flex items-end">
              <Button size="sm" class="h-9 w-full" :disabled="loadingBalance" @click="loadDailyBalance">
                <RefreshCw class="size-3.5 mr-1.5" :class="{ 'animate-spin': loadingBalance }" />
                Calcular Cuadre
              </Button>
            </div>
          </div>

          <!-- Indicadores de Balance -->
          <div v-if="dailyBalance" class="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div class="p-4 rounded-xl border bg-card space-y-1">
              <span class="text-xs text-muted-foreground block">Flujo Neto Esperado (Libro Mayor):</span>
              <span class="text-xl font-bold text-foreground">
                <MoneyText :amount="dailyBalance.expectedBalance" />
              </span>
            </div>

            <div class="p-4 rounded-xl border bg-card space-y-1">
              <span class="text-xs text-muted-foreground block">Saldo Neto en Extracto Bancario:</span>
              <span class="text-xl font-bold text-foreground">
                <MoneyText :amount="dailyBalance.statementBalance" />
              </span>
            </div>

            <div
              class="p-4 rounded-xl border space-y-1"
              :class="
                dailyBalance.result === 'CUADRADO'
                  ? 'bg-success/5 border-success/30'
                  : 'bg-destructive/5 border-destructive/30'
              "
            >
              <span class="text-xs text-muted-foreground block">Diferencia de Cuadre:</span>
              <span
                class="text-xl font-bold"
                :class="dailyBalance.result === 'CUADRADO' ? 'text-success' : 'text-destructive'"
              >
                <MoneyText :amount="dailyBalance.difference" />
              </span>
            </div>
          </div>

          <!-- Detalle de incidencias si está descuadrado -->
          <div v-if="dailyBalance && dailyBalance.issues.length > 0" class="rounded-lg border border-destructive/40 bg-destructive/5 p-4 space-y-2">
            <span class="text-xs font-bold text-destructive flex items-center gap-1.5">
              <ShieldAlert class="size-4" /> Partidas Pendientes que originan el descuadre:
            </span>
            <ul class="list-disc list-inside text-xs text-muted-foreground space-y-1">
              <li v-for="issue in dailyBalance.issues" :key="issue">
                {{ issue }}
              </li>
            </ul>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- ── Modales de Tesorería ── -->
    <CompanyAccountModal
      v-model:open="isAccountModalOpen"
      @saved="loadCompanyAccounts"
    />

    <ImportStatementModal
      v-model:open="isImportModalOpen"
      :company-accounts="companyAccounts"
      @imported="loadReconciliation"
    />

    <ManualMatchModal
      v-model:open="isMatchModalOpen"
      :statement-line="statementToMatch"
      @matched="loadReconciliation"
    />
  </div>
</template>
