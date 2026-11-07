import type {
  AdminUserDto,
  Company,
  GlobalParameters,
  Holiday,
  PaymentMethodConfig,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

export interface CreateHolidayPayload {
  date: string
  name: string
  isRecurring?: boolean
}

export interface CreateAdminUserPayload {
  email: string
  fullName: string
  role: string
  password?: string
  permissions?: string[]
}

/**
 * Servicio de Administración (FE-BO-41 y FE-BO-42).
 * Gestiona la configuración institucional, canales de pago, feriados,
 * usuarios del backoffice y parámetros operacionales globales.
 */
export const adminService = {
  /** Obtiene la información institucional y legal de la empresa (FE-BO-41). */
  async fetchCompany(): Promise<Company> {
    const { data } = await useApi().get<Company>('/admin/company')
    return data
  },

  /** Actualiza los datos corporativos de la empresa (FE-BO-41). */
  async updateCompany(payload: Partial<Company>): Promise<Company> {
    const { data } = await useApi().put<Company>('/admin/company', payload)
    return data
  },

  /** Obtiene el calendario de feriados no laborables (FE-BO-41). */
  async fetchHolidays(): Promise<Holiday[]> {
    const { data } = await useApi().get<Holiday[]>('/admin/holidays')
    return data
  },

  /** Registra un nuevo feriado en el calendario (FE-BO-41). */
  async createHoliday(payload: CreateHolidayPayload): Promise<Holiday> {
    const { data } = await useApi().post<Holiday>('/admin/holidays', payload)
    return data
  },

  /** Elimina un feriado registrado por su identificador (FE-BO-41). */
  async deleteHoliday(id: string): Promise<void> {
    await useApi().delete(`/admin/holidays/${encodeURIComponent(id)}`)
  },

  /** Obtiene la lista de canales y pasarelas de pago digitales (FE-BO-41). */
  async fetchPaymentMethods(): Promise<PaymentMethodConfig[]> {
    const { data } = await useApi().get<PaymentMethodConfig[]>('/admin/payment-methods')
    return data
  },

  /** Actualiza el estado o configuración de un método de pago (FE-BO-41). */
  async updatePaymentMethod(
    id: string,
    payload: Partial<PaymentMethodConfig>,
  ): Promise<PaymentMethodConfig> {
    const { data } = await useApi().put<PaymentMethodConfig>(
      `/admin/payment-methods/${encodeURIComponent(id)}`,
      payload,
    )
    return data
  },

  /** Obtiene los usuarios internos del backoffice con sus roles y permisos (FE-BO-41). */
  async fetchUsers(): Promise<AdminUserDto[]> {
    const { data } = await useApi().get<AdminUserDto[]>('/admin/users')
    return data
  },

  /** Registra/invita a un nuevo usuario interno al backoffice (FE-BO-41). */
  async createUser(payload: CreateAdminUserPayload): Promise<AdminUserDto> {
    const { data } = await useApi().post<AdminUserDto>('/admin/users', payload)
    return data
  },

  /** Obtiene los parámetros globales del negocio y reglas de control (FE-BO-42). */
  async fetchParameters(): Promise<GlobalParameters> {
    const { data } = await useApi().get<GlobalParameters>('/admin/parameters')
    return data
  },

  /** Actualiza los parámetros globales del negocio (FE-BO-42). */
  async updateParameters(payload: Partial<GlobalParameters>): Promise<GlobalParameters> {
    const { data } = await useApi().put<GlobalParameters>('/admin/parameters', payload)
    return data
  },
}
