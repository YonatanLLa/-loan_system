import '@prestamos/ui/styles.css'
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
      session.clear()
    },
  },
})

app.use(router)
app.mount('#app')
