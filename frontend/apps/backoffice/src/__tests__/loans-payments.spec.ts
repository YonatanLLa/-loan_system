import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'

import ReversePaymentModal from '../features/payments/components/ReversePaymentModal.vue'

describe('ReversePaymentModal (FE-BO-23)', () => {
  it('exige motivo y justificación obligatoria para confirmar el reverso', async () => {
    const payment = {
      id: 'pay-001',
      loanId: 'loan-001',
      customerName: 'María Quispe',
      kind: 'NORMAL' as const,
      amount: '520.00',
      method: 'YAPE' as const,
      status: 'APPLIED' as const,
      paidAt: '2026-11-05T12:00:00Z',
      createdAt: '2026-11-05T12:00:00Z',
      allocations: [],
    }

    const wrapper = mount(ReversePaymentModal, {
      props: {
        open: true,
        payment,
      },
      global: {
        stubs: {
          Card: { template: '<div><slot /></div>' },
          CardContent: { template: '<div><slot /></div>' },
          CardHeader: { template: '<div><slot /></div>' },
          CardTitle: { template: '<div><slot /></div>' },
          CardDescription: { template: '<div><slot /></div>' },
          Alert: { template: '<div><slot /></div>' },
          AlertTitle: { template: '<div><slot /></div>' },
          AlertDescription: { template: '<div><slot /></div>' },
        },
      },
    })

    // Botón debe estar deshabilitado si no hay motivo
    const submitBtn = wrapper.find('button[type="submit"]')
    expect(submitBtn.attributes('disabled')).toBeDefined()

    // Llenar motivo
    await wrapper.find('#revReason').setValue('Abono duplicado por error de pasarela')
    expect(submitBtn.attributes('disabled')).toBeUndefined()
  })
})
