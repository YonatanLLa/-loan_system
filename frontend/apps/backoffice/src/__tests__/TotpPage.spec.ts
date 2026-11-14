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
import { useSessionStore } from '@/app/stores/session'
import TotpPage from '@/features/auth/pages/TotpPage.vue'

const Empty = defineComponent({ render: () => null })

function ok(config: InternalAxiosRequestConfig, data: unknown): Promise<AxiosResponse> {
  return Promise.resolve({ data, status: 200, statusText: 'OK', headers: {}, config })
}

function fail(config: InternalAxiosRequestConfig, status: number, data: unknown): Promise<never> {
  const response = { data, status, statusText: 'Error', headers: {}, config } as AxiosResponse
  return Promise.reject(new AxiosError(`HTTP ${status}`, String(status), config, null, response))
}

async function mountTotp() {
  const pinia = createPinia()
  setActivePinia(pinia)
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/verificacion-dos-pasos', name: 'totp', component: TotpPage },
      { path: '/ingresar', name: 'login', component: Empty },
      { path: '/', name: 'dashboard', component: Empty },
    ],
  })
  await router.push('/verificacion-dos-pasos')
  await router.isReady()
  const wrapper = mount(TotpPage, { global: { plugins: [pinia, router] } })
  return { wrapper, router }
}

describe('TotpPage (MFA verify & enrollment)', () => {
  beforeEach(() => {
    sessionStorage.clear()
    document.body.innerHTML = ''
  })

  it('muestra aviso cuando no hay desafío MFA activo o ya expiró', async () => {
    const { wrapper } = await mountTotp()
    expect(wrapper.text()).toContain('Desafío de verificación no disponible')
    expect(wrapper.text()).toContain('Volver a iniciar sesión')
  })

  it('permite verificar con código TOTP y alternar a código de recuperación en MFA_REQUIRED', async () => {
    sessionStorage.setItem(
      'prestamos.backoffice.mfaChallenge',
      JSON.stringify({
        mode: 'verify',
        mfaToken: 'mfa-verify-tok',
        expiresInSeconds: 300,
        expiresAt: Date.now() + 180_000,
        email: 'admin@sistema.local',
      }),
    )

    const verifyBodies: unknown[] = []
    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => null, refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        if (config.url === '/auth/mfa/verify') {
          verifyBodies.push(JSON.parse(config.data as string))
          return ok(config, {
            accessToken: 'acc-mfa',
            refreshToken: 'ref-mfa',
            expiresInSeconds: 900,
            role: 'ROLE_ADMIN',
          })
        }
        if (config.url === '/me') {
          return ok(config, {
            id: 'u1',
            fullName: 'Admin',
            email: 'admin@sistema.local',
            role: 'ROLE_ADMIN',
            permissions: ['dashboard.view'],
            mfaEnabled: true,
          })
        }
        return fail(config, 404, {})
      },
    })

    const { wrapper, router } = await mountTotp()
    expect(wrapper.text()).toContain('Verificación en dos pasos')

    // Cambiar a recovery code y enviar
    const toggleBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Usar un código de recuperación'))
    await toggleBtn?.trigger('click')
    await wrapper.find('#mfa-recovery-code').setValue('ABCD-EFGH-IJKL')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(verifyBodies).toEqual([
      { mfaToken: 'mfa-verify-tok', recoveryCode: 'ABCD-EFGH-IJKL' },
    ])
    expect(router.currentRoute.value.name).toBe('dashboard')
  })

  it('muestra secreto, URI otpauth, confirma código y despliega recovery codes en MFA_ENROLLMENT_REQUIRED', async () => {
    sessionStorage.setItem(
      'prestamos.backoffice.mfaChallenge',
      JSON.stringify({
        mode: 'enroll',
        mfaToken: 'mfa-enroll-tok',
        expiresInSeconds: 300,
        expiresAt: Date.now() + 180_000,
        email: 'admin@sistema.local',
      }),
    )

    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => null, refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        if (config.url === '/auth/mfa/enroll') {
          return ok(config, {
            secret: 'NB2W45DFOIZA====',
            otpauthUri: 'otpauth://totp/LoanSystem:admin@sistema.local?secret=NB2W45DFOIZA====',
          })
        }
        if (config.url === '/auth/mfa/enroll/confirm') {
          return ok(config, {
            tokens: {
              accessToken: 'acc-2fa',
              refreshToken: 'ref-2fa',
              expiresInSeconds: 900,
              role: 'ROLE_ADMIN',
            },
            recoveryCodes: ['CODE-1111', 'CODE-2222'],
          })
        }
        if (config.url === '/me') {
          return ok(config, {
            id: 'u1',
            fullName: 'Admin',
            email: 'admin@sistema.local',
            role: 'ROLE_ADMIN',
            permissions: ['dashboard.view'],
            mfaEnabled: true,
          })
        }
        return fail(config, 404, {})
      },
    })

    const { wrapper, router } = await mountTotp()
    await flushPromises()

    expect(wrapper.text()).toContain('NB2W45DFOIZA====')
    await wrapper.find('#mfa-enroll-code').setValue('654321')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Guarda tus códigos de recuperación')
    expect(wrapper.text()).toContain('CODE-1111')
    expect(wrapper.text()).toContain('CODE-2222')

    const session = useSessionStore()
    expect(session.isAuthenticated).toBe(true)

    const continueBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Continuar al panel'))
    await continueBtn?.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('dashboard')
  })
})
