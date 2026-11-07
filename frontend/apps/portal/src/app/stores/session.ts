import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import type { Me, TokenResponse } from '@prestamos/api-client'

import { useApi } from '../api'

export interface ClientUser {
  id: string
  fullName: string
  docNumber: string
  email: string
  customerId?: string
  kycStatus: string
}

const INACTIVITY_TIMEOUT_MS = 15 * 60 * 1000 // 15 minutos de inactividad (FE-CP-02 / RNF-19)

export const useSessionStore = defineStore('session', () => {
  const accessToken = ref<string | null>(null)
  const user = ref<ClientUser | null>(null)
  const isDemo = ref(false)

  const isAuthenticated = computed(() => user.value !== null)

  let lastActivity = Date.now()
  let inactivityInterval: ReturnType<typeof setInterval> | null = null

  function markActivity() {
    lastActivity = Date.now()
  }

  function startInactivityWatch(onExpired?: () => void) {
    if (inactivityInterval) clearInterval(inactivityInterval)
    markActivity()

    // Escucha eventos de interacción (remueve primero para evitar duplicados)
    if (typeof window !== 'undefined') {
      window.removeEventListener('click', markActivity)
      window.removeEventListener('keydown', markActivity)
      window.removeEventListener('touchstart', markActivity)
      window.addEventListener('click', markActivity, { passive: true })
      window.addEventListener('keydown', markActivity, { passive: true })
      window.addEventListener('touchstart', markActivity, { passive: true })
    }

    inactivityInterval = setInterval(() => {
      if (isAuthenticated.value && Date.now() - lastActivity > INACTIVITY_TIMEOUT_MS) {
        clear()
        if (onExpired) onExpired()
      }
    }, 15000)
  }

  function stopInactivityWatch() {
    if (inactivityInterval) {
      clearInterval(inactivityInterval)
      inactivityInterval = null
    }
    if (typeof window !== 'undefined') {
      window.removeEventListener('click', markActivity)
      window.removeEventListener('keydown', markActivity)
      window.removeEventListener('touchstart', markActivity)
    }
  }

  function setSession(token: string) {
    accessToken.value = token
    markActivity()
  }

  function clear() {
    accessToken.value = null
    user.value = null
    isDemo.value = false
    stopInactivityWatch()
  }

  async function loadProfile(): Promise<ClientUser | null> {
    try {
      const { data } = await useApi().get<Me>('/me')
      user.value = {
        id: data.id,
        fullName: data.fullName,
        docNumber: '',
        email: data.email,
        customerId: data.customerId,
        kycStatus: data.kycStatus || 'PENDING',
      }
      return user.value
    } catch {
      return null
    }
  }

  function updateKycStatus(status: string) {
    if (user.value) {
      user.value.kycStatus = status
    }
  }

  async function refreshAccessToken(): Promise<boolean> {
    if (isDemo.value) return true
    try {
      const { data } = await useApi().post<TokenResponse>('/auth/refresh', null, {
        skipAuthRefresh: true,
      })
      accessToken.value = data.accessToken
      return true
    } catch {
      clear()
      return false
    }
  }

  return {
    accessToken,
    user,
    isDemo,
    isAuthenticated,
    setSession,
    clear,
    loadProfile,
    updateKycStatus,
    refreshAccessToken,
    startInactivityWatch,
    stopInactivityWatch,
    markActivity,
  }
})
