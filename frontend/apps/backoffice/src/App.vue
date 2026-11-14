<script setup lang="ts">
import { Toaster } from '@prestamos/ui/components/ui/sonner'

import { useTheme } from './app/composables/useTheme'

const { resolvedTheme } = useTheme()
</script>

<template>
  <RouterView />
  <Toaster :theme="resolvedTheme" rich-colors close-button position="top-right" />
</template>
