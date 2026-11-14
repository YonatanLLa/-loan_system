<script setup lang="ts">
import { computed } from 'vue'
import { Moon, Sun } from '@lucide/vue'

import { Button, type ButtonVariants } from '@prestamos/ui/components/ui/button'

import { useTheme } from '../composables/useTheme'

withDefaults(
  defineProps<{
    variant?: ButtonVariants['variant']
  }>(),
  {
    variant: 'ghost',
  },
)

const { isDark, toggleTheme } = useTheme()

const label = computed(() => (isDark.value ? 'Cambiar a modo claro' : 'Cambiar a modo oscuro'))
</script>

<template>
  <Button
    type="button"
    :variant="variant"
    size="icon-sm"
    :aria-label="label"
    :title="label"
    :aria-pressed="isDark"
    @click="toggleTheme"
  >
    <Sun v-if="isDark" class="size-4" aria-hidden="true" />
    <Moon v-else class="size-4" aria-hidden="true" />
    <span class="sr-only">{{ label }}</span>
  </Button>
</template>
