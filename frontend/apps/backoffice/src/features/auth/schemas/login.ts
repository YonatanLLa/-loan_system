import { z } from 'zod'
import { schemas } from '@prestamos/utils'

export const loginSchema = z.object({
  email: schemas.email,
  password: z.string().min(1, 'Ingresa tu contraseña'),
})

export type LoginForm = z.infer<typeof loginSchema>
