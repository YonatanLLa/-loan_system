import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'

import CompanyAccountModal from '../features/treasury/components/CompanyAccountModal.vue'

describe('CompanyAccountModal (FE-BO-27)', () => {
  it('valida que el CCI corporativo tenga exactamente 20 dígitos numéricos', async () => {
    const wrapper = mount(CompanyAccountModal, {
      props: {
        open: true,
      },
      global: {
        stubs: {
          Card: { template: '<div><slot /></div>' },
          CardContent: { template: '<div><slot /></div>' },
          CardHeader: { template: '<div><slot /></div>' },
          CardTitle: { template: '<div><slot /></div>' },
          CardDescription: { template: '<div><slot /></div>' },
          Alert: { template: '<div><slot /></div>' },
          AlertDescription: { template: '<div><slot /></div>' },
        },
      },
    })

    // Colocar CCI incompleto
    await wrapper.find('#cci').setValue('00219000123')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.text()).toContain('El Código de Cuenta Interbancario (CCI) debe contener exactamente 20 dígitos numéricos')
  })
})
