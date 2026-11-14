<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { LoaderCircle, UserPlus, UserCheck, AlertTriangle, ShieldCheck } from '@lucide/vue'
import { toast } from 'vue-sonner'

import type { Customer, CreateCustomerRequest } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { Button } from '@prestamos/ui/components/ui/button'
import { Input } from '@prestamos/ui/components/ui/input'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@prestamos/ui/components/ui/sheet'
import { Alert, AlertDescription, AlertTitle } from '@prestamos/ui/components/ui/alert'

import { useCustomers } from '../composables/useCustomers'

// FE-BO-06 y FE-BO-07: Alta y edición de clientes con validación por tipo de documento y manejo de 409 duplicado.

const props = defineProps<{
  open: boolean
  customerToEdit?: Customer | null
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'saved', customer: Customer): void
}>()

const { saveCustomer } = useCustomers()
const isEditing = computed(() => !!props.customerToEdit)
const isSubmitting = ref(false)
const duplicateDocError = ref<string | null>(null)
const fieldErrors = ref<Record<string, string>>({})

const acceptDataProcessing = ref(true)
const acceptCreditBureau = ref(true)
const acceptMarketing = ref(false)

const form = ref<CreateCustomerRequest>({
  personType: 'NATURAL',
  docType: 'DNI',
  docNumber: '',
  fullName: '',
  email: '',
  phone: '',
  address: '',
  district: '',
  province: 'Lima',
  department: 'Lima',
  ubigeo: '150101',
  birthDate: '1995-01-01',
  monthlyNetIncome: '2500.00',
  occupation: '',
  employerName: '',
  workSeniorityMonths: 12,
  isPep: false,
})

watch(
  () => props.customerToEdit,
  (c) => {
    duplicateDocError.value = null
    fieldErrors.value = {}
    if (c) {
      form.value = {
        personType: c.personType,
        docType: c.docType,
        docNumber: c.docNumber,
        fullName: c.fullName,
        email: c.email,
        phone: c.phone ?? c.phonePrimary ?? '',
        address: c.address ?? '',
        district: c.district ?? '',
        province: c.province ?? 'Lima',
        department: c.department ?? 'Lima',
        ubigeo: c.ubigeo ?? '150101',
        birthDate: c.birthDate ?? '1995-01-01',
        monthlyNetIncome: String(c.monthlyNetIncome ?? c.monthlyIncome ?? '2500.00').replace(
          /[^\d.]/g,
          '',
        ),
        occupation: c.occupation ?? '',
        employerName: c.employerName ?? '',
        workSeniorityMonths: c.workSeniorityMonths ?? 12,
        isPep: c.isPep ?? false,
      }
    } else {
      form.value = {
        personType: 'NATURAL',
        docType: 'DNI',
        docNumber: '',
        fullName: '',
        email: '',
        phone: '',
        address: '',
        district: '',
        province: 'Lima',
        department: 'Lima',
        ubigeo: '150101',
        birthDate: '1995-01-01',
        monthlyNetIncome: '2500.00',
        occupation: '',
        employerName: '',
        workSeniorityMonths: 12,
        isPep: false,
      }
      acceptDataProcessing.value = true
      acceptCreditBureau.value = true
      acceptMarketing.value = false
    }
  },
  { immediate: true },
)

function validateClient(): boolean {
  fieldErrors.value = {}
  duplicateDocError.value = null
  let valid = true

  // Validaciones según el tipo de documento (catálogo regex)
  if (form.value.docType === 'DNI') {
    if (!/^\d{8}$/.test(form.value.docNumber)) {
      fieldErrors.value.docNumber = 'El DNI debe tener exactamente 8 dígitos numéricos.'
      valid = false
    }
  } else if (form.value.docType === 'CE') {
    if (!/^\d{9}$/.test(form.value.docNumber)) {
      fieldErrors.value.docNumber = 'El Carné de Extranjería debe tener 9 dígitos numéricos.'
      valid = false
    }
  } else if (form.value.docType === 'RUC') {
    if (!/^\d{11}$/.test(form.value.docNumber)) {
      fieldErrors.value.docNumber = 'El RUC debe tener 11 dígitos numéricos.'
      valid = false
    }
  } else if (form.value.docType === 'PASAPORTE') {
    if (!/^[A-Z0-9]{6,12}$/i.test(form.value.docNumber)) {
      fieldErrors.value.docNumber = 'El pasaporte debe tener entre 6 y 12 caracteres alfanuméricos.'
      valid = false
    }
  }

  if (!form.value.fullName?.trim()) {
    fieldErrors.value.fullName = 'El nombre completo o razón social es obligatorio.'
    valid = false
  }

  if (!form.value.email.trim() || !form.value.email.includes('@')) {
    fieldErrors.value.email = 'Ingresa un correo electrónico válido.'
    valid = false
  }

  if (!/^9\d{8}$/.test(form.value.phone ?? '')) {
    fieldErrors.value.phone = 'El celular debe tener 9 dígitos y empezar con 9 (formato Perú).'
    valid = false
  }

  if (form.value.personType === 'NATURAL' && !form.value.birthDate) {
    fieldErrors.value.birthDate = 'La fecha de nacimiento es requerida.'
    valid = false
  }

  const income = Number(form.value.monthlyNetIncome)
  if (Number.isNaN(income) || income <= 0) {
    fieldErrors.value.monthlyNetIncome = 'El ingreso neto mensual debe ser mayor a 0.'
    valid = false
  }

  if (!isEditing.value && !acceptDataProcessing.value) {
    fieldErrors.value.consents =
      'El consentimiento LPDP (Ley N° 29733) es obligatorio para registrar al cliente.'
    valid = false
  }

  return valid
}

async function onSubmit() {
  if (!validateClient()) return

  isSubmitting.value = true
  duplicateDocError.value = null

  try {
    const consents = [
      { purpose: 'DATA_PROCESSING' as const, version: 'v1.0-2026' },
      ...(acceptCreditBureau.value
        ? [{ purpose: 'CREDIT_BUREAU_QUERY' as const, version: 'v1.0-2026' }]
        : []),
      ...(acceptMarketing.value
        ? [{ purpose: 'MARKETING' as const, version: 'v1.0-2026' }]
        : []),
    ]

    const payload: CreateCustomerRequest = {
      ...form.value,
      consents,
    }

    if (isEditing.value && props.customerToEdit) {
      const data = await saveCustomer(
        payload,
        props.customerToEdit.id,
        props.customerToEdit.version ?? 1,
      )
      toast.success('Cliente actualizado', {
        description: `Los datos de ${data.fullName} se guardaron exitosamente.`,
      })
      emit('saved', data)
      emit('update:open', false)
    } else {
      const data = await saveCustomer(payload)
      toast.success('Cliente registrado', {
        description: `${data.fullName} ha sido dado de alta exitosamente.`,
      })
      emit('saved', data)
      emit('update:open', false)
    }
  } catch (error) {
    const apiError = toApiError(error)

    // FE-BO-07: Mensaje claro ante documento duplicado (409)
    if (apiError.status === 409) {
      duplicateDocError.value =
        apiError.detail || 'Ya existe un cliente registrado con este documento.'
      toast.error('Documento duplicado', {
        description: duplicateDocError.value,
      })
      return
    }

    if (apiError.fieldErrors) {
      for (const [k, v] of Object.entries(apiError.fieldErrors)) {
        if (v && v[0]) {
          fieldErrors.value[k.toLowerCase()] = v[0]
        }
      }
    }
    toast.error(apiError.title, { description: apiError.detail })
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <Sheet :open="open" @update:open="(val) => emit('update:open', val)">
    <SheetContent side="right" class="w-full sm:max-w-xl overflow-y-auto">
      <SheetHeader class="border-b pb-4">
        <div class="flex items-center gap-2 text-primary">
          <UserPlus v-if="!isEditing" class="size-5" />
          <UserCheck v-else class="size-5" />
          <SheetTitle>{{ isEditing ? 'Editar Cliente' : 'Alta de Nuevo Cliente' }}</SheetTitle>
        </div>
        <SheetDescription>
          {{
            isEditing
              ? 'Modifica los datos de contacto, dirección y perfil económico del cliente. El documento de identidad es inmutable (Decisión D3).'
              : 'Registra un nuevo solicitante con validación de documento y consentimientos LPDP (FE-BO-06).'
          }}
        </SheetDescription>
      </SheetHeader>

      <form class="space-y-5 pt-4" @submit.prevent="onSubmit">
        <!-- Alerta de documento duplicado (409 - FE-BO-07) -->
        <Alert v-if="duplicateDocError" variant="destructive">
          <AlertTriangle class="size-4" />
          <AlertTitle>Documento Duplicado (Conflicto 409)</AlertTitle>
          <AlertDescription>
            {{ duplicateDocError }}
          </AlertDescription>
        </Alert>

        <div class="space-y-4">
          <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            1. Identificación
          </h4>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="personType" class="text-xs font-medium">Tipo de Persona</label>
              <select
                id="personType"
                v-model="form.personType"
                :disabled="isEditing"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring disabled:opacity-50"
              >
                <option value="NATURAL">Persona Natural</option>
                <option value="JURIDICA">Persona Jurídica</option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label for="docType" class="text-xs font-medium">Tipo de Documento</label>
              <select
                id="docType"
                v-model="form.docType"
                :disabled="isEditing"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring disabled:opacity-50"
              >
                <option value="DNI">DNI (8 dígitos)</option>
                <option value="CE">Carné de Extranjería (9 dígitos)</option>
                <option value="RUC">RUC (11 dígitos)</option>
                <option value="PASAPORTE">Pasaporte</option>
              </select>
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="docNumber" class="text-xs font-medium">Número de Documento</label>
              <Input
                id="docNumber"
                v-model="form.docNumber"
                :disabled="isEditing"
                placeholder="Ej. 45891230"
                class="font-mono"
                :class="{ 'border-destructive': fieldErrors.docNumber }"
              />
              <p v-if="fieldErrors.docNumber" class="text-xs text-destructive">
                {{ fieldErrors.docNumber }}
              </p>
            </div>

            <div class="space-y-1.5">
              <label for="birthDate" class="text-xs font-medium">
                {{ form.personType === 'JURIDICA' ? 'Fecha de Constitución' : 'Fecha de Nacimiento' }}
              </label>
              <Input
                id="birthDate"
                v-model="form.birthDate"
                type="date"
                :disabled="isEditing"
                :class="{ 'border-destructive': fieldErrors.birthDate }"
              />
              <p v-if="fieldErrors.birthDate" class="text-xs text-destructive">
                {{ fieldErrors.birthDate }}
              </p>
            </div>
          </div>

          <div class="space-y-1.5">
            <label for="fullName" class="text-xs font-medium">
              {{ form.personType === 'JURIDICA' ? 'Razón Social' : 'Nombres y Apellidos Completos' }}
            </label>
            <Input
              id="fullName"
              v-model="form.fullName"
              :disabled="isEditing"
              placeholder="Ej. María Elena Quispe Huamán"
              :class="{ 'border-destructive': fieldErrors.fullName }"
            />
            <p v-if="fieldErrors.fullName" class="text-xs text-destructive">
              {{ fieldErrors.fullName }}
            </p>
          </div>
        </div>

        <div class="space-y-4 border-t pt-4">
          <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            2. Contacto y Ubicación
          </h4>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="email" class="text-xs font-medium">Correo Electrónico</label>
              <Input
                id="email"
                v-model="form.email"
                type="email"
                placeholder="cliente@correo.pe"
                :class="{ 'border-destructive': fieldErrors.email }"
              />
              <p v-if="fieldErrors.email" class="text-xs text-destructive">
                {{ fieldErrors.email }}
              </p>
            </div>

            <div class="space-y-1.5">
              <label for="phone" class="text-xs font-medium">Celular (Perú)</label>
              <Input
                id="phone"
                v-model="form.phone"
                placeholder="987654321"
                maxlength="9"
                class="font-mono"
                :class="{ 'border-destructive': fieldErrors.phone }"
              />
              <p v-if="fieldErrors.phone" class="text-xs text-destructive">
                {{ fieldErrors.phone }}
              </p>
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div class="sm:col-span-2 space-y-1.5">
              <label for="address" class="text-xs font-medium">Dirección Domiciliaria</label>
              <Input
                id="address"
                v-model="form.address"
                placeholder="Av. Los Próceres 450, Urb. Santa Catalina"
              />
            </div>

            <div class="space-y-1.5">
              <label for="district" class="text-xs font-medium">Distrito</label>
              <Input id="district" v-model="form.district" placeholder="La Victoria" />
            </div>
          </div>
        </div>

        <div class="space-y-4 border-t pt-4">
          <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            3. Perfil Económico / Laboral
          </h4>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="monthlyNetIncome" class="text-xs font-medium">
                Ingreso Neto Mensual (S/)
              </label>
              <Input
                id="monthlyNetIncome"
                v-model="form.monthlyNetIncome"
                type="number"
                step="0.01"
                min="0"
                class="font-mono"
                :class="{ 'border-destructive': fieldErrors.monthlyNetIncome }"
              />
              <p v-if="fieldErrors.monthlyNetIncome" class="text-xs text-destructive">
                {{ fieldErrors.monthlyNetIncome }}
              </p>
            </div>

            <div class="space-y-1.5">
              <label for="workSeniorityMonths" class="text-xs font-medium">
                Antigüedad Laboral (meses)
              </label>
              <Input
                id="workSeniorityMonths"
                v-model.number="form.workSeniorityMonths"
                type="number"
                min="0"
              />
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label for="occupation" class="text-xs font-medium">Ocupación / Cargo</label>
              <Input id="occupation" v-model="form.occupation" placeholder="Ej. Contador Público" />
            </div>

            <div class="space-y-1.5">
              <label for="employerName" class="text-xs font-medium">Centro Laboral / Empresa</label>
              <Input
                id="employerName"
                v-model="form.employerName"
                placeholder="Ej. Corporación Peruana SAC"
              />
            </div>
          </div>

          <label class="flex items-center gap-2 text-xs cursor-pointer pt-1">
            <input
              v-model="form.isPep"
              type="checkbox"
              class="rounded border-input"
            />
            <span>
              <strong>Persona Expuesta Políticamente (PEP):</strong> Declaración jurada SBS / UIF-Perú.
            </span>
          </label>
        </div>

        <!-- Consentimientos LPDP en Alta -->
        <div v-if="!isEditing" class="rounded-lg border p-3.5 space-y-2 bg-primary/5">
          <div class="flex items-center gap-2 text-xs font-semibold text-primary">
            <ShieldCheck class="size-4" />
            <span>Consentimientos Ley N° 29733 (LPDP)</span>
          </div>
          <label class="flex items-start gap-2 text-xs cursor-pointer">
            <input
              v-model="acceptDataProcessing"
              type="checkbox"
              class="mt-0.5 rounded border-input"
            />
            <span>
              <strong>Tratamiento de Datos Personales (Obligatorio):</strong> Autorizo el tratamiento
              para evaluación crediticia, prevención LA/FT y ejecución contractual.
            </span>
          </label>
          <label class="flex items-start gap-2 text-xs cursor-pointer">
            <input
              v-model="acceptCreditBureau"
              type="checkbox"
              class="mt-0.5 rounded border-input"
            />
            <span>
              <strong>Consulta en Centrales de Riesgo (SBS / Equifax / Sentinel):</strong> Autorizo
              la consulta de mi historial crediticio.
            </span>
          </label>
          <label class="flex items-start gap-2 text-xs cursor-pointer">
            <input
              v-model="acceptMarketing"
              type="checkbox"
              class="mt-0.5 rounded border-input"
            />
            <span>
              <strong>Comunicaciones Comerciales (Opcional):</strong> Deseo recibir ofertas y
              campañas promocionales.
            </span>
          </label>
          <p v-if="fieldErrors.consents" class="text-xs text-destructive">
            {{ fieldErrors.consents }}
          </p>
        </div>

        <div class="flex items-center justify-end gap-2 border-t pt-4">
          <Button
            type="button"
            variant="ghost"
            :disabled="isSubmitting"
            @click="emit('update:open', false)"
          >
            Cancelar
          </Button>
          <Button type="submit" :disabled="isSubmitting">
            <LoaderCircle v-if="isSubmitting" class="size-4 mr-2 animate-spin" />
            {{ isEditing ? 'Guardar Cambios' : 'Registrar Cliente' }}
          </Button>
        </div>
      </form>
    </SheetContent>
  </Sheet>
</template>
