import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { defineComponent } from 'vue'

import ThemeToggle from '@/app/components/ThemeToggle.vue'
import {
  THEME_STORAGE_KEY,
  initTheme,
  resetThemeForTesting,
  useTheme,
} from '@/app/composables/useTheme'
import LoginPage from '@/features/auth/pages/LoginPage.vue'

const Empty = defineComponent({ render: () => null })

function createMockMatchMedia(initialMatches: boolean) {
  let matches = initialMatches
  let listener: ((event: MediaQueryListEvent) => void) | null = null

  const mql = {
    get matches() {
      return matches
    },
    media: '(prefers-color-scheme: dark)',
    onchange: null,
    addEventListener: vi.fn<(type: string, cb: (event: MediaQueryListEvent) => void) => void>(
      (type, cb) => {
        if (type === 'change') listener = cb
      },
    ),
    removeEventListener: vi.fn<(type: string, cb: (event: MediaQueryListEvent) => void) => void>(
      (type, cb) => {
        if (type === 'change' && listener === cb) listener = null
      },
    ),
    addListener: vi.fn<() => void>(),
    removeListener: vi.fn<() => void>(),
    dispatchEvent: vi.fn<() => boolean>(() => true),
  }

  return {
    matchMedia: vi.fn<(query: string) => MediaQueryList>(() => mql as unknown as MediaQueryList),
    triggerChange(nextMatches: boolean) {
      matches = nextMatches
      listener?.({ matches: nextMatches } as MediaQueryListEvent)
    },
  }
}

describe('useTheme & ThemeToggle (Modo oscuro)', () => {
  const originalMatchMedia = window.matchMedia

  beforeEach(() => {
    localStorage.clear()
    Object.defineProperty(window, 'matchMedia', {
      value: undefined,
      writable: true,
      configurable: true,
    })
    resetThemeForTesting()
  })

  afterEach(() => {
    Object.defineProperty(window, 'matchMedia', {
      value: originalMatchMedia,
      writable: true,
      configurable: true,
    })
    resetThemeForTesting()
  })

  it('usa "system" por defecto (también ante valores inválidos) y soporta ausencia de matchMedia', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'tema-desconocido')
    initTheme()

    const { themeMode, resolvedTheme, isDark } = useTheme()
    expect(themeMode.value).toBe('system')
    expect(resolvedTheme.value).toBe('light')
    expect(isDark.value).toBe(false)
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(document.documentElement.style.colorScheme).toBe('light')
  })

  it('sincroniza con prefers-color-scheme y responde a eventos change en modo system', () => {
    const mockMedia = createMockMatchMedia(true)
    Object.defineProperty(window, 'matchMedia', {
      value: mockMedia.matchMedia,
      writable: true,
      configurable: true,
    })

    initTheme()
    const { themeMode, resolvedTheme, isDark, setTheme } = useTheme()

    expect(themeMode.value).toBe('system')
    expect(resolvedTheme.value).toBe('dark')
    expect(isDark.value).toBe(true)
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(document.documentElement.style.colorScheme).toBe('dark')

    // Sistema cambia a claro mientras mode === 'system'
    mockMedia.triggerChange(false)
    expect(resolvedTheme.value).toBe('light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(document.documentElement.style.colorScheme).toBe('light')

    // Si el usuario fija explícitamente 'dark', cambios del SO no sobrescriben
    setTheme('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
    mockMedia.triggerChange(false)
    expect(resolvedTheme.value).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })

  it('persiste setTheme y toggleTheme alterna correctamente entre claro y oscuro', () => {
    const mockMedia = createMockMatchMedia(true)
    Object.defineProperty(window, 'matchMedia', {
      value: mockMedia.matchMedia,
      writable: true,
      configurable: true,
    })

    initTheme()
    const { themeMode, resolvedTheme, toggleTheme } = useTheme()
    expect(resolvedTheme.value).toBe('dark')

    // Desde system (resuelto dark) alterna a light explícito
    toggleTheme()
    expect(themeMode.value).toBe('light')
    expect(resolvedTheme.value).toBe('light')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)

    // Alterna a dark explícito
    toggleTheme()
    expect(themeMode.value).toBe('dark')
    expect(resolvedTheme.value).toBe('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(document.documentElement.style.colorScheme).toBe('dark')
  })

  it('ThemeToggle actualiza aria-label, aria-pressed y alterna el tema al hacer clic', async () => {
    initTheme()
    const wrapper = mount(ThemeToggle)

    const btn = wrapper.find('button')
    expect(btn.attributes('aria-label')).toBe('Cambiar a modo oscuro')
    expect(btn.attributes('aria-pressed')).toBe('false')

    await btn.trigger('click')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(btn.attributes('aria-label')).toBe('Cambiar a modo claro')
    expect(btn.attributes('aria-pressed')).toBe('true')

    await btn.trigger('click')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(btn.attributes('aria-label')).toBe('Cambiar a modo oscuro')
  })

  it('renderiza ThemeToggle en LoginPage para alternar tema antes de iniciar sesión', async () => {
    initTheme()
    const pinia = createPinia()
    setActivePinia(pinia)
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/ingresar', name: 'login', component: LoginPage },
        { path: '/', name: 'dashboard', component: Empty },
      ],
    })
    await router.push('/ingresar')
    await router.isReady()

    const wrapper = mount(LoginPage, { global: { plugins: [pinia, router] } })
    const toggle = wrapper.find('button[aria-label="Cambiar a modo oscuro"]')
    expect(toggle.exists()).toBe(true)

    await toggle.trigger('click')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })
})
