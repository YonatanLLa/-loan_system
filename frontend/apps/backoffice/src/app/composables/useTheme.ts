import { computed, readonly, ref } from 'vue'

export type ThemeMode = 'light' | 'dark' | 'system'
export type ResolvedTheme = 'light' | 'dark'

export const THEME_STORAGE_KEY = 'prestamos.backoffice.theme'

function isThemeMode(value: unknown): value is ThemeMode {
  return value === 'light' || value === 'dark' || value === 'system'
}

function readStoredTheme(): ThemeMode {
  try {
    if (typeof localStorage === 'undefined') return 'system'
    const raw = localStorage.getItem(THEME_STORAGE_KEY)
    return isThemeMode(raw) ? raw : 'system'
  } catch {
    return 'system'
  }
}

function writeStoredTheme(mode: ThemeMode): void {
  try {
    if (typeof localStorage === 'undefined') return
    localStorage.setItem(THEME_STORAGE_KEY, mode)
  } catch {
    // Ignorar errores de almacenamiento (modo privado / cuota excedida).
  }
}

function getSystemPrefersDark(): boolean {
  try {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
      return false
    }
    return window.matchMedia('(prefers-color-scheme: dark)').matches
  } catch {
    return false
  }
}

function resolveThemeValue(mode: ThemeMode, prefersDark: boolean): ResolvedTheme {
  if (mode === 'system') {
    return prefersDark ? 'dark' : 'light'
  }
  return mode
}

function applyDocumentTheme(resolved: ResolvedTheme): void {
  if (typeof document === 'undefined') return
  const root = document.documentElement
  root.classList.toggle('dark', resolved === 'dark')
  root.style.colorScheme = resolved
}

const themeMode = ref<ThemeMode>(readStoredTheme())
const systemPrefersDark = ref<boolean>(getSystemPrefersDark())

let mediaQueryList: MediaQueryList | null = null
let mediaChangeHandler: ((event: MediaQueryListEvent) => void) | null = null

const resolvedTheme = computed<ResolvedTheme>(() =>
  resolveThemeValue(themeMode.value, systemPrefersDark.value),
)
const isDark = computed(() => resolvedTheme.value === 'dark')

function detachMediaListener(): void {
  if (mediaQueryList && mediaChangeHandler && typeof mediaQueryList.removeEventListener === 'function') {
    mediaQueryList.removeEventListener('change', mediaChangeHandler)
  }
  mediaQueryList = null
  mediaChangeHandler = null
}

function attachMediaListener(): void {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return
  detachMediaListener()
  const mql = window.matchMedia('(prefers-color-scheme: dark)')
  systemPrefersDark.value = Boolean(mql.matches)
  if (typeof mql.addEventListener === 'function') {
    const handler = (event: MediaQueryListEvent) => {
      systemPrefersDark.value = event.matches
      if (themeMode.value === 'system') {
        applyDocumentTheme(resolvedTheme.value)
      }
    }
    mql.addEventListener('change', handler)
    mediaQueryList = mql
    mediaChangeHandler = handler
  }
}

/** Inicializa el tema desde `localStorage` + `prefers-color-scheme` y suscribe cambios del sistema. */
export function initTheme(): void {
  themeMode.value = readStoredTheme()
  systemPrefersDark.value = getSystemPrefersDark()
  attachMediaListener()
  applyDocumentTheme(resolvedTheme.value)
}

/** Cambia la preferencia de tema, la persiste en `localStorage` y actualiza el DOM. */
export function setTheme(mode: ThemeMode): void {
  themeMode.value = mode
  writeStoredTheme(mode)
  applyDocumentTheme(resolvedTheme.value)
}

/** Alterna explícitamente entre modo claro y oscuro según el tema resuelto actual. */
export function toggleTheme(): void {
  setTheme(isDark.value ? 'light' : 'dark')
}

/** Restablece el estado del singleton en pruebas unitarias. */
export function resetThemeForTesting(): void {
  detachMediaListener()
  themeMode.value = readStoredTheme()
  systemPrefersDark.value = getSystemPrefersDark()
  if (typeof document !== 'undefined') {
    document.documentElement.classList.remove('dark')
    document.documentElement.style.colorScheme = ''
  }
}

/** Composable singleton para gestionar el modo claro / oscuro / sistema del backoffice. */
export function useTheme() {
  return {
    themeMode: readonly(themeMode),
    resolvedTheme,
    isDark,
    initTheme,
    setTheme,
    toggleTheme,
  }
}
