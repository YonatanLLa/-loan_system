import { z } from 'zod';
import { schemas } from '@prestamos/utils';

export const loginSchema = z.object({
  identifier: schemas.correoOCelular,
  password: z.string().min(6, 'La contraseña debe tener al menos 6 caracteres').optional(),
});

export const registerSchema = z
  .object({
    docType: z.enum(['DNI', 'CE']),
    docNumber: z.string().trim(),
    fullName: z.string().trim().min(3, 'Ingresa tu nombre completo'),
    email: schemas.email,
    phone: schemas.celularPe,
  })
  .superRefine((data, ctx) => {
    const res =
      data.docType === 'DNI'
        ? schemas.dni.safeParse(data.docNumber)
        : schemas.carnetExtranjeria.safeParse(data.docNumber);
    if (!res.success) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: res.error.issues[0]?.message ?? 'Documento inválido',
        path: ['docNumber'],
      });
    }
  });

export const otpSchema = z.object({
  code: z.string().length(6, 'El código debe tener 6 dígitos').regex(/^\d{6}$/, 'Solo dígitos'),
});

export type LoginFormData = z.infer<typeof loginSchema>;
export type RegisterFormData = z.infer<typeof registerSchema>;
export type OtpFormData = z.infer<typeof otpSchema>;
