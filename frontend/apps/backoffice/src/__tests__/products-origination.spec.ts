import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'

import ProductFormSheet from '../features/products/components/ProductFormSheet.vue'

describe('ProductFormSheet (FE-BO-11 / FE-BO-12)', () => {
  it('valida que el monto mínimo no supere al monto máximo', async () => {
    const wrapper = mount(ProductFormSheet, {
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

    // Invertir montos
    await wrapper.find('#name').setValue('Producto Test')
    await wrapper.find('#minAmount').setValue('10000.00')
    await wrapper.find('#maxAmount').setValue('2000.00')

    await wrapper.find('form').trigger('submit')

    expect(wrapper.text()).toContain('El monto mínimo debe ser mayor a 0 y no puede superar al monto máximo')
  })

  it('valida que el plazo mínimo no supere al plazo máximo', async () => {
    const wrapper = mount(ProductFormSheet, {
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

    await wrapper.find('#name').setValue('Producto Test')
    await wrapper.find('#minTerm').setValue('24')
    await wrapper.find('#maxTerm').setValue('12')

    await wrapper.find('form').trigger('submit')

    expect(wrapper.text()).toContain('El plazo mínimo no puede superar al plazo máximo')
  })

  it('valida que el DTI automático no supere al DTI de revisión manual (FE-BO-12)', async () => {
    const wrapper = mount(ProductFormSheet, {
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

    await wrapper.find('#name').setValue('Producto Test')
    await wrapper.find('#maxDtiAuto').setValue('0.60')
    await wrapper.find('#maxDtiManual').setValue('0.40')

    await wrapper.find('form').trigger('submit')

    expect(wrapper.text()).toContain('El DTI automático no puede ser mayor que el DTI de revisión manual')
  })
})
