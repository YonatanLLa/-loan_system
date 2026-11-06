import { readonly, ref } from 'vue'

/**
 * Envuelve una acción asíncrona y bloquea nuevas ejecuciones mientras está en curso.
 * Para operaciones que mueven dinero se combina con `createIdempotentAction` de
 * @prestamos/api-client (FE-NF-04): botón bloqueado + misma Idempotency-Key.
 */
export function useBusyAction<TArgs extends unknown[], TResult>(
  action: (...args: TArgs) => Promise<TResult>,
) {
  const isBusy = ref(false)

  async function run(...args: TArgs): Promise<TResult | undefined> {
    if (isBusy.value) return undefined
    isBusy.value = true
    try {
      return await action(...args)
    } finally {
      isBusy.value = false
    }
  }

  return { run, isBusy: readonly(isBusy) }
}
