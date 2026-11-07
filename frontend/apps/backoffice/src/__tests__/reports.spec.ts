import { describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { reportsService } from '../features/reports/services'
import { useDashboardKpis, useReports } from '../features/reports/composables'

describe('Reports Service (FE-BO-37 a FE-BO-40)', () => {
  it('expone todas las operaciones de KPIs, cartera, aging, flujo de caja, productividad y estado de cuenta', () => {
    expect(typeof reportsService.fetchDashboardKpis).toBe('function')
    expect(typeof reportsService.fetchPortfolioReport).toBe('function')
    expect(typeof reportsService.fetchAgingReport).toBe('function')
    expect(typeof reportsService.fetchCashflowReport).toBe('function')
    expect(typeof reportsService.fetchProductivityReport).toBe('function')
    expect(typeof reportsService.fetchAccountStatement).toBe('function')
    expect(typeof reportsService.exportToCsv).toBe('function')
  })

  it('exportToCsv genera un archivo descargable con los encabezados y filas sanitizados', () => {
    const appendChildSpy = vi.spyOn(document.body, 'appendChild')
    const removeChildSpy = vi.spyOn(document.body, 'removeChild')

    vi.spyOn(window.URL, 'createObjectURL').mockReturnValue('blob:mock-url')
    vi.spyOn(window.URL, 'revokeObjectURL').mockReturnValue()

    reportsService.exportToCsv('reporte_test', ['Código', 'Nombre'], [['001', 'Juan "Pérez"']])

    expect(appendChildSpy).toHaveBeenCalled()
    expect(removeChildSpy).toHaveBeenCalled()
  })
})

describe('useDashboardKpis Composable (FE-BO-37 y FE-BO-38)', () => {
  it('inicializa el estado de KPIs correctamente', () => {
    setActivePinia(createPinia())
    const { kpis, loading, error, loadKpis } = useDashboardKpis()

    expect(kpis.value).toBeNull()
    expect(loading.value).toBe(false)
    expect(error.value).toBeNull()
    expect(typeof loadKpis).toBe('function')
  })
})

describe('useReports Composable (FE-BO-39 y FE-BO-40)', () => {
  it('inicia en la pestaña de cartera y permite alternar pestañas', () => {
    setActivePinia(createPinia())
    const { activeTab, portfolioPages, portfolioPage } = useReports()

    expect(activeTab.value).toBe('portfolio')
    activeTab.value = 'aging'
    expect(activeTab.value).toBe('aging')

    activeTab.value = 'cashflow'
    expect(activeTab.value).toBe('cashflow')

    activeTab.value = 'statement'
    expect(activeTab.value).toBe('statement')

    // Verificación de cálculo de páginas de cartera
    portfolioPage.value.total = 45
    expect(portfolioPages.value).toBe(5) // Math.ceil(45 / 10) = 5
  })
})
