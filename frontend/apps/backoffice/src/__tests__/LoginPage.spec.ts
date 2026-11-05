import { beforeEach, describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { defineComponent } from 'vue'

import LoginPage from '../features/auth/pages/LoginPage.vue'

const Empty = defineComponent({ render: () => null })

// VeeValidate valida de forma asíncrona (esquema Zod + microtareas): se espera a que termine.
const settle = () => new Promise((resolve) => setTimeout(resolve, 20))

async function mountLogin() {
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
  return mount(LoginPage, { global: { plugins: [pinia, router] } })
}

describe('LoginPage', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('valida los campos antes de llamar a la API', async () => {
    const wrapper = await mountLogin()

    await wrapper.find('form').trigger('submit')
    await flushPromises()
    await settle()
    await flushPromises()

    expect(wrapper.text()).toContain('Ingresa un correo válido')
    expect(wrapper.text()).toContain('Ingresa tu contraseña')
  })

  it('marca el campo inválido para lectores de pantalla', async () => {
    const wrapper = await mountLogin()

    await wrapper.find('#email').setValue('no-es-correo')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    await settle()
    await flushPromises()

    expect(wrapper.find('#email').attributes('aria-invalid')).toBe('true')
  })

  it('muestra aviso cuando la sesión ha expirado', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/ingresar', name: 'login', component: LoginPage },
        { path: '/', name: 'dashboard', component: Empty },
      ],
    })
    await router.push('/ingresar?motivo=sesion-expirada')
    await router.isReady()
    const wrapper = mount(LoginPage, { global: { plugins: [pinia, router] } })

    expect(wrapper.text()).toContain('Tu sesión expiró')
  })
})
