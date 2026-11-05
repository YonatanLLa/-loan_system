import { globalIgnores } from 'eslint/config'
import { defineConfigWithVueTs, vueTsConfigs } from '@vue/eslint-config-typescript'
import pluginVue from 'eslint-plugin-vue'
import pluginPlaywright from 'eslint-plugin-playwright'
import pluginVitest from '@vitest/eslint-plugin'
import pluginOxlint from 'eslint-plugin-oxlint'
import skipFormatting from 'eslint-config-prettier/flat'

// Configuración única para todo el monorepo (apps/* y packages/*).
export default defineConfigWithVueTs(
  {
    name: 'app/files-to-lint',
    files: ['**/*.{vue,ts,mts,tsx}'],
  },

  globalIgnores([
    '**/dist/**',
    '**/dist-ssr/**',
    '**/coverage/**',
    '**/playwright-report/**',
    '**/test-results/**',
    'packages/api-client/src/generated/**',
    // Componentes vendorizados de shadcn-vue: se regeneran con el CLI, no se lintan.
    'packages/ui/src/components/ui/**',
  ]),

  ...pluginVue.configs['flat/essential'],
  vueTsConfigs.recommended,

  {
    ...pluginPlaywright.configs['flat/recommended'],
    files: ['apps/*/e2e/**/*.{test,spec}.{js,ts,jsx,tsx}'],
  },

  {
    ...pluginVitest.configs.recommended,
    files: ['**/src/**/__tests__/*'],
  },

  {
    name: 'app/reglas-del-proyecto',
    rules: {
      // Nada de HTML crudo con contenido del usuario o de la API (ARQUITECTURA_FRONTEND §11).
      'vue/no-v-html': 'error',
      // Una feature solo se importa por su index.ts (ARQUITECTURA_FRONTEND §4.3).
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['@/features/*/*'],
              message:
                'Importa solo desde el index.ts de la feature (@/features/<nombre>), no desde sus archivos internos.',
            },
          ],
        },
      ],
    },
  },

  ...pluginOxlint.buildFromOxlintConfigFile('.oxlintrc.json'),

  skipFormatting,
)
