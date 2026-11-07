import { z } from 'zod';
import { schemas } from '@prestamos/utils';

export const complaintSchema = z.object({
  type: z.enum(['QUEJA', 'RECLAMO']),
  fullName: z.string().min(3, 'Ingresa tu nombre completo'),
  docType: z.enum(['DNI', 'CE', 'RUC']),
  docNumber: schemas.dni,
  email: schemas.email,
  phone: schemas.celularPe,
  detail: z.string().min(10, 'Describe el hecho con al menos 10 caracteres'),
  requestedSolution: z.string().min(5, 'Indica qué solución solicitas'),
  loanId: z.string().optional(),
});

export type ComplaintFormData = z.infer<typeof complaintSchema>;
