import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

import AddHolidayDialog from '../features/admin/components/AddHolidayDialog.vue'
import AddUserDialog from '../features/admin/components/AddUserDialog.vue'
import { useAdminSettings } from '../features/admin/composables'
import { adminService } from '../features/admin/services'

const stubs = {
  Card: { template: '<div><slot /></div>' },
  CardContent: { template: '<div><slot /></div>' },
  CardHeader: { template: '<div><slot /></div>' },
  CardTitle: { template: '<div><slot /></div>' },
  CardDescription: { template: '<div><slot /></div>' },
}

describe('AddHolidayDialog (FE-BO-41)', () => {
  it('deshabilita el botón de guardar si falta fecha o nombre', async () => {
    const wrapper = mount(AddHolidayDialog, {
      props: { open: true, isBusy: false },
      global: { stubs },
    })

    const submit = wrapper.find('button[type="submit"]')
    expect(submit.attributes('disabled')).toBeDefined()

    await wrapper.find('#holidayDate').setValue('2026-12-25')
    expect(submit.attributes('disabled')).toBeDefined()

    await wrapper.find('#holidayName').setValue('Navidad')
    expect(submit.attributes('disabled')).toBeUndefined()
  })

  it('emite el evento submit con los datos del feriado y recurrencia', async () => {
    const wrapper = mount(AddHolidayDialog, {
      props: { open: true, isBusy: false },
      global: { stubs },
    })

    await wrapper.find('#holidayDate').setValue('2026-07-28')
    await wrapper.find('#holidayName').setValue('Fiestas Patrias')
    await wrapper.find('#isRecurring').setValue(true)

    await wrapper.find('form').trigger('submit')

    const emitted = wrapper.emitted('submit')
    expect(emitted).toBeTruthy()
    expect(emitted?.[0]?.[0]).toEqual({
      date: '2026-07-28',
      name: 'Fiestas Patrias',
      isRecurring: true,
    })
  })
})

describe('AddUserDialog (FE-BO-41)', () => {
  it('exige nombre completo y correo corporativo para habilitar el registro', async () => {
    const wrapper = mount(AddUserDialog, {
      props: { open: true, isBusy: false },
      global: { stubs },
    })

    const submit = wrapper.find('button[type="submit"]')
    expect(submit.attributes('disabled')).toBeDefined()

    await wrapper.find('#userFullName').setValue('Carlos Mendoza')
    expect(submit.attributes('disabled')).toBeDefined()

    await wrapper.find('#userEmail').setValue('carlos.mendoza@fintech.pe')
    expect(submit.attributes('disabled')).toBeUndefined()
  })

  it('emite el payload con el rol seleccionado y permisos correspondientes', async () => {
    const wrapper = mount(AddUserDialog, {
      props: { open: true, isBusy: false },
      global: { stubs },
    })

    await wrapper.find('#userFullName').setValue('Ana Morales')
    await wrapper.find('#userEmail').setValue('ana.morales@fintech.pe')
    await wrapper.find('#userRole').setValue('ROLE_COLLECTOR')

    await wrapper.find('form').trigger('submit')

    const emitted = wrapper.emitted('submit')
    expect(emitted).toBeTruthy()
    const payload = emitted?.[0]?.[0] as { fullName: string; email: string; role: string; permissions: string[] }
    expect(payload.fullName).toBe('Ana Morales')
    expect(payload.email).toBe('ana.morales@fintech.pe')
    expect(payload.role).toBe('ROLE_COLLECTOR')
    expect(payload.permissions).toContain('collections.actions')
  })
})

describe('Admin Service y Composable (FE-BO-41 y FE-BO-42)', () => {
  it('adminService expone todas las operaciones de administración y parámetros', () => {
    expect(typeof adminService.fetchCompany).toBe('function')
    expect(typeof adminService.updateCompany).toBe('function')
    expect(typeof adminService.fetchHolidays).toBe('function')
    expect(typeof adminService.createHoliday).toBe('function')
    expect(typeof adminService.deleteHoliday).toBe('function')
    expect(typeof adminService.fetchPaymentMethods).toBe('function')
    expect(typeof adminService.updatePaymentMethod).toBe('function')
    expect(typeof adminService.fetchUsers).toBe('function')
    expect(typeof adminService.createUser).toBe('function')
    expect(typeof adminService.fetchParameters).toBe('function')
    expect(typeof adminService.updateParameters).toBe('function')
  })

  it('useAdminSettings inicializa pestañas y estados correctamente', () => {
    setActivePinia(createPinia())
    const settings = useAdminSettings()

    expect(settings.activeTab.value).toBe('company')
    settings.activeTab.value = 'parameters'
    expect(settings.activeTab.value).toBe('parameters')

    expect(settings.company.value).toBeNull()
    expect(settings.holidays.value).toEqual([])
    expect(settings.paymentMethods.value).toEqual([])
    expect(settings.users.value).toEqual([])
    expect(settings.parameters.value).toBeNull()
  })
})
