import { z } from 'zod';
import { schemas } from '@prestamos/utils';

export const paymentSchema = z.object({
  loanId: z.string().min(1),
  installmentId: z.string().optional(),
  amount: schemas.monto,
  paymentMethod: z.enum([
    'TRANSFERENCIA_BANCARIA',
    'YAPE',
    'PLIN',
    'TARJETA_DEBITO',
    'TARJETA_CREDITO',
    'PASARELA_PAGO',
  ]),
  operationNumber: z.string().optional(),
});

export type PaymentFormData = z.infer<typeof paymentSchema>;
