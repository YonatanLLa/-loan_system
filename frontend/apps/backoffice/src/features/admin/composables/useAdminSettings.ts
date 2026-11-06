import { ref } from 'vue'
import { toApiError } from '@prestamos/api-client'
import type {
  AdminUserDto,
  Company,
  GlobalParameters,
  Holiday,
  PaymentMethodConfig,
} from '@prestamos/api-client'
import { toast } from 'vue-sonner'

import {
  adminService,
  type CreateAdminUserPayload,
  type CreateHolidayPayload,
} from '../services/admin.service'

export type AdminTab = 'company' | 'payment-methods' | 'holidays' | 'users' | 'parameters'

/**
 * Composable para la gestión integral de la configuración institucional,
 * canales digitales, feriados, usuarios y parámetros del backoffice (FE-BO-41 y FE-BO-42).
 */
export function useAdminSettings() {
  const activeTab = ref<AdminTab>('company')

  // ── Empresa ────────────────────────────────────────────────────────────────
  const company = ref<Company | null>(null)
  const companyLoading = ref(false)
  const companySaving = ref(false)
  const companyError = ref<string | null>(null)

  async function loadCompany(): Promise<void> {
    companyLoading.value = true
    companyError.value = null
    try {
      company.value = await adminService.fetchCompany()
    } catch (err) {
      const apiErr = toApiError(err)
      companyError.value = apiErr.detail
      toast.error('Error al cargar datos de la empresa', { description: apiErr.detail })
    } finally {
      companyLoading.value = false
    }
  }

  async function saveCompany(payload: Partial<Company>): Promise<boolean> {
    companySaving.value = true
    companyError.value = null
    try {
      company.value = await adminService.updateCompany(payload)
      toast.success('Empresa actualizada', {
        description: 'Los datos institucionales y de soporte fueron guardados.',
      })
      return true
    } catch (err) {
      const apiErr = toApiError(err)
      companyError.value = apiErr.detail
      toast.error('Error al actualizar empresa', { description: apiErr.detail })
      return false
    } finally {
      companySaving.value = false
    }
  }

  // ── Feriados ───────────────────────────────────────────────────────────────
  const holidays = ref<Holiday[]>([])
  const holidaysLoading = ref(false)
  const holidaysSaving = ref(false)
  const holidaysError = ref<string | null>(null)

  async function loadHolidays(): Promise<void> {
    holidaysLoading.value = true
    holidaysError.value = null
    try {
      holidays.value = await adminService.fetchHolidays()
    } catch (err) {
      const apiErr = toApiError(err)
      holidaysError.value = apiErr.detail
      toast.error('Error al cargar feriados', { description: apiErr.detail })
    } finally {
      holidaysLoading.value = false
    }
  }

  async function addHoliday(payload: CreateHolidayPayload): Promise<boolean> {
    if (!payload.date || !payload.name.trim()) return false
    holidaysSaving.value = true
    holidaysError.value = null
    try {
      const created = await adminService.createHoliday(payload)
      holidays.value = [...holidays.value, created].sort((a, b) => a.date.localeCompare(b.date))
      toast.success('Feriado agregado', {
        description: `${created.name} (${created.date}) añadido al calendario de vencimientos.`,
      })
      return true
    } catch (err) {
      const apiErr = toApiError(err)
      holidaysError.value = apiErr.detail
      toast.error('No se pudo agregar el feriado', { description: apiErr.detail })
      return false
    } finally {
      holidaysSaving.value = false
    }
  }

  async function removeHoliday(id: string): Promise<boolean> {
    holidaysSaving.value = true
    holidaysError.value = null
    try {
      await adminService.deleteHoliday(id)
      holidays.value = holidays.value.filter((h) => h.id !== id)
      toast.success('Feriado eliminado', {
        description: 'Se retiró el feriado del calendario del sistema.',
      })
      return true
    } catch (err) {
      const apiErr = toApiError(err)
      holidaysError.value = apiErr.detail
      toast.error('Error al eliminar feriado', { description: apiErr.detail })
      return false
    } finally {
      holidaysSaving.value = false
    }
  }

  // ── Métodos de Pago y Canales ──────────────────────────────────────────────
  const paymentMethods = ref<PaymentMethodConfig[]>([])
  const paymentMethodsLoading = ref(false)
  const paymentMethodsError = ref<string | null>(null)

  async function loadPaymentMethods(): Promise<void> {
    paymentMethodsLoading.value = true
    paymentMethodsError.value = null
    try {
      paymentMethods.value = await adminService.fetchPaymentMethods()
    } catch (err) {
      const apiErr = toApiError(err)
      paymentMethodsError.value = apiErr.detail
      toast.error('Error al cargar métodos de pago', { description: apiErr.detail })
    } finally {
      paymentMethodsLoading.value = false
    }
  }

  async function togglePaymentMethod(id: string, isActive: boolean): Promise<boolean> {
    try {
      const updated = await adminService.updatePaymentMethod(id, { isActive })
      const idx = paymentMethods.value.findIndex((m) => m.id === id)
      if (idx >= 0) {
        paymentMethods.value[idx] = updated
      }
      toast.success('Canal actualizado', {
        description: `${updated.name} ahora está ${isActive ? 'ACTIVO' : 'INACTIVO'}.`,
      })
      return true
    } catch (err) {
      const apiErr = toApiError(err)
      toast.error('Error al actualizar método de pago', { description: apiErr.detail })
      return false
    }
  }

  // ── Usuarios de Backoffice ─────────────────────────────────────────────────
  const users = ref<AdminUserDto[]>([])
  const usersLoading = ref(false)
  const usersSaving = ref(false)
  const usersError = ref<string | null>(null)

  async function loadUsers(): Promise<void> {
    usersLoading.value = true
    usersError.value = null
    try {
      users.value = await adminService.fetchUsers()
    } catch (err) {
      const apiErr = toApiError(err)
      usersError.value = apiErr.detail
      toast.error('Error al cargar usuarios', { description: apiErr.detail })
    } finally {
      usersLoading.value = false
    }
  }

  async function addUser(payload: CreateAdminUserPayload): Promise<boolean> {
    if (!payload.email.trim() || !payload.fullName.trim() || !payload.role) return false
    usersSaving.value = true
    usersError.value = null
    try {
      const created = await adminService.createUser(payload)
      users.value = [created, ...users.value]
      toast.success('Usuario registrado', {
        description: `Se creó la cuenta para ${created.fullName} (${created.email}).`,
      })
      return true
    } catch (err) {
      const apiErr = toApiError(err)
      usersError.value =
        apiErr.status === 409
          ? 'Ya existe un usuario con este correo electrónico.'
          : apiErr.detail
      toast.error('Error al registrar usuario', { description: usersError.value })
      return false
    } finally {
      usersSaving.value = false
    }
  }

  // ── Parámetros Globales ────────────────────────────────────────────────────
  const parameters = ref<GlobalParameters | null>(null)
  const parametersLoading = ref(false)
  const parametersSaving = ref(false)
  const parametersError = ref<string | null>(null)

  async function loadParameters(): Promise<void> {
    parametersLoading.value = true
    parametersError.value = null
    try {
      parameters.value = await adminService.fetchParameters()
    } catch (err) {
      const apiErr = toApiError(err)
      parametersError.value = apiErr.detail
      toast.error('Error al cargar parámetros globales', { description: apiErr.detail })
    } finally {
      parametersLoading.value = false
    }
  }

  async function saveParameters(payload: Partial<GlobalParameters>): Promise<boolean> {
    parametersSaving.value = true
    parametersError.value = null
    try {
      parameters.value = await adminService.updateParameters(payload)
      toast.success('Parámetros actualizados', {
        description: 'Las políticas y reglas operacionales globales fueron guardadas.',
      })
      return true
    } catch (err) {
      const apiErr = toApiError(err)
      parametersError.value = apiErr.detail
      toast.error('Error al guardar parámetros', { description: apiErr.detail })
      return false
    } finally {
      parametersSaving.value = false
    }
  }

  return {
    activeTab,
    // Empresa
    company,
    companyLoading,
    companySaving,
    companyError,
    loadCompany,
    saveCompany,
    // Feriados
    holidays,
    holidaysLoading,
    holidaysSaving,
    holidaysError,
    loadHolidays,
    addHoliday,
    removeHoliday,
    // Métodos de Pago
    paymentMethods,
    paymentMethodsLoading,
    paymentMethodsError,
    loadPaymentMethods,
    togglePaymentMethod,
    // Usuarios
    users,
    usersLoading,
    usersSaving,
    usersError,
    loadUsers,
    addUser,
    // Parámetros
    parameters,
    parametersLoading,
    parametersSaving,
    parametersError,
    loadParameters,
    saveParameters,
  }
}
