import { describe, expect, it } from 'vitest'

import router from '../app/router'

describe('router del portal', () => {
  it('registra las rutas de todas las features del cliente', () => {
    const names = router.getRoutes().map((route) => route.name)
    for (const name of ['home', 'loans', 'apply', 'applications', 'contract', 'pay', 'profile']) {
      expect(names).toContain(name)
    }
  })

  it('exige KYC verificado para solicitar un préstamo y firmar el contrato', () => {
    expect(router.resolve({ name: 'apply' }).meta.requiresKyc).toBe(true)
    expect(router.resolve({ name: 'contract' }).meta.requiresKyc).toBe(true)
  })

  it('deja público el Libro de Reclamaciones', () => {
    expect(router.resolve({ name: 'complaints' }).meta.public).toBe(true)
  })
})
