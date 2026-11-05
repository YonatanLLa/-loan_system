<script setup lang="ts">
import { Toaster } from '@prestamos/ui/components/ui/sonner'
</script>

<template>
  <RouterView />
  <Toaster rich-colors close-button position="top-right" />
</template>
