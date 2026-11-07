import { z } from 'zod';
import { schemas } from '@prestamos/utils';

export const prepaymentSchema = z.object({
  loanId: z.string().min(1),
  type: z.enum(['TOTAL', 'PARTIAL']),
  amount: schemas.monto.optional(),
  reductionOption: z.enum(['REDUCE_TERM', 'REDUCE_INSTALLMENT']).optional(),
});

export type PrepaymentFormData = z.infer<typeof prepaymentSchema>;
