import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

import WriteOffDialog from '../features/collections/components/WriteOffDialog.vue'
import CollectionActionDialog from '../features/collections/components/CollectionActionDialog.vue'
import AssignCollectorDialog from '../features/collections/components/AssignCollectorDialog.vue'
import { useAging, useReceivables } from '../features/collections/composables'
import { collectionsService } from '../features/collections/services'

const stubs = {
  Card: { template: '<div><slot /></div>' },
  CardContent: { template: '<div><slot /></div>' },
  CardHeader: { template: '<div><slot /></div>' },
  CardTitle: { template: '<div><slot /></div>' },
  CardDescription: { template: '<div><slot /></div>' },
  Alert: { template: '<div><slot /></div>' },
  AlertDescription: { template: '<div><slot /></div>' },
}

const row = {
  loanId: 'loan-007',
  customerId: 'cus-007',
  customerName: 'Rosa Chávez Díaz',
  phone: '987000007',
  productName: 'Crédito Personal',
  bucket: 'TRAMO_4' as const,
  daysLate: 210,
  overdueInstallments: 7,
  overduePrincipal: '2800.00',
  totalDue: '3100.00',
}

describe('WriteOffDialog (FE-BO-35)', () => {
  it('exige justificación, acta y un segundo aprobador', async () => {
    setActivePinia(createPinia())
    const wrapper = mount(WriteOffDialog, {
      props: {
        open: true,
        row,
        approvers: [
          { id: 'usr-001', fullName: 'Administrador del Sistema', email: 'admin@sistema.local', role: 'ROLE_ADMIN' },
          { id: 'usr-004', fullName: 'Carla Núñez (Cobranza)', email: 'cobranza@sistema.local', role: 'ROLE_COLLECTOR' },
        ],
      },
      global: { stubs },
    })

    const submit = wrapper.find('button[type="submit"]')
    expect(submit.attributes('disabled')).toBeDefined()

    await wrapper.find('#writeOffReason').setValue('Cartera irrecuperable tras 210 días sin contacto')
    await wrapper.find('#minuteRef').setValue('ACTA-2026-014')
    await wrapper.find('#approvedBy').setValue('Carla Núñez (Cobranza)')

    expect(submit.attributes('disabled')).toBeUndefined()
  })
})

describe('CollectionActionDialog (FE-BO-33)', () => {
  it('pide monto y fecha cuando la gestión es una promesa de pago', async () => {
    const wrapper = mount(CollectionActionDialog, {
      props: { open: true, row },
      global: { stubs },
    })

    await wrapper.find('#actionNote').setValue('El cliente atendió la llamada')
    expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeUndefined()

    await wrapper.find('#actionType').setValue('PROMESA_DE_PAGO')
    expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeDefined()

    await wrapper.find('#promiseAmount').setValue('500.00')
    await wrapper.find('#promiseDate').setValue('2026-11-12')
    expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeUndefined()
  })

  it('permite rellenar notas rápidamente usando plantillas predefinidas', async () => {
    const wrapper = mount(CollectionActionDialog, {
      props: { open: true, row },
      global: { stubs },
    })

    const buttons = wrapper.findAll('button[type="button"]')
    const templateBtn = buttons.find((b) => b.text().includes('Sin respuesta'))
    expect(templateBtn).toBeDefined()
    await templateBtn?.trigger('click')

    const textarea = wrapper.find('#actionNote').element as HTMLTextAreaElement
    expect(textarea.value).toContain('Cliente no contesta')
  })
})

describe('AssignCollectorDialog (FE-BO-34)', () => {
  it('exige seleccionar un gestor para habilitar la asignación', async () => {
    const wrapper = mount(AssignCollectorDialog, {
      props: {
        open: true,
        row,
        collectors: [
          { id: 'usr-004', fullName: 'Carla Núñez (Cobranza)', email: 'cobranza@sistema.local', role: 'ROLE_COLLECTOR' },
          { id: 'usr-005', fullName: 'Marcos Rivas', email: 'marcos@sistema.local', role: 'ROLE_COLLECTOR' },
        ],
      },
      global: { stubs },
    })

    const submit = wrapper.find('button[type="submit"]')
    expect(submit.attributes('disabled')).toBeDefined()

    await wrapper.find('#collector').setValue('Carla Núñez (Cobranza)')
    expect(submit.attributes('disabled')).toBeUndefined()
  })
})

describe('Collections Composables y Service (FE-BO-32 a FE-BO-36)', () => {
  it('collectionsService expone todos los métodos del dominio de cobranzas', () => {
    expect(typeof collectionsService.fetchAging).toBe('function')
    expect(typeof collectionsService.fetchAgingBucketCounts).toBe('function')
    expect(typeof collectionsService.fetchCollectors).toBe('function')
    expect(typeof collectionsService.fetchActiveProducts).toBe('function')
    expect(typeof collectionsService.assignCollector).toBe('function')
    expect(typeof collectionsService.fetchReceivables).toBe('function')
    expect(typeof collectionsService.fetchCollectionActions).toBe('function')
    expect(typeof collectionsService.createCollectionAction).toBe('function')
    expect(typeof collectionsService.writeOffLoan).toBe('function')
  })

  it('useAging inicializa filtros y calcula totalPages correctamente', () => {
    const { bucket, totalPages, pageData, chooseBucket } = useAging(10)
    expect(bucket.value).toBe('')
    expect(totalPages.value).toBe(1)

    chooseBucket('TRAMO_2')
    expect(bucket.value).toBe('TRAMO_2')

    pageData.value.total = 35
    expect(totalPages.value).toBe(4)
  })

  it('useReceivables inicializa con valores por defecto y calcula páginas', () => {
    const { type, query, totalPages, pageData } = useReceivables(5)
    expect(type.value).toBe('')
    expect(query.value).toBe('')
    expect(totalPages.value).toBe(1)

    pageData.value.total = 21
    expect(totalPages.value).toBe(5)
  })
})
