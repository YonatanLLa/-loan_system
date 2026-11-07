import { z } from 'zod';
import { schemas } from '@prestamos/utils';

export const simulateSchema = z.object({
  amount: schemas.monto,
  termMonths: z.number().int().min(1, 'El plazo mínimo es 1 mes').max(60, 'El plazo máximo es 60 meses'),
});

export const applicationSchema = z.object({
  productId: z.string().min(1, 'Selecciona un producto'),
  requestedAmount: schemas.monto,
  termMonths: z.number().int().min(1).max(60),
  monthlyIncome: schemas.monto,
  occupation: z.string().min(3, 'Ingresa tu ocupación'),
  employerName: z.string().optional(),
});

export type SimulateFormData = z.infer<typeof simulateSchema>;
export type ApplicationFormData = z.infer<typeof applicationSchema>;
