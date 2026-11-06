import type { AxiosRequestConfig } from 'axios'

export const IDEMPOTENCY_HEADER = 'Idempotency-Key'

export function newIdempotencyKey(): string {
  return crypto.randomUUID()
}

/** Config de Axios con la cabecera Idempotency-Key (RF-13.2, RF-16.2). */
export function withIdempotency(key: string, config: AxiosRequestConfig = {}): AxiosRequestConfig {
  return { ...config, headers: { ...config.headers, [IDEMPOTENCY_HEADER]: key } }
}

/**
 * Acción que mueve dinero: genera UNA clave al primer intento y la reutiliza en
 * cada reintento hasta que la operación termina bien. Así un doble clic o una
 * caída de red no duplican un desembolso ni un pago (FE-NF-04).
 */
export function createIdempotentAction<TArgs extends unknown[], TResult>(
  run: (idempotencyKey: string, ...args: TArgs) => Promise<TResult>,
) {
  let key: string | null = null

  return {
    async execute(...args: TArgs): Promise<TResult> {
      key ??= newIdempotencyKey()
      const result = await run(key, ...args)
      key = null
      return result
    },
    /** Descarta la clave, p. ej. si el usuario cambia los datos de la operación. */
    reset() {
      key = null
    },
    get pendingKey() {
      return key
    },
  }
}
