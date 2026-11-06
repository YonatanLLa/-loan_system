import { describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { complaintsService } from '../features/complaints/services'
import { useComplaints } from '../features/complaints/composables'

describe('Complaints Service (FE-CP-21 / Ley 29571)', () => {
  it('expone registro y consulta de hojas de reclamación', () => {
    expect(typeof complaintsService.submitComplaint).toBe('function')
    expect(typeof complaintsService.getComplaint).toBe('function')
  })
})

describe('useComplaints Composable (FE-CP-21)', () => {
  it('inicializa formulario y valida campos obligatorios antes de enviar', async () => {
    setActivePinia(createPinia())
    const complaints = useComplaints()

    expect(complaints.form.value.claimType).toBe('RECLAMO')
    expect(complaints.form.value.consumerFullName).toBe('')

    // Intenta enviar vacío
    const res = await complaints.submitClaim()
    expect(res).toBeNull()
    expect(complaints.errorMessage.value).toContain('nombres y apellidos')
  })

  it('permite reiniciar el formulario con resetForm()', () => {
    setActivePinia(createPinia())
    const complaints = useComplaints()

    complaints.form.value.consumerFullName = 'Juan Perez'
    complaints.errorMessage.value = 'Un error previo'

    complaints.resetForm()
    expect(complaints.form.value.consumerFullName).toBe('')
    expect(complaints.errorMessage.value).toBeNull()
  })
})
