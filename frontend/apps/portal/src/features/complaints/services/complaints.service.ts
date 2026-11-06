import type {
  ComplaintClaimRecord,
  ComplaintClaimRequest,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

/**
 * Servicio del Libro de Reclamaciones Virtual (FE-CP-21 / Ley 29571 Indecopi).
 */
export const complaintsService = {
  /**
   * Registra una nueva hoja de reclamación (Reclamo o Queja) y retorna el correlativo oficial.
   */
  async submitComplaint(payload: ComplaintClaimRequest): Promise<ComplaintClaimRecord> {
    const { data } = await useApi().post<ComplaintClaimRecord>('/complaints', payload)
    return data
  },

  /**
   * Consulta el estado de una reclamación mediante su número correlativo (ej. LR-2026-00001).
   */
  async getComplaint(correlative: string): Promise<ComplaintClaimRecord> {
    const { data } = await useApi().get<ComplaintClaimRecord>(
      `/complaints/${encodeURIComponent(correlative)}`,
    )
    return data
  },
}
