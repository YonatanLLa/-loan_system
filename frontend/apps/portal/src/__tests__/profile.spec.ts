import { describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { profileService } from '../features/profile/services'
import { useProfile } from '../features/profile/composables'

describe('Profile Service (FE-CP-18 y FE-CP-19)', () => {
  it('expone todas las operaciones de cuentas bancarias y devolución de saldo', () => {
    expect(typeof profileService.fetchProfile).toBe('function')
    expect(typeof profileService.fetchBankAccounts).toBe('function')
    expect(typeof profileService.changePrimaryAccount).toBe('function')
    expect(typeof profileService.fetchRefunds).toBe('function')
    expect(typeof profileService.requestRefund).toBe('function')
  })
})

describe('useProfile Composable (FE-CP-18 y FE-CP-19)', () => {
  it('inicializa estados y detecta cuenta en periodo de enfriamiento (cooling-off)', () => {
    setActivePinia(createPinia())
    const profileComp = useProfile()

    expect(profileComp.profile.value).toBeNull()
    expect(profileComp.bankAccounts.value).toEqual([])
    expect(profileComp.refunds.value).toEqual([])
    expect(profileComp.coolingOffAccount.value).toBeUndefined()

    // Cuentas simuladas con una en enfriamiento
    const futureDate = new Date(Date.now() + 24 * 3600 * 1000).toISOString()
    profileComp.bankAccounts.value = [
      {
        id: 'cba-001',
        customerId: 'cus-001',
        bankName: 'BCP',
        accountType: 'CUENTA_AHORRO',
        accountNumberMasked: '**** 1234',
        cci: '00219000000000010000',
        isVerified: true,
        isPrimary: true,
        status: 'ACTIVE',
        coolingOffUntil: futureDate,
      },
    ]

    expect(profileComp.primaryAccount.value?.id).toBe('cba-001')
    expect(profileComp.coolingOffAccount.value?.id).toBe('cba-001')
  })
})
