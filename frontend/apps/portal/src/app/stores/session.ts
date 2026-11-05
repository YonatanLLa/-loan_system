import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import { useApi } from '../api'

export interface ClientUser {
  id: string
  fullName: string
  docNumber: string
  email: string
  kycStatus: string
}

interface TokenResponse {
  accessToken: string
}

export const useSessionStore = defineStore('session', () => {
  const accessToken = ref<string | null>(null)
  const user = ref<ClientUser | null>(null)
  const isDemo = ref(false)

  const isAuthenticated = computed(() => user.value !== null)

  function clear() {
    accessToken.value = null
    user.value = null
    isDemo.value = false
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
    clear,
    refreshAccessToken,
  }
})
