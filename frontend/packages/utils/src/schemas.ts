import { z } from 'zod'

/**
 * Primitivas de validación compartidas (ARQUITECTURA_FRONTEND §7).
 *
 * La validación del frontend es para la experiencia de usuario: el backend
 * vuelve a validar todo. Si el catálogo de tipos de documento del backend
 * (RF-42.1) trae otro regex, manda el catálogo.
 */

export const dni = z.string().trim().regex(/^\d{8}$/, 'El DNI tiene 8 dígitos')

export const ruc = z.string().trim().regex(/^\d{11}$/, 'El RUC tiene 11 dígitos')

export const carnetExtranjeria = z
  .string()
  .trim()
  .regex(/^\d{9}$/, 'El carné de extranjería tiene 9 dígitos')

/** Código de Cuenta Interbancario (Perú): 20 dígitos. */
export const cci = z.string().trim().regex(/^\d{20}$/, 'El CCI tiene 20 dígitos')

/** Celular peruano: 9 dígitos y empieza con 9. */
export const celularPe = z
  .string()
  .trim()
  .regex(/^9\d{8}$/, 'El celular tiene 9 dígitos y empieza con 9')

export const email = z.string().trim().email('Ingresa un correo válido')

/** Monto como string decimal positivo con hasta 2 decimales (así viaja a la API). */
export const monto = z
  .string()
  .trim()
  .regex(/^\d+(\.\d{1,2})?$/, 'Ingresa un monto con hasta 2 decimales')
  .refine((value) => Number(value) > 0, 'El monto debe ser mayor a cero')

/** Correo o celular, para el ingreso al portal del cliente (US-26). */
export const correoOCelular = z
  .string()
  .trim()
  .refine(
    (value) => email.safeParse(value).success || celularPe.safeParse(value).success,
    'Ingresa tu correo o tu celular de 9 dígitos',
  )
