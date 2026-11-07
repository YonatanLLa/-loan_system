import { describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { Payment } from '@prestamos/api-client'

import { paymentsService } from '../features/payments/services'
import {
  usePaymentGateway,
  usePaymentHistory,
  usePrepayment,
} from '../features/payments/composables'

describe('Payments Service (FE-CP-12 a FE-CP-17)', () => {
  it('expone todas las operaciones de pasarela, prepago total, amortización parcial e historial', () => {
    expect(typeof paymentsService.createPaymentIntent).toBe('function')
    expect(typeof paymentsService.getPayment).toBe('function')
    expect(typeof paymentsService.getPaymentHistory).toBe('function')
    expect(typeof paymentsService.downloadReceiptPdf).toBe('function')
    expect(typeof paymentsService.quotePrepayment).toBe('function')
    expect(typeof paymentsService.simulatePartialPrepayment).toBe('function')
  })
})

describe('usePaymentGateway Composable (FE-CP-12 a FE-CP-14)', () => {
  it('inicializa correctamente los estados del gateway y detecta tipos de medios', () => {
    setActivePinia(createPinia())
    const gateway = usePaymentGateway({ loanId: 'loan-001', defaultAmount: '250.00' })

    expect(gateway.loanId.value).toBe('loan-001')
    expect(gateway.amount.value).toBe('250.00')
    expect(gateway.step.value).toBe('SELECT_METHOD')
    expect(gateway.selectedMethod.value).toBe('YAPE')
    expect(gateway.isQrMethod.value).toBe(true)
    expect(gateway.isCardMethod.value).toBe(false)
    expect(gateway.isVoucherMethod.value).toBe(false)

    // Cambia a tarjeta
    gateway.selectedMethod.value = 'TARJETA_DEBITO'
    expect(gateway.isQrMethod.value).toBe(false)
    expect(gateway.isCardMethod.value).toBe(true)

    // Cambia a transferencia con voucher
    gateway.selectedMethod.value = 'TRANSFERENCIA_BANCARIA'
    expect(gateway.isVoucherMethod.value).toBe(true)
  })

  it('valida datos obligatorios antes de iniciar intención de pago', async () => {
    setActivePinia(createPinia())
    const gateway = usePaymentGateway({ loanId: '' })

    const resNoLoan = await gateway.initiatePayment()
    expect(resNoLoan).toBeNull()
    expect(gateway.errorMessage.value).toContain('selecciona un préstamo')

    gateway.loanId.value = 'loan-001'
    gateway.amount.value = '0'
    const resZero = await gateway.initiatePayment()
    expect(resZero).toBeNull()
    expect(gateway.errorMessage.value).toContain('monto válido')

    gateway.amount.value = '150.00'
    gateway.selectedMethod.value = 'TRANSFERENCIA_BANCARIA'
    const resNoVoucher = await gateway.initiatePayment()
    expect(resNoVoucher).toBeNull()
    expect(gateway.errorMessage.value).toContain('comprobante o voucher')
  })

  it('permite seleccionar voucher y reiniciar el flujo de pago con reset()', () => {
    setActivePinia(createPinia())
    const gateway = usePaymentGateway({ loanId: 'loan-001' })

    const file = new File(['dummy-content'], 'comprobante-bcp.png', { type: 'image/png' })
    gateway.handleVoucherSelected(file)
    expect(gateway.voucherFileName.value).toBe('comprobante-bcp.png')
    expect(gateway.voucherFile.value).toBe(file)

    gateway.reset()
    expect(gateway.step.value).toBe('SELECT_METHOD')
    expect(gateway.voucherFileName.value).toBe('')
    expect(gateway.voucherFile.value).toBeNull()
    expect(gateway.activeIntent.value).toBeNull()
  })
})

describe('usePrepayment Composable (FE-CP-16 y FE-CP-17)', () => {
  it('inicializa estados limpios para cotización y simulación', () => {
    setActivePinia(createPinia())
    const prepay = usePrepayment()

    expect(prepay.quote.value).toBeNull()
    expect(prepay.isQuoting.value).toBe(false)
    expect(prepay.partialSimulation.value).toBeNull()
    expect(prepay.partialOption.value).toBe('REDUCE_TERM')
  })

  it('valida montos mayores a cero para la simulación de abono extraordinario', async () => {
    setActivePinia(createPinia())
    const prepay = usePrepayment()

    const res = await prepay.simulatePartialPrepayment('loan-001', '0')
    expect(res).toBeNull()
    expect(prepay.partialError.value).toContain('mayor a cero')
  })
})

describe('usePaymentHistory Composable (FE-CP-13 y FE-CP-15)', () => {
  it('filtra pagos por estado y calcula totales pendientes y aplicados', () => {
    setActivePinia(createPinia())
    const history = usePaymentHistory()

    const mockPayments: Payment[] = [
      {
        id: 'pay-001',
        loanId: 'loan-001',
        customerName: 'María Quispe',
        kind: 'NORMAL',
        amount: '250.00',
        method: 'YAPE',
        status: 'APPLIED',
        receiptNumber: 'REC-20261106-0001',
        paidAt: '2026-11-06T10:00:00Z',
        allocations: [],
        createdAt: '2026-11-06T10:00:00Z',
      },
      {
        id: 'pay-002',
        loanId: 'loan-001',
        customerName: 'María Quispe',
        kind: 'NORMAL',
        amount: '500.00',
        method: 'TRANSFERENCIA_BANCARIA',
        status: 'PENDING_CONFIRMATION',
        externalReference: 'VOUCHER-bcp.jpg',
        paidAt: '2026-11-06T11:00:00Z',
        allocations: [],
        createdAt: '2026-11-06T11:00:00Z',
      },
    ]

    history.payments.value = mockPayments

    expect(history.pendingCount.value).toBe(1)
    expect(history.appliedCount.value).toBe(1)
    expect(history.filteredPayments.value).toHaveLength(2)

    history.statusFilter.value = 'PENDING_CONFIRMATION'
    expect(history.filteredPayments.value).toHaveLength(1)
    expect(history.filteredPayments.value[0]!.id).toBe('pay-002')

    history.statusFilter.value = 'APPLIED'
    expect(history.filteredPayments.value).toHaveLength(1)
    expect(history.filteredPayments.value[0]!.id).toBe('pay-001')
  })
})
