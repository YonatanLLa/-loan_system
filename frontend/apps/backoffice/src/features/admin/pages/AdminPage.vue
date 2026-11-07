<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import {
  AlertCircle,
  Building2,
  CalendarDays,
  CheckCircle2,
  CreditCard,
  LoaderCircle,
  Plus,
  Save,
  ShieldCheck,
  Sliders,
  Trash2,
  Users,
  XCircle,
} from '@lucide/vue'

import type { Company, GlobalParameters } from '@prestamos/api-client'
import { PageHeader, TableGenerator } from '@prestamos/ui'
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
import { Label } from '@prestamos/ui/components/ui/label'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import { formatBusinessDate, formatInstant } from '@prestamos/utils'

import AddHolidayDialog from '../components/AddHolidayDialog.vue'
import AddUserDialog from '../components/AddUserDialog.vue'
import { useAdminSettings } from '../composables'
import type { CreateAdminUserPayload, CreateHolidayPayload } from '../services/admin.service'

// FE-BO-41 y FE-BO-42: Administración general, empresa, canales, feriados, usuarios y parámetros.

const {
  activeTab,
  // Empresa
  company,
  companyLoading,
  companySaving,
  loadCompany,
  saveCompany,
  // Feriados
  holidays,
  holidaysLoading,
  holidaysSaving,
  loadHolidays,
  addHoliday,
  removeHoliday,
  // Canales
  paymentMethods,
  paymentMethodsLoading,
  loadPaymentMethods,
  togglePaymentMethod,
  // Usuarios
  users,
  usersLoading,
  usersSaving,
  loadUsers,
  addUser,
  // Parámetros
  parameters,
  parametersLoading,
  parametersSaving,
  loadParameters,
  saveParameters,
} = useAdminSettings()

// Formularios locales reactivos
const formCompany = reactive<Partial<Company>>({
  legalName: '',
  tradeName: '',
  taxId: '',
  legalAddress: '',
  supportPhone: '',
  supportEmail: '',
  legalRepName: '',
  defaultCurrency: 'PEN',
})

const formParams = reactive<Partial<GlobalParameters>>({
  collectionAllowedHoursStart: '08:00',
  collectionAllowedHoursEnd: '20:00',
  bankAccountChangeHoldHours: 24,
  prepaymentQuoteValidityDays: 3,
  maxDtiRatioPercent: 45,
  alertOverdueDaysThreshold: 5,
})

watch(
  company,
  (c) => {
    if (c) {
      formCompany.legalName = c.legalName
      formCompany.tradeName = c.tradeName
      formCompany.taxId = c.taxId
      formCompany.legalAddress = c.legalAddress
      formCompany.supportPhone = c.supportPhone
      formCompany.supportEmail = c.supportEmail
      formCompany.legalRepName = c.legalRepName
      formCompany.defaultCurrency = c.defaultCurrency
    }
  },
  { immediate: true },
)

watch(
  parameters,
  (p) => {
    if (p) {
      formParams.collectionAllowedHoursStart = p.collectionAllowedHoursStart
      formParams.collectionAllowedHoursEnd = p.collectionAllowedHoursEnd
      formParams.bankAccountChangeHoldHours = p.bankAccountChangeHoldHours
      formParams.prepaymentQuoteValidityDays = p.prepaymentQuoteValidityDays
      formParams.maxDtiRatioPercent = p.maxDtiRatioPercent
      formParams.alertOverdueDaysThreshold = p.alertOverdueDaysThreshold
    }
  },
  { immediate: true },
)

// Modales
const isHolidayModalOpen = ref(false)
const isUserModalOpen = ref(false)

async function handleAddHoliday(payload: CreateHolidayPayload) {
  const ok = await addHoliday(payload)
  if (ok) isHolidayModalOpen.value = false
}

async function handleAddUser(payload: CreateAdminUserPayload) {
  const ok = await addUser(payload)
  if (ok) isUserModalOpen.value = false
}

onMounted(() => {
  void loadCompany()
  void loadPaymentMethods()
  void loadHolidays()
  void loadUsers()
  void loadParameters()
})
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Administración del Sistema"
      description="Configuración institucional, canales digitales, feriados nacionales, usuarios internos y parámetros globales (FE-BO-41 y FE-BO-42)."
    />

    <!-- Selector de Pestañas -->
    <div class="flex flex-wrap items-center gap-1 border-b text-sm">
      <button
        type="button"
        class="flex items-center gap-2 border-b-2 px-4 py-2.5 font-semibold transition-all"
        :class="
          activeTab === 'company'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground'
        "
        @click="activeTab = 'company'"
      >
        <Building2 class="size-4" />
        Empresa
      </button>

      <button
        type="button"
        class="flex items-center gap-2 border-b-2 px-4 py-2.5 font-semibold transition-all"
        :class="
          activeTab === 'payment-methods'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground'
        "
        @click="activeTab = 'payment-methods'"
      >
        <CreditCard class="size-4" />
        Canales Digitales
      </button>

      <button
        type="button"
        class="flex items-center gap-2 border-b-2 px-4 py-2.5 font-semibold transition-all"
        :class="
          activeTab === 'holidays'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground'
        "
        @click="activeTab = 'holidays'"
      >
        <CalendarDays class="size-4" />
        Feriados
      </button>

      <button
        type="button"
        class="flex items-center gap-2 border-b-2 px-4 py-2.5 font-semibold transition-all"
        :class="
          activeTab === 'users'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground'
        "
        @click="activeTab = 'users'"
      >
        <Users class="size-4" />
        Usuarios Backoffice
      </button>

      <button
        type="button"
        class="flex items-center gap-2 border-b-2 px-4 py-2.5 font-semibold transition-all"
        :class="
          activeTab === 'parameters'
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground'
        "
        @click="activeTab = 'parameters'"
      >
        <Sliders class="size-4" />
        Parámetros Globales (FE-BO-42)
      </button>
    </div>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- TAB 1: DATOS DE LA EMPRESA (FE-BO-41)                      -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <template v-if="activeTab === 'company'">
      <Card class="max-w-4xl">
        <CardHeader>
          <CardTitle class="text-base flex items-center gap-2">
            <Building2 class="size-4 text-primary" />
            Información Institucional y Legal
          </CardTitle>
          <CardDescription class="text-xs">
            Datos que aparecen en contratos electrónicos, pagarés, hojas resumen y correos oficiales.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div v-if="companyLoading" class="p-6 space-y-4">
            <Skeleton class="h-10 w-full" />
            <Skeleton class="h-10 w-full" />
            <Skeleton class="h-10 w-full" />
          </div>
          <form v-else class="space-y-4" @submit.prevent="saveCompany(formCompany)">
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div class="space-y-1.5">
                <Label for="companyLegalName" class="text-xs font-medium">Razón Social Legal</Label>
                <Input id="companyLegalName" v-model="formCompany.legalName" required class="h-9 text-xs" />
              </div>

              <div class="space-y-1.5">
                <Label for="companyTradeName" class="text-xs font-medium">Nombre Comercial</Label>
                <Input id="companyTradeName" v-model="formCompany.tradeName" required class="h-9 text-xs" />
              </div>

              <div class="space-y-1.5">
                <Label for="companyTaxId" class="text-xs font-medium">RUC (Registro Único de Contribuyentes)</Label>
                <Input id="companyTaxId" v-model="formCompany.taxId" maxlength="11" required class="h-9 text-xs font-mono" />
              </div>

              <div class="space-y-1.5">
                <Label for="companyLegalRep" class="text-xs font-medium">Representante Legal</Label>
                <Input id="companyLegalRep" v-model="formCompany.legalRepName" required class="h-9 text-xs" />
              </div>

              <div class="space-y-1.5 md:col-span-2">
                <Label for="companyAddress" class="text-xs font-medium">Dirección Legal / Domicilio Fiscal</Label>
                <Input id="companyAddress" v-model="formCompany.legalAddress" required class="h-9 text-xs" />
              </div>

              <div class="space-y-1.5">
                <Label for="companyPhone" class="text-xs font-medium">Teléfono Central / WhatsApp de Atención</Label>
                <Input id="companyPhone" v-model="formCompany.supportPhone" required class="h-9 text-xs" />
              </div>

              <div class="space-y-1.5">
                <Label for="companyEmail" class="text-xs font-medium">Correo Electrónico de Soporte y Notificaciones</Label>
                <Input id="companyEmail" v-model="formCompany.supportEmail" type="email" required class="h-9 text-xs" />
              </div>

              <div class="space-y-1.5">
                <Label for="companyCurrency" class="text-xs font-medium">Moneda Base de Operación</Label>
                <select
                  id="companyCurrency"
                  v-model="formCompany.defaultCurrency"
                  class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
                >
                  <option value="PEN">Soles Peruanos (PEN - S/)</option>
                  <option value="USD">Dólares Americanos (USD - $)</option>
                </select>
              </div>
            </div>

            <div class="flex justify-end pt-4 border-t">
              <Button type="submit" size="sm" :disabled="companySaving">
                <LoaderCircle v-if="companySaving" class="mr-1.5 size-3.5 animate-spin" />
                <Save v-else class="mr-1.5 size-3.5" />
                Guardar Cambios Institucionales
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </template>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- TAB 2: CANALES Y MÉTODOS DE PAGO (FE-BO-41)                -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <template v-else-if="activeTab === 'payment-methods'">
      <Card>
        <CardHeader class="pb-3">
          <CardTitle class="text-base flex items-center gap-2">
            <CreditCard class="size-4 text-primary" />
            Canales y Pasarelas de Recaudación Digital
          </CardTitle>
          <CardDescription class="text-xs">
            Habilita o deshabilita los métodos disponibles para abonos de cuotas y conciliación automática.
          </CardDescription>
        </CardHeader>
        <CardContent class="p-0">
          <div v-if="paymentMethodsLoading" class="p-6 space-y-3">
            <Skeleton class="h-10 w-full" />
            <Skeleton class="h-10 w-full" />
          </div>
          <div v-else>
            <TableGenerator
              :columns="[
                { key: 'name', label: 'Canal de Recaudación' },
                { key: 'type', label: 'Tipo de Método' },
                { key: 'webhook', label: 'Notificación Webhook' },
                { key: 'status', label: 'Estado' },
                { key: 'actions', label: 'Acción' },
              ]"
              :rows="paymentMethods"
              row-key="id"
              body-class="divide-y text-xs"
            >
              <template #row="{ row: method }">
                <td class="px-4 py-3 font-semibold text-foreground">
                  {{ method.name }}
                </td>
                <td class="px-4 py-3">
                  <Badge variant="outline" class="font-mono text-[10px]">
                    {{ method.methodType }}
                  </Badge>
                </td>
                <td class="px-4 py-3">
                  <span
                    v-if="method.webhookEnabled"
                    class="inline-flex items-center gap-1 text-emerald-600 font-medium"
                  >
                    <CheckCircle2 class="size-3.5" />
                    En tiempo real
                  </span>
                  <span v-else class="text-muted-foreground">
                    Carga manual
                  </span>
                </td>
                <td class="px-4 py-3">
                  <Badge
                    :variant="method.isActive ? 'default' : 'secondary'"
                    class="text-[10px]"
                  >
                    {{ method.isActive ? 'ACTIVO' : 'INACTIVO' }}
                  </Badge>
                </td>
                <td class="px-4 py-3">
                  <Button
                    variant="outline"
                    size="sm"
                    class="h-7 text-xs"
                    @click="togglePaymentMethod(method.id, !method.isActive)"
                  >
                    {{ method.isActive ? 'Desactivar' : 'Activar' }}
                  </Button>
                </td>
              </template>
            </TableGenerator>
          </div>
        </CardContent>
      </Card>
    </template>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- TAB 3: FERIADOS NACIONALES (FE-BO-41)                      -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <template v-else-if="activeTab === 'holidays'">
      <div class="flex justify-between items-center mb-2">
        <p class="text-xs text-muted-foreground">
          Días no laborables registrados. Las cuotas con vencimiento en feriado se postergan al siguiente día hábil.
        </p>
        <Button size="sm" class="h-8" @click="isHolidayModalOpen = true">
          <Plus class="size-3.5 mr-1" />
          Registrar Feriado
        </Button>
      </div>

      <Card>
        <CardContent class="p-0">
          <div v-if="holidaysLoading" class="p-6 space-y-3">
            <Skeleton class="h-10 w-full" />
            <Skeleton class="h-10 w-full" />
          </div>
          <div v-else-if="holidays.length === 0" class="p-8 text-center text-xs text-muted-foreground">
            No hay feriados registrados en el calendario.
          </div>
          <div v-else>
            <TableGenerator
              :columns="[
                { key: 'date', label: 'Fecha' },
                { key: 'name', label: 'Festividad' },
                { key: 'recurrence', label: 'Frecuencia' },
                { key: 'actions', label: 'Acciones' },
              ]"
              :rows="holidays"
              row-key="id"
              body-class="divide-y text-xs"
            >
              <template #row="{ row: h }">
                <td class="px-4 py-3 font-mono font-semibold">
                  {{ formatBusinessDate(h.date) }}
                </td>
                <td class="px-4 py-3 font-medium text-foreground">
                  {{ h.name }}
                </td>
                <td class="px-4 py-3">
                  <Badge variant="outline" class="text-[10px]">
                    {{ h.isRecurring ? 'Recurrente Anual' : 'Fecha Específica' }}
                  </Badge>
                </td>
                <td class="px-4 py-3">
                  <Button
                    variant="ghost"
                    size="sm"
                    class="h-7 text-xs text-destructive hover:bg-destructive/10"
                    :disabled="holidaysSaving"
                    @click="removeHoliday(h.id)"
                  >
                    <Trash2 class="size-3.5 mr-1" />
                    Eliminar
                  </Button>
                </td>
              </template>
            </TableGenerator>
          </div>
        </CardContent>
      </Card>

      <AddHolidayDialog
        v-model:open="isHolidayModalOpen"
        :is-busy="holidaysSaving"
        @submit="handleAddHoliday"
      />
    </template>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- TAB 4: USUARIOS DEL BACKOFFICE (FE-BO-41)                  -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <template v-else-if="activeTab === 'users'">
      <div class="flex justify-between items-center mb-2">
        <p class="text-xs text-muted-foreground">
          Colaboradores internos con credenciales, roles asignados y permisos de auditoría.
        </p>
        <Button size="sm" class="h-8" @click="isUserModalOpen = true">
          <Plus class="size-3.5 mr-1" />
          Nuevo Usuario
        </Button>
      </div>

      <Card>
        <CardContent class="p-0">
          <div v-if="usersLoading" class="p-6 space-y-3">
            <Skeleton class="h-10 w-full" />
            <Skeleton class="h-10 w-full" />
          </div>
          <div v-else>
            <TableGenerator
              :columns="[
                { key: 'name', label: 'Nombre Completo' },
                { key: 'email', label: 'Correo Corporativo' },
                { key: 'role', label: 'Rol Asignado' },
                { key: 'permissions', label: 'Permisos' },
                { key: 'created', label: 'Fecha de Registro' },
              ]"
              :rows="users"
              row-key="id"
              body-class="divide-y text-xs"
            >
              <template #row="{ row: u }">
                <td class="px-4 py-3 font-semibold text-foreground">
                  {{ u.fullName }}
                </td>
                <td class="px-4 py-3 font-mono text-muted-foreground">
                  {{ u.email }}
                </td>
                <td class="px-4 py-3">
                  <Badge variant="secondary" class="text-[10px] font-mono">
                    {{ u.role }}
                  </Badge>
                </td>
                <td class="px-4 py-3">
                  <span class="inline-flex items-center gap-1 text-[11px] text-muted-foreground">
                    <ShieldCheck class="size-3.5 text-primary" />
                    {{ u.permissions?.length ? `${u.permissions.length} módulos` : 'Todos (*)' }}
                  </span>
                </td>
                <td class="px-4 py-3 text-muted-foreground">
                  {{ u.createdAt ? formatInstant(u.createdAt) : 'Registrado' }}
                </td>
              </template>
            </TableGenerator>
          </div>
        </CardContent>
      </Card>

      <AddUserDialog
        v-model:open="isUserModalOpen"
        :is-busy="usersSaving"
        @submit="handleAddUser"
      />
    </template>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- TAB 5: PARÁMETROS GLOBALES (FE-BO-42)                      -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <template v-else-if="activeTab === 'parameters'">
      <Card class="max-w-4xl">
        <CardHeader>
          <CardTitle class="text-base flex items-center gap-2">
            <Sliders class="size-4 text-primary" />
            Parámetros Operacionales del Negocio (FE-BO-42)
          </CardTitle>
          <CardDescription class="text-xs">
            Políticas transversales de riesgo, horario de contacto en cobranza, periodos de enfriamiento y límites DTI.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div v-if="parametersLoading" class="p-6 space-y-4">
            <Skeleton class="h-10 w-full" />
            <Skeleton class="h-10 w-full" />
          </div>
          <form v-else class="space-y-5" @submit.prevent="saveParameters(formParams)">
            <!-- Horario de Cobranza -->
            <div class="rounded-lg border p-4 bg-muted/10 space-y-3">
              <h4 class="font-semibold text-xs flex items-center gap-1.5 text-foreground">
                <ShieldCheck class="size-4 text-primary" />
                Horario Permitido para Gestión de Cobranza (Normativa SBS / Protección al Consumidor)
              </h4>
              <p class="text-[11px] text-muted-foreground">
                El sistema bloquea llamadas, mensajes de WhatsApp y recordatorios automáticos fuera de esta ventana horaria.
              </p>
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div class="space-y-1">
                  <Label for="hoursStart" class="text-xs font-medium">Hora de inicio permitida</Label>
                  <Input
                    id="hoursStart"
                    v-model="formParams.collectionAllowedHoursStart"
                    type="time"
                    required
                    class="h-9 text-xs"
                  />
                </div>
                <div class="space-y-1">
                  <Label for="hoursEnd" class="text-xs font-medium">Hora de fin permitida</Label>
                  <Input
                    id="hoursEnd"
                    v-model="formParams.collectionAllowedHoursEnd"
                    type="time"
                    required
                    class="h-9 text-xs"
                  />
                </div>
              </div>
            </div>

            <!-- Reglas de Seguridad y Riesgo -->
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div class="space-y-1.5 rounded-lg border p-3.5 bg-muted/10">
                <Label for="bankHoldHours" class="text-xs font-semibold block">
                  Espera tras cambio de cuenta bancaria (horas)
                </Label>
                <p class="text-[11px] text-muted-foreground mb-2">
                  Periodo de retención anti-fraude antes de autorizar un desembolso a una nueva cuenta (FE-CP-18).
                </p>
                <Input
                  id="bankHoldHours"
                  v-model.number="formParams.bankAccountChangeHoldHours"
                  type="number"
                  min="0"
                  max="168"
                  required
                  class="h-9 text-xs font-mono"
                />
              </div>

              <div class="space-y-1.5 rounded-lg border p-3.5 bg-muted/10">
                <Label for="quoteValidity" class="text-xs font-semibold block">
                  Vigencia de cotización de prepago (días)
                </Label>
                <p class="text-[11px] text-muted-foreground mb-2">
                  Días calendario en los que el monto liquidado para cancelación anticipada permanece inmutable.
                </p>
                <Input
                  id="quoteValidity"
                  v-model.number="formParams.prepaymentQuoteValidityDays"
                  type="number"
                  min="1"
                  max="30"
                  required
                  class="h-9 text-xs font-mono"
                />
              </div>

              <div class="space-y-1.5 rounded-lg border p-3.5 bg-muted/10">
                <Label for="maxDti" class="text-xs font-semibold block">
                  Umbral DTI Máximo Global (%)
                </Label>
                <p class="text-[11px] text-muted-foreground mb-2">
                  Relación cuota/ingreso máxima admitida antes de recomendar rechazo automático en admisiones.
                </p>
                <Input
                  id="maxDti"
                  v-model.number="formParams.maxDtiRatioPercent"
                  type="number"
                  min="1"
                  max="100"
                  required
                  class="h-9 text-xs font-mono"
                />
              </div>

              <div class="space-y-1.5 rounded-lg border p-3.5 bg-muted/10">
                <Label for="overdueThreshold" class="text-xs font-semibold block">
                  Umbral para alertas de morosidad (días)
                </Label>
                <p class="text-[11px] text-muted-foreground mb-2">
                  Días de atraso a partir de los cuales se activan alertas críticas en el dashboard ejecutivo.
                </p>
                <Input
                  id="overdueThreshold"
                  v-model.number="formParams.alertOverdueDaysThreshold"
                  type="number"
                  min="1"
                  max="60"
                  required
                  class="h-9 text-xs font-mono"
                />
              </div>
            </div>

            <div class="flex justify-end pt-4 border-t">
              <Button type="submit" size="sm" :disabled="parametersSaving">
                <LoaderCircle v-if="parametersSaving" class="mr-1.5 size-3.5 animate-spin" />
                <Save v-else class="mr-1.5 size-3.5" />
                Guardar Parámetros Globales
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </template>
  </div>
</template>
