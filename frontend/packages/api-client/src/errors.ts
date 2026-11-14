import axios from 'axios'

/** Cuerpo de error estándar del backend (RFC 7807, ASP.NET Core ProblemDetails). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
  code?: string
  errorType?: string
  /** Errores de validación por campo: { "Amount": ["El monto es obligatorio"] } */
  errors?: Record<string, string[]>
}

export interface ApiErrorInit {
  status: number
  title: string
  detail: string
  code?: string
  errorType?: string
  fieldErrors?: Record<string, string[]>
  traceId?: string
}

/** Único tipo de error que ven las features (FE-NF-05). */
export class ApiError extends Error {
  readonly status: number
  readonly title: string
  readonly detail: string
  readonly code?: string
  readonly errorType?: string
  readonly fieldErrors: Record<string, string[]>
  readonly traceId?: string

  constructor(init: ApiErrorInit) {
    super(init.detail)
    this.name = 'ApiError'
    this.status = init.status
    this.title = init.title
    this.detail = init.detail
    this.code = init.code
    this.errorType = init.errorType
    this.fieldErrors = init.fieldErrors ?? {}
    this.traceId = init.traceId
  }

  get isNetworkError(): boolean {
    return this.status === 0
  }
}

const DEFAULT_TITLES: Record<number, string> = {
  400: 'Solicitud inválida',
  401: 'Sesión no válida',
  403: 'Sin permiso',
  404: 'No encontrado',
  409: 'Conflicto',
  422: 'No se puede procesar',
  423: 'Cuenta bloqueada',
  429: 'Demasiadas solicitudes',
  500: 'Error del servidor',
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error

  if (axios.isAxiosError(error)) {
    if (!error.response) {
      const timedOut = error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT'
      return new ApiError({
        status: 0,
        title: 'Sin conexión',
        detail: timedOut
          ? 'El servidor tardó demasiado en responder. Inténtalo nuevamente.'
          : 'No se pudo conectar con el servidor. Revisa tu conexión.',
      })
    }

    const { status } = error.response
    const body = isProblemDetails(error.response.data) ? error.response.data : undefined
    const title = body?.title ?? DEFAULT_TITLES[status] ?? 'Error inesperado'
    return new ApiError({
      status,
      title,
      detail: body?.detail ?? title,
      code: body?.code,
      errorType: body?.errorType,
      fieldErrors: body?.errors,
      traceId: body?.traceId,
    })
  }

  return new ApiError({
    status: 0,
    title: 'Error inesperado',
    detail: error instanceof Error ? error.message : String(error),
  })
}

/**
 * Convierte los errores por campo del backend ("Amount", "Customer.DocNumber")
 * a los nombres de campo del formulario ("amount", "customer.docNumber"),
 * listos para `setErrors` de VeeValidate.
 */
export function toFormErrors(error: ApiError): Record<string, string> {
  const result: Record<string, string> = {}
  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    const [first] = messages
    if (first === undefined) continue
    const name = field
      .split('.')
      .map((part) => part.charAt(0).toLowerCase() + part.slice(1))
      .join('.')
    result[name] = first
  }
  return result
}

function isProblemDetails(value: unknown): value is ProblemDetails {
  return (
    typeof value === 'object' &&
    value !== null &&
    ('title' in value || 'detail' in value || 'code' in value)
  )
}
