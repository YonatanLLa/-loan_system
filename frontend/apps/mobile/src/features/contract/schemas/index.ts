import { z } from 'zod';

export const signContractSchema = z.object({
  loanId: z.string().min(1),
  otpCode: z.string().length(6, 'El código OTP debe tener 6 dígitos').regex(/^\d{6}$/, 'Solo dígitos'),
});

export type SignContractFormData = z.infer<typeof signContractSchema>;
