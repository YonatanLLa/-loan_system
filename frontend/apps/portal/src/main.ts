import '@prestamos/ui/styles.css'
import './assets/portal.css'
import 'vue-sonner/style.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './app/router'
import { setupApi } from './app/api'
import { useSessionStore } from './app/stores/session'

const app = createApp(App)
const pinia = createPinia()
app.use(pinia)

const session = useSessionStore(pinia)
setupApi({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  session: {
    getAccessToken: () => session.accessToken,
    refresh: () => session.refreshAccessToken(),
    onSessionExpired: () => {
      const reason = session.sessionTerminationReason
      session.clear()
      if (!router.currentRoute.value.meta.public) {
        void router.replace({
          path: '/login',
          query: {
            motivo: reason === 'reused' ? 'sesion-comprometida' : 'sesion-expirada',
          },
        })
      }
    },
  },
})

// API simulada (MSW): responde /api/* con datos semilla mientras no exista el backend.
// `VITE_USE_MOCKS` es false en producción, así que este bloque no llega al build.
if (import.meta.env.VITE_USE_MOCKS === 'true') {
  const { startMocks } = await import('@prestamos/mocks/browser')
  await startMocks()
}

app.use(router)
app.mount('#app')
