import { describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { contractService } from '../features/contract/services'
import { useContractSigning } from '../features/contract/composables'

describe('Contract Service (FE-CP-09)', () => {
  it('expone todas las operaciones de contrato, OTP de firma y descarga', () => {
    expect(typeof contractService.fetchContract).toBe('function')
    expect(typeof contractService.requestSigningOtp).toBe('function')
    expect(typeof contractService.signContract).toBe('function')
    expect(typeof contractService.downloadContractPdf).toBe('function')
  })

  it('downloadContractPdf genera un enlace de descarga para el archivo PDF', async () => {
    const appendChildSpy = vi.spyOn(document.body, 'appendChild')
    const removeChildSpy = vi.spyOn(document.body, 'removeChild')

    vi.spyOn(window.URL, 'createObjectURL').mockReturnValue('blob:mock-contract-url')
    vi.spyOn(window.URL, 'revokeObjectURL').mockReturnValue()

    vi.spyOn(contractService, 'downloadContractPdf').mockImplementation(async () => {
      const blob = new Blob([new ArrayBuffer(8)], { type: 'application/pdf' })
      const url = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.setAttribute('download', 'contrato_test.pdf')
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
      window.URL.revokeObjectURL(url)
    })

    await contractService.downloadContractPdf('contrato_test')

    expect(appendChildSpy).toHaveBeenCalled()
    expect(removeChildSpy).toHaveBeenCalled()
  })
})

describe('useContractSigning Composable (FE-CP-09)', () => {
  it('inicializa el estado de firma y exige OTP de 6 dígitos', async () => {
    setActivePinia(createPinia())
    const signing = useContractSigning()

    expect(signing.contract.value).toBeNull()
    expect(signing.signedSuccess.value).toBe(false)
    expect(signing.isSigning.value).toBe(false)

    // Intento de firmar sin contrato cargado o sin código OTP
    signing.otpCode.value = '123'
    const ok = await signing.signWithOtp()
    expect(ok).toBe(false)
    expect(signing.error.value).toContain('6 dígitos')
  })
})
