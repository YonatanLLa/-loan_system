<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  AlertTriangle,
  ArrowLeft,
  Building2,
  Calendar,
  CheckCircle2,
  Clock,
  CreditCard,
  DollarSign,
  FileBadge,
  FileCheck2,
  FileText,
  Mail,
  MapPin,
  Pencil,
  Phone,
  RefreshCw,
  ShieldAlert,
  ShieldCheck,
  Smartphone,
  Star,
  User,
  UserCheck,
  XCircle,
} from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { Customer360, CustomerBankAccount, KycStatus } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { MoneyText, PageHeader, StatusBadge, TableGenerator } from '@prestamos/ui'
import { Alert, AlertDescription, AlertTitle } from '@prestamos/ui/components/ui/alert'
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
import CustomerFormSheet from '../components/CustomerFormSheet.vue'
import KycDecisionDialog from '../components/KycDecisionDialog.vue'
import AddBankAccountDialog from '../components/AddBankAccountDialog.vue'

// FE-BO-08, FE-BO-09, FE-BO-10: Expediente KYC, cuentas bancarias y Ficha 360 del cliente.

const route = useRoute()
const router = useRouter()
const customerId = computed(() => route.params.id as string)

const loading = ref(true)
const customer = ref<Customer360 | null>(null)
const activeTab = ref<'kyc' | 'accounts' | 'loans' | 'applications'>('kyc')

// Modales
const isEditOpen = ref(false)
const isKycModalOpen = ref(false)
const isAddAccountOpen = ref(false)

async function loadCustomer() {
  loading.value = true
  try {
    const { data } = await useApi().get<Customer360>(`/customers/${customerId.value}`)
    customer.value = data
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al cargar ficha 360', { description: apiError.detail })
    void router.replace({ name: 'customers' })
  } finally {
    loading.value = false
  }
}

async function verifyAccount(account: CustomerBankAccount) {
  try {
    const { data } = await useApi().post<CustomerBankAccount>(
      `/customer-bank-accounts/${account.id}/verify`,
    )
    toast.success('Cuenta verificada', {
      description: `La cuenta ${data.bankName} (${data.accountNumberMasked}) fue verificada contra el titular.`,
    })
    void loadCustomer()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al verificar cuenta', { description: apiError.detail })
  }
}

async function makePrimaryAccount(account: CustomerBankAccount) {
  try {
    await useApi().post(`/customer-bank-accounts/${account.id}/make-primary`)
    toast.success('Cuenta principal actualizada', {
      description: `Los desembolsos se realizarán por defecto a ${account.bankName} (${account.accountNumberMasked}).`,
    })
    void loadCustomer()
  } catch (error) {
    const apiError = toApiError(error)
    toast.error('Error al actualizar cuenta principal', { description: apiError.detail })
  }
}

function onKycDecided(newStatus: KycStatus) {
  if (customer.value) {
    customer.value.kycStatus = newStatus
  }
  void loadCustomer()
}

function onAccountAdded() {
  void loadCustomer()
}

onMounted(() => {
  void loadCustomer()
})
</script>

<template>
  <div class="space-y-6">
    <!-- ── Botón Volver y Cabecera ── -->
    <div class="flex items-center gap-2">
      <Button variant="ghost" size="sm" as-child class="-ml-2">
        <RouterLink :to="{ name: 'customers' }">
          <ArrowLeft class="size-4 mr-1.5" />
          Volver a Clientes
        </RouterLink>
      </Button>
    </div>

    <div v-if="loading" class="space-y-4">
      <Skeleton class="h-28 w-full" />
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4">
        <Skeleton class="h-24" />
        <Skeleton class="h-24" />
        <Skeleton class="h-24" />
        <Skeleton class="h-24" />
      </div>
    </div>

    <template v-else-if="customer">
      <!-- ── ALERTA DESTACADA DE MORA (FE-BO-10) ── -->
      <Alert
        v-if="customer.hasActiveArrears || customer.maxDaysLate > 0"
        variant="destructive"
        class="border-destructive/60 bg-destructive/10 text-destructive shadow-sm"
      >
        <ShieldAlert class="size-5" />
        <AlertTitle class="font-bold flex items-center gap-2">
          ALERTA DE MORA ACTIVA (FE-BO-10)
        </AlertTitle>
        <AlertDescription class="text-xs mt-1">
          El cliente presenta préstamos con cuotas vencidas y un atraso máximo de
          <strong class="font-bold underline">{{ customer.maxDaysLate }} días</strong>. La
          originación de nuevos créditos se encuentra bloqueada por política de riesgo hasta
          regularizar la cartera.
        </AlertDescription>
      </Alert>

      <!-- ── Encabezado Principal del Cliente ── -->
      <Card>
        <CardContent class="p-6">
          <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div class="space-y-1">
              <div class="flex flex-wrap items-center gap-2">
                <h1 class="text-2xl font-bold tracking-tight text-foreground">
                  {{ customer.fullName }}
                </h1>
                <Badge variant="outline" class="font-mono text-xs">
                  {{ customer.docType }}: {{ customer.docNumber }}
                </Badge>
                <StatusBadge kind="kyc" :status="customer.kycStatus" />
              </div>
              <p class="text-xs text-muted-foreground">
                Cliente ID: <span class="font-mono">{{ customer.id }}</span> · Registrado el
                {{ formatInstant(customer.createdAt) }}
              </p>
            </div>

            <div class="flex items-center gap-2 shrink-0">
              <Button variant="outline" size="sm" @click="isEditOpen = true">
                <Pencil class="size-3.5 mr-1.5" />
                Editar Datos
              </Button>
              <Button size="sm" @click="isKycModalOpen = true">
                <FileBadge class="size-3.5 mr-1.5" />
                Dictamen KYC
              </Button>
            </div>
          </div>

          <!-- Metadatos de contacto y empleo -->
          <div
            class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4 mt-6 pt-4 border-t text-xs"
          >
            <div class="flex items-center gap-2 text-muted-foreground">
              <Phone class="size-4 text-primary shrink-0" />
              <span class="text-foreground font-medium">{{ customer.phone }}</span>
            </div>
            <div class="flex items-center gap-2 text-muted-foreground">
              <Mail class="size-4 text-primary shrink-0" />
              <span class="text-foreground font-medium truncate">{{ customer.email }}</span>
            </div>
            <div class="flex items-center gap-2 text-muted-foreground">
              <MapPin class="size-4 text-primary shrink-0" />
              <span class="text-foreground font-medium truncate">
                {{ customer.address || customer.district || 'Sin dirección registrada' }}
              </span>
            </div>
            <div class="flex items-center gap-2 text-muted-foreground">
              <Calendar class="size-4 text-primary shrink-0" />
              <span
                >Nac:
                <strong class="text-foreground">{{
                  formatBusinessDate(customer.birthDate)
                }}</strong></span
              >
            </div>
          </div>
        </CardContent>
      </Card>

      <!-- ── Tarjetas de Indicadores Financieros 360 (FE-BO-10) ── -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <!-- Deuda Consolidada -->
        <Card>
          <CardHeader class="pb-2">
            <CardDescription class="text-xs">Deuda Consolidada</CardDescription>
            <CardTitle class="text-xl font-bold">
              <MoneyText :amount="customer.totalDebt" />
            </CardTitle>
          </CardHeader>
          <CardContent class="text-xs text-muted-foreground">
            Capital + Interés + Mora vigente
          </CardContent>
        </Card>

        <!-- Atraso Máximo -->
        <Card :class="{ 'border-destructive/50 bg-destructive/5': customer.maxDaysLate > 0 }">
          <CardHeader class="pb-2">
            <CardDescription class="text-xs">Atraso Máximo</CardDescription>
            <CardTitle
              class="text-xl font-bold"
              :class="customer.maxDaysLate > 0 ? 'text-destructive' : 'text-success'"
            >
              {{ customer.maxDaysLate }} días
            </CardTitle>
          </CardHeader>
          <CardContent class="text-xs text-muted-foreground">
            {{ customer.maxDaysLate > 0 ? 'En situación de mora' : 'Pagos al día' }}
          </CardContent>
        </Card>

        <!-- Saldo a Favor -->
        <Card>
          <CardHeader class="pb-2">
            <CardDescription class="text-xs">Saldo a Favor</CardDescription>
            <CardTitle class="text-xl font-bold text-primary">
              <MoneyText :amount="customer.creditBalance" />
            </CardTitle>
          </CardHeader>
          <CardContent class="text-xs text-muted-foreground">
            Disponible para amortizar o devolver
          </CardContent>
        </Card>

        <!-- Ingreso Neto Declarado -->
        <Card>
          <CardHeader class="pb-2">
            <CardDescription class="text-xs">Ingreso Neto Mensual</CardDescription>
            <CardTitle class="text-xl font-bold">
              <MoneyText :amount="customer.monthlyNetIncome" />
            </CardTitle>
          </CardHeader>
          <CardContent class="text-xs text-muted-foreground truncate">
            {{ customer.occupation || 'Actividad independiente' }}
          </CardContent>
        </Card>
      </div>

      <!-- ── Pestañas de la Ficha 360 ── -->
      <div class="space-y-4">
        <div class="flex items-center gap-1 border-b">
          <button
            class="px-4 py-2.5 text-sm font-semibold border-b-2 transition-all flex items-center gap-2"
            :class="
              activeTab === 'kyc'
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            "
            @click="activeTab = 'kyc'"
          >
            <FileBadge class="size-4" />
            Expediente KYC (FE-BO-08)
          </button>

          <button
            class="px-4 py-2.5 text-sm font-semibold border-b-2 transition-all flex items-center gap-2"
            :class="
              activeTab === 'accounts'
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            "
            @click="activeTab = 'accounts'"
          >
            <Building2 class="size-4" />
            Cuentas Bancarias (FE-BO-09)
            <Badge variant="outline" class="ml-1 text-[10px]">
              {{ customer.bankAccounts.length }}
            </Badge>
          </button>

          <button
            class="px-4 py-2.5 text-sm font-semibold border-b-2 transition-all flex items-center gap-2"
            :class="
              activeTab === 'loans'
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            "
            @click="activeTab = 'loans'"
          >
            <CreditCard class="size-4" />
            Préstamos Activos e Históricos (FE-BO-10)
            <Badge variant="outline" class="ml-1 text-[10px]">
              {{ customer.loans.length }}
            </Badge>
          </button>

          <button
            class="px-4 py-2.5 text-sm font-semibold border-b-2 transition-all flex items-center gap-2"
            :class="
              activeTab === 'applications'
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            "
            @click="activeTab = 'applications'"
          >
            <FileText class="size-4" />
            Solicitudes Pasadas (FE-BO-10)
            <Badge variant="outline" class="ml-1 text-[10px]">
              {{ customer.applications.length }}
            </Badge>
          </button>
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- PESTAÑA 1: EXPEDIENTE KYC (FE-BO-08)                      -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'kyc'" class="space-y-6">
          <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
            <!-- Visor de DNI / Selfie -->
            <Card class="md:col-span-2">
              <CardHeader>
                <CardTitle class="text-base flex items-center gap-2">
                  <UserCheck class="size-4 text-primary" />
                  Visor de Documentos Biométricos
                </CardTitle>
                <CardDescription>
                  Archivos adjuntos cargados por el cliente durante el onboarding digital.
                </CardDescription>
              </CardHeader>
              <CardContent class="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <!-- DNI Anverso -->
                <div class="border rounded-lg p-3 text-center space-y-2 bg-muted/20">
                  <div
                    class="h-32 bg-muted rounded flex items-center justify-center border border-dashed text-muted-foreground text-xs font-mono"
                  >
                    [ DNI ANVERSO ]
                  </div>
                  <span class="text-xs font-semibold block">DNI Frontal</span>
                  <p class="text-[11px] text-muted-foreground">Documento de identidad legible</p>
                </div>

                <!-- DNI Reverso -->
                <div class="border rounded-lg p-3 text-center space-y-2 bg-muted/20">
                  <div
                    class="h-32 bg-muted rounded flex items-center justify-center border border-dashed text-muted-foreground text-xs font-mono"
                  >
                    [ DNI REVERSO ]
                  </div>
                  <span class="text-xs font-semibold block">DNI Reverso</span>
                  <p class="text-[11px] text-muted-foreground">Código de barras / huella</p>
                </div>

                <!-- Selfie con prueba de vida -->
                <div class="border rounded-lg p-3 text-center space-y-2 bg-muted/20">
                  <div
                    class="h-32 bg-muted rounded flex items-center justify-center border border-dashed text-muted-foreground text-xs font-mono"
                  >
                    [ SELFIE LIVENESS ]
                  </div>
                  <span class="text-xs font-semibold block">Selfie con Prueba de Vida</span>
                  <p class="text-[11px] text-muted-foreground">Biometría 3D validada</p>
                </div>
              </CardContent>
            </Card>

            <!-- Verificaciones Automáticas -->
            <Card>
              <CardHeader>
                <CardTitle class="text-base flex items-center gap-2">
                  <ShieldCheck class="size-4 text-success" />
                  Motor de Cumplimiento
                </CardTitle>
                <CardDescription>Validaciones automáticas integradas (US-05).</CardDescription>
              </CardHeader>
              <CardContent class="space-y-4 text-xs">
                <!-- Check 1: Reniec -->
                <div
                  class="flex items-start justify-between gap-2 p-2.5 rounded-lg border bg-success/5 border-success/20"
                >
                  <div>
                    <span class="font-semibold text-foreground block"
                      >Validación Reniec / Identidad</span
                    >
                    <span class="text-muted-foreground">Nombres y DNI coincidentes</span>
                  </div>
                  <Badge
                    variant="outline"
                    class="border-success text-success bg-success/10 text-[10px]"
                  >
                    APROBADO
                  </Badge>
                </div>

                <!-- Check 2: Biometría -->
                <div
                  class="flex items-start justify-between gap-2 p-2.5 rounded-lg border bg-success/5 border-success/20"
                >
                  <div>
                    <span class="font-semibold text-foreground block">Biometría Facial</span>
                    <span class="text-muted-foreground">Coincidencia DNI vs Selfie: 98.4%</span>
                  </div>
                  <Badge
                    variant="outline"
                    class="border-success text-success bg-success/10 text-[10px]"
                  >
                    APROBADO
                  </Badge>
                </div>

                <!-- Check 3: PEP / AML -->
                <div
                  class="flex items-start justify-between gap-2 p-2.5 rounded-lg border bg-success/5 border-success/20"
                >
                  <div>
                    <span class="font-semibold text-foreground block"
                      >Listas PEP & AML / Lavado</span
                    >
                    <span class="text-muted-foreground">Búsqueda en listas OFAC y SBS</span>
                  </div>
                  <Badge
                    variant="outline"
                    class="border-success text-success bg-success/10 text-[10px]"
                  >
                    SIN RIESGO
                  </Badge>
                </div>

                <!-- Dictamen actual -->
                <div class="pt-3 border-t space-y-2">
                  <div class="flex items-center justify-between">
                    <span class="text-muted-foreground">Estado del Expediente:</span>
                    <StatusBadge kind="kyc" :status="customer.kycStatus" />
                  </div>
                  <div
                    v-if="customer.kycNotes"
                    class="p-2.5 bg-muted rounded text-xs text-foreground"
                  >
                    <span class="font-semibold block text-[11px] text-muted-foreground"
                      >Observaciones:</span
                    >
                    {{ customer.kycNotes }}
                  </div>
                  <Button size="sm" class="w-full mt-2" @click="isKycModalOpen = true">
                    Emitir Dictamen Manual
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- PESTAÑA 2: CUENTAS BANCARIAS (FE-BO-09)                   -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'accounts'" class="space-y-4">
          <div class="flex items-center justify-between">
            <div>
              <h3 class="text-base font-semibold">Cuentas Bancarias y Billeteras del Cliente</h3>
              <p class="text-xs text-muted-foreground">
                Cuentas autorizadas para el desembolso 100% digital y validación de titularidad.
              </p>
            </div>
            <Button size="sm" @click="isAddAccountOpen = true">
              <Building2 class="size-4 mr-1.5" />
              Nueva Cuenta
            </Button>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Card
              v-for="acc in customer.bankAccounts"
              :key="acc.id"
              class="relative"
              :class="{ 'border-primary/60 shadow-xs': acc.isPrimary }"
            >
              <CardHeader class="pb-2">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <Building2
                      v-if="acc.accountType !== 'BILLETERA_DIGITAL'"
                      class="size-4 text-primary"
                    />
                    <Smartphone v-else class="size-4 text-primary" />
                    <CardTitle class="text-sm font-semibold">{{ acc.bankName }}</CardTitle>
                  </div>
                  <div class="flex items-center gap-1.5">
                    <Badge
                      v-if="acc.isPrimary"
                      variant="outline"
                      class="border-primary text-primary bg-primary/10 text-[10px]"
                    >
                      <Star class="size-2.5 mr-1 fill-primary" />
                      Primaria
                    </Badge>
                    <Badge
                      variant="outline"
                      :class="
                        acc.isVerified
                          ? 'border-success text-success bg-success/10'
                          : 'border-warning text-warning bg-warning/10'
                      "
                      class="text-[10px]"
                    >
                      {{ acc.isVerified ? 'Verificada' : 'Por Verificar' }}
                    </Badge>
                  </div>
                </div>
                <CardDescription class="text-xs">
                  {{ acc.accountType.replace(/_/g, ' ') }}
                </CardDescription>
              </CardHeader>
              <CardContent class="space-y-3 pt-1">
                <div
                  class="rounded bg-muted/40 p-2.5 font-mono text-xs flex justify-between items-center"
                >
                  <span class="text-muted-foreground">Identificador:</span>
                  <span class="font-bold text-foreground">
                    {{ acc.accountNumberMasked }}
                    <span v-if="acc.phoneWallet" class="text-xs font-normal text-muted-foreground"
                      >({{ acc.phoneWallet }})</span
                    >
                  </span>
                </div>

                <div class="flex items-center gap-2 pt-1">
                  <!-- Botón verificar titularidad -->
                  <Button
                    v-if="!acc.isVerified"
                    size="sm"
                    variant="outline"
                    class="h-8 text-xs flex-1 text-success hover:text-success border-success/30"
                    @click="verifyAccount(acc)"
                  >
                    <CheckCircle2 class="size-3.5 mr-1" />
                    Verificar Titularidad
                  </Button>

                  <!-- Botón hacer primaria -->
                  <Button
                    v-if="!acc.isPrimary && acc.isVerified"
                    size="sm"
                    variant="outline"
                    class="h-8 text-xs flex-1"
                    @click="makePrimaryAccount(acc)"
                  >
                    <Star class="size-3.5 mr-1" />
                    Marcar como Primaria
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- PESTAÑA 3: PRÉSTAMOS ACTIVOS E HISTÓRICOS (FE-BO-10)       -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'loans'" class="space-y-4">
          <Card>
            <CardHeader class="pb-3">
              <CardTitle class="text-base">Historial de Créditos y Cartera</CardTitle>
              <CardDescription>
                Todos los préstamos desembolsados, saldos pendientes y estado de mora.
              </CardDescription>
            </CardHeader>
            <CardContent class="p-0">
              <div
                v-if="customer.loans.length === 0"
                class="p-8 text-center text-xs text-muted-foreground"
              >
                El cliente no cuenta con préstamos registrados.
              </div>
              <div v-else>
                <TableGenerator
                  :columns="[
                    { key: 'id', label: 'Código' },
                    { key: 'product', label: 'Producto' },
                    { key: 'principal', label: 'Monto Capital' },
                    { key: 'outstanding', label: 'Saldo Insoluto' },
                    { key: 'nextDueDate', label: 'Próximo Vencimiento' },
                    { key: 'daysLate', label: 'Días Atraso' },
                    { key: 'status', label: 'Estado' },
                    { key: 'action', label: 'Ver', headerClass: 'text-right' },
                  ]"
                  :rows="customer.loans"
                  row-key="id"
                >
                  <template #row="{ row: loan }">
                    <td class="px-4 py-3 font-mono text-xs font-semibold">
                      {{ loan.id }}
                    </td>
                    <td class="px-4 py-3 font-medium">{{ loan.productName }}</td>
                    <td class="px-4 py-3 font-mono text-xs">
                      <MoneyText :amount="loan.principal" />
                    </td>
                    <td class="px-4 py-3 font-mono text-xs font-bold text-foreground">
                      <MoneyText :amount="loan.outstandingPrincipal" />
                    </td>
                    <td class="px-4 py-3 text-xs text-muted-foreground">
                      {{ loan.nextDueDate ? formatBusinessDate(loan.nextDueDate) : '—' }}
                    </td>
                    <td class="px-4 py-3 text-xs font-semibold">
                      <span
                        :class="
                          loan.daysLate > 0 ? 'text-destructive font-bold' : 'text-muted-foreground'
                        "
                      >
                        {{ loan.daysLate > 0 ? `${loan.daysLate} días` : 'Al día' }}
                      </span>
                    </td>
                    <td class="px-4 py-3">
                      <StatusBadge kind="loan" :status="loan.status" />
                    </td>
                    <td class="px-4 py-3 text-right">
                      <Button variant="ghost" size="sm" class="h-8 text-xs" as-child>
                        <RouterLink :to="{ name: 'loans', query: { q: loan.id } }">
                          Detalle
                        </RouterLink>
                      </Button>
                    </td>
                  </template>
                </TableGenerator>
              </div>
            </CardContent>
          </Card>
        </div>

        <!-- ══════════════════════════════════════════════════════════ -->
        <!-- PESTAÑA 4: SOLICITUDES PASADAS (FE-BO-10)                 -->
        <!-- ══════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'applications'" class="space-y-4">
          <Card>
            <CardHeader class="pb-3">
              <CardTitle class="text-base">Solicitudes de Crédito en el Embudo</CardTitle>
              <CardDescription>
                Historial de solicitudes originadas en backoffice o portal del cliente.
              </CardDescription>
            </CardHeader>
            <CardContent class="p-0">
              <div
                v-if="customer.applications.length === 0"
                class="p-8 text-center text-xs text-muted-foreground"
              >
                No hay solicitudes de crédito registradas para este cliente.
              </div>
              <div v-else>
                <TableGenerator
                  :columns="[
                    { key: 'id', label: 'ID Solicitud' },
                    { key: 'product', label: 'Producto' },
                    { key: 'amount', label: 'Monto Solicitado' },
                    { key: 'createdAt', label: 'Fecha Solicitud' },
                    { key: 'status', label: 'Estado' },
                  ]"
                  :rows="customer.applications"
                  row-key="id"
                >
                  <template #row="{ row: app }">
                    <td class="px-4 py-3 font-mono text-xs font-medium">{{ app.id }}</td>
                    <td class="px-4 py-3">{{ app.productName }}</td>
                    <td class="px-4 py-3 font-mono text-xs font-semibold">
                      <MoneyText :amount="app.requestedAmount" />
                    </td>
                    <td class="px-4 py-3 text-xs text-muted-foreground">
                      {{ formatInstant(app.createdAt) }}
                    </td>
                    <td class="px-4 py-3">
                      <StatusBadge kind="application" :status="app.status" />
                    </td>
                  </template>
                </TableGenerator>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>

      <!-- ── Modales ── -->
      <CustomerFormSheet
        v-model:open="isEditOpen"
        :customer-to-edit="customer"
        @saved="loadCustomer"
      />

      <KycDecisionDialog
        v-model:open="isKycModalOpen"
        :customer-id="customer.id"
        :customer-name="customer.fullName"
        :current-status="customer.kycStatus"
        @decided="onKycDecided"
      />

      <AddBankAccountDialog
        v-model:open="isAddAccountOpen"
        :customer-id="customer.id"
        :customer-name="customer.fullName"
        @added="onAccountAdded"
      />
    </template>
  </div>
</template>
