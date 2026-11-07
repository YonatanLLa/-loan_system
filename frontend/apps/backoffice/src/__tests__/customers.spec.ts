import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'

import CustomerFormSheet from '../features/customers/components/CustomerFormSheet.vue'

describe('CustomerFormSheet (FE-BO-06 / FE-BO-07)', () => {
  it('valida el formato de documento DNI para exigir exactamente 8 dígitos', async () => {
    const wrapper = mount(CustomerFormSheet, {
      props: {
        open: true,
      },
      global: {
        stubs: {
          Sheet: { template: '<div><slot /></div>' },
          SheetContent: { template: '<div><slot /></div>' },
          SheetHeader: { template: '<div><slot /></div>' },
          SheetTitle: { template: '<div><slot /></div>' },
          SheetDescription: { template: '<div><slot /></div>' },
          Alert: { template: '<div><slot /></div>' },
          AlertTitle: { template: '<div><slot /></div>' },
          AlertDescription: { template: '<div><slot /></div>' },
        },
      },
    })

    // Colocar un DNI inválido de 5 dígitos
    const docInput = wrapper.find('#docNumber')
    await docInput.setValue('12345')

    await wrapper.find('form').trigger('submit')

    expect(wrapper.text()).toContain('El DNI debe tener exactamente 8 dígitos numéricos')
  })

  it('valida formato de celular para exigir 9 dígitos y empezar con 9', async () => {
    const wrapper = mount(CustomerFormSheet, {
      props: {
        open: true,
      },
      global: {
        stubs: {
          Sheet: { template: '<div><slot /></div>' },
          SheetContent: { template: '<div><slot /></div>' },
          SheetHeader: { template: '<div><slot /></div>' },
          SheetTitle: { template: '<div><slot /></div>' },
          SheetDescription: { template: '<div><slot /></div>' },
          Alert: { template: '<div><slot /></div>' },
          AlertTitle: { template: '<div><slot /></div>' },
          AlertDescription: { template: '<div><slot /></div>' },
        },
      },
    })

    await wrapper.find('#docNumber').setValue('41234567')
    await wrapper.find('#fullName').setValue('Juan Perez')
    await wrapper.find('#phone').setValue('812345678') // no empieza con 9

    await wrapper.find('form').trigger('submit')

    expect(wrapper.text()).toContain('El celular debe tener 9 dígitos y empezar con 9')
  })
})
