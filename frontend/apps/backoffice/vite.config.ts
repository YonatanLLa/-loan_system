import { fileURLToPath, URL } from 'node:url'
import process from 'node:process'

import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import vueDevTools from 'vite-plugin-vue-devtools'
import { msw } from 'msw/vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  return {
    // msw() sirve /mockServiceWorker.js para la API simulada (no se incluye en el build de producción).
    plugins: [vue(), tailwindcss(), vueDevTools(), ...(mode === 'production' ? [] : [msw({ mode: 'worker-only' })])],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
        // Los componentes shadcn-vue de packages/ui se importan entre sí con este nombre.
        '@prestamos/ui': fileURLToPath(new URL('../../packages/ui/src', import.meta.url)),
      },
      dedupe: ['vue', 'vue-router', 'pinia'],
    },
    server: {
      port: 5173,
      strictPort: true,
      proxy: {
        '/api': {
          target: env.VITE_API_PROXY_TARGET || 'http://localhost:5000',
          changeOrigin: true,
        },
      },
    },
    preview: {
      port: 4173,
      strictPort: true,
    },
  }
})
