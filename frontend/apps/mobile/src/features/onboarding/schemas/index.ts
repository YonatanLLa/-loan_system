import { z } from 'zod';
import { schemas } from '@prestamos/utils';

export const bankAccountSchema = z
  .object({
    bankName: z.string().min(2, 'Selecciona o ingresa tu banco'),
    accountType: z.enum(['CUENTA_CORRIENTE', 'CUENTA_AHORRO', 'BILLETERA_DIGITAL']),
    cci: schemas.cci.optional(),
    phoneWallet: schemas.celularPe.optional(),
  })
  .refine(
    (data: { accountType: string; phoneWallet?: string; cci?: string }) =>
      data.accountType === 'BILLETERA_DIGITAL' ? !!data.phoneWallet : !!data.cci,
    {
      message: 'Para cuentas bancarias ingresa el CCI de 20 dígitos; para billeteras el celular',
      path: ['cci'],
    }
  );

export const consentsSchema = z.object({
  privacyAccepted: z.literal(true, {
    errorMap: () => ({ message: 'Debes aceptar la política de privacidad' }),
  }),
  creditBureauAccepted: z.literal(true, {
    errorMap: () => ({ message: 'Debes autorizar la consulta en centrales de riesgo' }),
  }),
  marketingAccepted: z.boolean().default(false),
});

export type BankAccountFormData = z.infer<typeof bankAccountSchema>;
export type ConsentsFormData = z.infer<typeof consentsSchema>;
