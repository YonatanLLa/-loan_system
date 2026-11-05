/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Ruta base de la API (en desarrollo, Vite la redirige al backend con un proxy). */
  readonly VITE_API_BASE_URL: string
  /** 'true' habilita el ingreso demo sin backend (despliegue de prueba). */
  readonly VITE_DEMO_LOGIN?: string
  /** Backend al que apunta el proxy de desarrollo (por defecto http://localhost:5000). */
  readonly VITE_API_PROXY_TARGET?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
