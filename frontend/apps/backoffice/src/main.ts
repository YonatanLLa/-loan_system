import '@prestamos/ui/styles.css'
import 'vue-sonner/style.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import { setupApi } from './app/api'
import router from './app/router'
import { useSessionStore } from './app/stores/session'

const app = createApp(App)
const pinia = createPinia()
app.use(pinia)

const session = useSessionStore(pinia)
setupApi({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  session: {
    getAccessToken: () => session.accessToken,
    refresh: () => session.refreshAccessToken(),
    onSessionExpired: () => {
      session.clear()
      void router.replace({ name: 'login', query: { motivo: 'sesion-expirada' } })
    },
  },
})

app.use(router)
app.mount('#app')
