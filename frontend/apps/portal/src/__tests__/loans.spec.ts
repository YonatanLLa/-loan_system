import { describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { Installment } from '@prestamos/api-client'

import { loansService } from '../features/loans/services'
import { useClientLoans, useLoanSchedule } from '../features/loans/composables'

describe('Loans Service (FE-CP-10 y FE-CP-11)', () => {
  it('expone todas las operaciones de préstamos, cronograma y estados de cuenta', () => {
    expect(typeof loansService.fetchMyLoans).toBe('function')
    expect(typeof loansService.fetchLoanDetail).toBe('function')
    expect(typeof loansService.fetchLoanSchedule).toBe('function')
    expect(typeof loansService.fetchLoanStatement).toBe('function')
  })
})

describe('useClientLoans Composable (FE-CP-10)', () => {
  it('inicializa estados y detecta si existen préstamos o préstamos en mora', () => {
    setActivePinia(createPinia())
    const clientLoans = useClientLoans()

    expect(clientLoans.loans.value).toEqual([])
    expect(clientLoans.hasLoans.value).toBe(false)
    expect(clientLoans.hasOverdueLoans.value).toBe(false)
    expect(clientLoans.totalOutstanding.value).toBe('0.00')

    // Agrega préstamos simulados
    clientLoans.loans.value = [
      {
        id: 'loan-001',
        applicationId: 'app-001',
        customerId: 'cus-001',
        customerName: 'María Quispe',
        productId: 'prod-001',
        productName: 'Crédito Personal',
        principal: '3000.00',
        disbursedAmount: '3000.00',
        annualRate: '0.350000',
        amortizationMethod: 'FRANCES',
        frequency: 'MENSUAL',
        term: 12,
        graceDays: 0,
        status: 'EN_MORA',
        outstandingPrincipal: '2200.00',
        totalPaid: '800.00',
        scheduleVersion: 1,
        moraRatePerDay: '0.0005',
        createdAt: '2026-01-01T00:00:00Z',
      },
    ]

    expect(clientLoans.hasLoans.value).toBe(true)
    expect(clientLoans.hasOverdueLoans.value).toBe(true)
    expect(clientLoans.totalOutstanding.value).toBe('2200.00')
    expect(clientLoans.activeLoan.value?.id).toBe('loan-001')
  })
})

describe('useLoanSchedule Composable (FE-CP-11)', () => {
  it('semaforiza los estados de cuota por color según la normativa FE-CP-11', () => {
    setActivePinia(createPinia())
    const { getInstallmentColorClass, getInstallmentStatusLabel } = useLoanSchedule()

    const baseInst: Installment = {
      number: 1,
      dueDate: '2026-11-20',
      principal: '250.00',
      interest: '50.00',
      fee: '5.00',
      totalDue: '305.00',
      balance: '2750.00',
      paidPrincipal: '0.00',
      paidInterest: '0.00',
      paidFee: '0.00',
      lateFee: '0.00',
      paidLateFee: '0.00',
      daysLate: 0,
      status: 'PAID',
    }

    // 1. Verde para cuota pagada
    expect(getInstallmentColorClass(baseInst)).toContain('success')
    expect(getInstallmentStatusLabel('PAID')).toBe('Pagada')

    // 2. Amarillo para cuota pendiente próxima
    const pendingInst: Installment = { ...baseInst, status: 'PENDING' }
    expect(getInstallmentColorClass(pendingInst)).toContain('amber')
    expect(getInstallmentStatusLabel('PENDING')).toBe('Pendiente')

    // 3. Rojo para cuota vencida en mora
    const overdueInst: Installment = { ...baseInst, status: 'OVERDUE', daysLate: 8 }
    expect(getInstallmentColorClass(overdueInst)).toContain('destructive')
    expect(getInstallmentStatusLabel('OVERDUE')).toBe('Vencida en Mora')

    // 4. Azul para cuota con pago parcial
    const partialInst: Installment = { ...baseInst, status: 'PARTIAL' }
    expect(getInstallmentColorClass(partialInst)).toContain('primary')
    expect(getInstallmentStatusLabel('PARTIAL')).toBe('Pago Parcial')
  })
})
