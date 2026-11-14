import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { defineComponent } from 'vue'

import { type AxiosResponse, type InternalAxiosRequestConfig } from '@prestamos/api-client'

import { setupApi } from '@/app/api'
import LoginPage from '../features/auth/pages/LoginPage.vue'

const Empty = defineComponent({ render: () => null })

// VeeValidate valida de forma asíncrona (esquema Zod + microtareas): se espera a que termine.
const settle = () => new Promise((resolve) => setTimeout(resolve, 20))

async function mountLogin(path = '/ingresar') {
  const pinia = createPinia()
  setActivePinia(pinia)
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/ingresar', name: 'login', component: LoginPage },
      { path: '/verificacion-dos-pasos', name: 'totp', component: Empty },
      { path: '/', name: 'dashboard', component: Empty },
    ],
  })
  await router.push(path)
  await router.isReady()
  const wrapper = mount(LoginPage, { global: { plugins: [pinia, router] } })
  return { wrapper, router }
}

describe('LoginPage', () => {
  beforeEach(() => {
    sessionStorage.clear()
    document.body.innerHTML = ''
  })

  it('valida los campos antes de llamar a la API', async () => {
    const { wrapper } = await mountLogin()

    await wrapper.find('form').trigger('submit')
    await flushPromises()
    await settle()
    await flushPromises()

    expect(wrapper.text()).toContain('Ingresa un correo válido')
    expect(wrapper.text()).toContain('Ingresa tu contraseña')
  })

  it('marca el campo inválido para lectores de pantalla', async () => {
    const { wrapper } = await mountLogin()

    await wrapper.find('#email').setValue('no-es-correo')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    await settle()
    await flushPromises()

    expect(wrapper.find('#email').attributes('aria-invalid')).toBe('true')
  })

  it('muestra aviso cuando la sesión ha expirado o fue comprometida por reutilización', async () => {
    const { wrapper: expiredWrapper } = await mountLogin('/ingresar?motivo=sesion-expirada')
    expect(expiredWrapper.text()).toContain('Tu sesión expiró')

    const { wrapper: reusedWrapper } = await mountLogin('/ingresar?motivo=sesion-comprometida')
    expect(reusedWrapper.text()).toContain('Sesión revocada por seguridad')
  })

  it('redirige a /verificacion-dos-pasos cuando el backend responde MFA_REQUIRED', async () => {
    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => null, refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config: InternalAxiosRequestConfig): Promise<AxiosResponse> =>
        Promise.resolve({
          data: {
            status: 'MFA_REQUIRED',
            accessToken: null,
            refreshToken: null,
            expiresInSeconds: null,
            role: null,
            mfaToken: 'mfa-123',
            mfaExpiresInSeconds: 300,
          },
          status: 200,
          statusText: 'OK',
          headers: {},
          config,
        }),
    })

    const { wrapper, router } = await mountLogin('/ingresar?redirect=%2Faprobaciones')
    await wrapper.find('#email').setValue('admin@sistema.local')
    await wrapper.find('#password').setValue('Demo1234!')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    await settle()
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('totp')
    expect(router.currentRoute.value.query.redirect).toBe('/aprobaciones')
  })
})
