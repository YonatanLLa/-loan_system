import process from 'node:process'
import { defineConfig, devices } from '@playwright/test'

// Pruebas de extremo a extremo del backoffice (ARQUITECTURA_FRONTEND §12).
// Antes de la primera ejecución: pnpm exec playwright install chromium
const port = process.env.CI ? 4174 : 5174

export default defineConfig({
  testDir: './e2e',
  timeout: 30 * 1000,
  expect: { timeout: 5000 },
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: `http://localhost:${port}`,
    trace: 'on-first-retry',
    headless: !!process.env.CI,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: process.env.CI ? 'pnpm preview' : 'pnpm dev',
    port,
    reuseExistingServer: !process.env.CI,
  },
})
