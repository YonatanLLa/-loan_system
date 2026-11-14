import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { defineComponent } from 'vue'

import {
  AxiosError,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from '@prestamos/api-client'

import { setupApi } from '@/app/api'
import {
  THEME_STORAGE_KEY,
  initTheme,
  resetThemeForTesting,
  useTheme,
} from '@/app/composables/useTheme'
import { buildMenu } from '@/app/navigation'
import { useSessionStore } from '@/app/stores/session'
import { profileRoutes } from '@/features/profile'
import ProfilePage from '@/features/profile/pages/ProfilePage.vue'

const Empty = defineComponent({ render: () => null })

function ok(config: InternalAxiosRequestConfig, data: unknown): Promise<AxiosResponse> {
  return Promise.resolve({ data, status: 200, statusText: 'OK', headers: {}, config })
}

function fail(config: InternalAxiosRequestConfig, status: number, data: unknown): Promise<never> {
  const response = { data, status, statusText: 'Error', headers: {}, config } as AxiosResponse
  return Promise.reject(new AxiosError(`HTTP ${status}`, String(status), config, null, response))
}

async function mountProfilePage() {
  const pinia = createPinia()
  setActivePinia(pinia)

  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: Empty, children: [...profileRoutes] },
      { path: '/ingresar', name: 'login', component: Empty, meta: { public: true } },
    ],
  })

  const session = useSessionStore(pinia)
  await session.refreshProfile()

  await router.push('/perfil')
  await router.isReady()

  const wrapper = mount(ProfilePage, {
    global: { plugins: [pinia, router] },
  })

  return { wrapper, router, session }
}

describe('ProfilePage (Mi perfil · GET /api/me & POST /api/auth/logout)', () => {
  beforeEach(() => {
    sessionStorage.clear()
    localStorage.clear()
    resetThemeForTesting()
    initTheme()
    document.body.innerHTML = ''
  })

  it('expone la ruta /perfil en Configuración sin restricción de permiso para roles autenticados', () => {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [{ path: '/', component: Empty, children: [...profileRoutes] }],
    })

    const auditorPermissions = ['dashboard.view', 'audit.read', 'reports.read']
    const menu = buildMenu(router.getRoutes(), (perm) => auditorPermissions.includes(perm))

    expect(profileRoutes[0]?.meta?.permission).toBeUndefined()
    expect(menu).toEqual([
      {
        group: 'Configuración',
        items: [expect.objectContaining({ routeName: 'profile', label: 'Mi perfil' })],
      },
    ])
  })

  it('renderiza identidad, mfaEnabled: false y todos los permisos efectivos de GET /api/me', async () => {
    const permissions = ['dashboard.view', 'audit.read', 'approvals.read', 'custom.future.permission']
    const writeTextMock = vi.fn<(text: string) => Promise<void>>().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: writeTextMock },
      configurable: true,
    })

    setupApi({
      baseURL: '/api',
      session: {
        getAccessToken: () => 'acc-aud',
        refresh: async () => false,
        onSessionExpired: vi.fn<() => void>(),
      },
      adapter: (config) => {
        if (config.url === '/me') {
          return ok(config, {
            id: '550e8400-e29b-41d4-a716-446655440099',
            userId: '550e8400-e29b-41d4-a716-446655440099',
            fullName: 'Auditor Prueba',
            email: 'auditor@sistema.local',
            role: 'ROLE_AUDITOR',
            permissions,
            mfaEnabled: false,
          })
        }
        return fail(config, 404, {})
      },
    })

    const { wrapper } = await mountProfilePage()
    const text = wrapper.text()

    expect(text).toContain('Auditor Prueba')
    expect(text).toContain('auditor@sistema.local')
    expect(text).toContain('ROLE_AUDITOR')
    expect(text).toContain('Auditor interno')
    expect(text).toContain('550e8400-e29b-41d4-a716-446655440099')
    expect(text).toContain('MFA no configurado')
    expect(wrapper.find('[data-testid="permissions-count"]').text()).toBe('4 permisos')

    for (const perm of permissions) {
      expect(text).toContain(perm)
    }
    expect(text).toContain('Consultar bitácora de auditoría')

    const copyBtn = wrapper.findAll('button').find((button) => button.text().includes('Copiar ID'))
    expect(copyBtn).toBeDefined()
    await copyBtn!.trigger('click')
    await flushPromises()
    expect(writeTextMock).toHaveBeenCalledWith('550e8400-e29b-41d4-a716-446655440099')
  })

  it('al hacer clic en "Actualizar" re-consulta GET /api/me y actualiza la vista', async () => {
    let meCalls = 0
    setupApi({
      baseURL: '/api',
      session: {
        getAccessToken: () => 'acc-aud',
        refresh: async () => false,
        onSessionExpired: vi.fn<() => void>(),
      },
      adapter: (config) => {
        if (config.url === '/me') {
          meCalls += 1
          return ok(config, {
            id: 'user-1',
            userId: 'user-1',
            fullName: meCalls === 1 ? 'Auditor Prueba' : 'Auditor Renovado',
            email: 'auditor@sistema.local',
            role: 'ROLE_AUDITOR',
            permissions: meCalls === 1 ? ['dashboard.view'] : ['dashboard.view', 'audit.read'],
            mfaEnabled: meCalls > 1,
          })
        }
        return fail(config, 404, {})
      },
    })

    const { wrapper } = await mountProfilePage()
    expect(meCalls).toBe(1)
    expect(wrapper.text()).toContain('Auditor Prueba')
    expect(wrapper.text()).toContain('MFA no configurado')

    const refreshBtn = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Actualizar'))
    expect(refreshBtn).toBeDefined()

    await refreshBtn!.trigger('click')
    await flushPromises()

    expect(meCalls).toBe(2)
    expect(wrapper.text()).toContain('Auditor Renovado')
    expect(wrapper.text()).toContain('MFA TOTP activo')
    expect(wrapper.text()).toContain('audit.read')
  })

  it('confirma "Cerrar todas las sesiones", llama POST /api/auth/logout con {} y redirige a login', async () => {
    sessionStorage.setItem('prestamos.backoffice.refreshToken', 'ref-tab-1')
    const logoutPayloads: Array<Record<string, unknown>> = []

    setupApi({
      baseURL: '/api',
      session: {
        getAccessToken: () => 'acc-aud',
        refresh: async () => false,
        onSessionExpired: vi.fn<() => void>(),
      },
      adapter: (config) => {
        if (config.url === '/me') {
          return ok(config, {
            id: 'user-aud',
            userId: 'user-aud',
            fullName: 'Auditor Prueba',
            email: 'auditor@sistema.local',
            role: 'ROLE_AUDITOR',
            permissions: ['dashboard.view', 'audit.read'],
            mfaEnabled: false,
          })
        }
        if (config.url === '/auth/logout') {
          logoutPayloads.push(JSON.parse(config.data as string) as Record<string, unknown>)
          return ok(config, null)
        }
        return fail(config, 404, {})
      },
    })

    const { wrapper, router, session } = await mountProfilePage()
    expect(wrapper.text()).toContain('Sesión activa en esta pestaña')

    const triggerBtn = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Cerrar todas las sesiones'))
    expect(triggerBtn).toBeDefined()

    await triggerBtn!.trigger('click')
    expect(wrapper.find('[role="alertdialog"]').exists()).toBe(true)
    expect(logoutPayloads).toHaveLength(0)

    const confirmBtn = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Confirmar cierre de todas las sesiones'))
    expect(confirmBtn).toBeDefined()

    await confirmBtn!.trigger('click')
    await flushPromises()

    expect(logoutPayloads).toEqual([{}])
    expect(Object.prototype.hasOwnProperty.call(logoutPayloads[0], 'refreshToken')).toBe(false)
    expect(session.isAuthenticated).toBe(false)
    expect(sessionStorage.getItem('prestamos.backoffice.refreshToken')).toBeNull()
    expect(router.currentRoute.value.name).toBe('login')
  })

  it('al hacer clic en "Cerrar sesión actual" envía { refreshToken } y redirige a login', async () => {
    sessionStorage.setItem('prestamos.backoffice.refreshToken', 'ref-tab-current')
    const logoutPayloads: Array<Record<string, unknown>> = []

    setupApi({
      baseURL: '/api',
      session: {
        getAccessToken: () => 'acc-aud',
        refresh: async () => false,
        onSessionExpired: vi.fn<() => void>(),
      },
      adapter: (config) => {
        if (config.url === '/me') {
          return ok(config, {
            id: 'user-aud',
            userId: 'user-aud',
            fullName: 'Auditor Prueba',
            email: 'auditor@sistema.local',
            role: 'ROLE_AUDITOR',
            permissions: ['dashboard.view'],
            mfaEnabled: false,
          })
        }
        if (config.url === '/auth/logout') {
          logoutPayloads.push(JSON.parse(config.data as string) as Record<string, unknown>)
          return ok(config, null)
        }
        return fail(config, 404, {})
      },
    })

    const { wrapper, router, session } = await mountProfilePage()
    const currentBtn = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Cerrar sesión actual'))
    expect(currentBtn).toBeDefined()

    await currentBtn!.trigger('click')
    await flushPromises()

    expect(logoutPayloads).toEqual([{ refreshToken: 'ref-tab-current' }])
    expect(session.isAuthenticated).toBe(false)
    expect(router.currentRoute.value.name).toBe('login')
  })

  it('permite elegir Claro, Oscuro y Sistema desde la tarjeta Apariencia sincronizada con useTheme', async () => {
    setupApi({
      baseURL: '/api',
      session: {
        getAccessToken: () => 'acc-aud',
        refresh: async () => false,
        onSessionExpired: vi.fn<() => void>(),
      },
      adapter: (config) => {
        if (config.url === '/me') {
          return ok(config, {
            id: 'user-aud',
            userId: 'user-aud',
            fullName: 'Auditor Prueba',
            email: 'auditor@sistema.local',
            role: 'ROLE_AUDITOR',
            permissions: ['dashboard.view'],
            mfaEnabled: true,
          })
        }
        return fail(config, 404, {})
      },
    })

    const { wrapper } = await mountProfilePage()
    const { themeMode, resolvedTheme, setTheme } = useTheme()

    const darkRadio = wrapper
      .findAll('[role="radio"]')
      .find((btn) => btn.text().includes('Oscuro'))
    const lightRadio = wrapper
      .findAll('[role="radio"]')
      .find((btn) => btn.text().includes('Claro'))
    const systemRadio = wrapper
      .findAll('[role="radio"]')
      .find((btn) => btn.text().includes('Sistema'))

    expect(darkRadio).toBeDefined()
    expect(lightRadio).toBeDefined()
    expect(systemRadio).toBeDefined()

    await darkRadio!.trigger('click')
    expect(themeMode.value).toBe('dark')
    expect(resolvedTheme.value).toBe('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(darkRadio!.attributes('aria-checked')).toBe('true')
    expect(wrapper.find('[data-testid="active-theme-badge"]').text()).toBe('Tema oscuro activo')

    await lightRadio!.trigger('click')
    expect(themeMode.value).toBe('light')
    expect(resolvedTheme.value).toBe('light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(lightRadio!.attributes('aria-checked')).toBe('true')

    await systemRadio!.trigger('click')
    expect(themeMode.value).toBe('system')
    expect(systemRadio!.attributes('aria-checked')).toBe('true')
    expect(wrapper.text()).toContain('Modo automático activo')

    // Cambio externo (como ThemeToggle) se refleja reactivamente en ProfilePage
    setTheme('dark')
    await flushPromises()
    expect(darkRadio!.attributes('aria-checked')).toBe('true')
  })
})
