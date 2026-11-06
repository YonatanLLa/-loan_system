import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'

import CompanyAccountModal from '../features/treasury/components/CompanyAccountModal.vue'
import InternalTransferModal from '../features/treasury/components/InternalTransferModal.vue'

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

describe('InternalTransferModal (RF-24.5 y RF-24.6)', () => {
  it('exige número de operación bancaria y monto mayor a cero para registrar el fondeo entre cuentas propias', async () => {
    const wrapper = mount(InternalTransferModal, {
      props: {
        open: true,
        companyAccounts: [
          {
            id: 'bca-001',
            bankName: 'BCP',
            accountNumberMasked: '**** 4410',
            currency: 'PEN',
            isDisbursement: true,
            isCollection: true,
            isActive: true,
          },
          {
            id: 'bca-002',
            bankName: 'Interbank',
            accountNumberMasked: '**** 7782',
            currency: 'PEN',
            isDisbursement: false,
            isCollection: true,
            isActive: true,
          },
        ],
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

    await wrapper.find('#trfAmount').setValue('0')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.text()).toContain('Ingresa un monto válido mayor a cero')

    await wrapper.find('#trfAmount').setValue('5000.00')
    await wrapper.find('#trfOp').setValue('   ')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.text()).toContain('Ingresa el número de operación bancaria')
  })
})

