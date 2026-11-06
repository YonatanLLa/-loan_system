<script setup lang="ts">
import type { Component } from 'vue'

defineProps<{
  title: string
  description?: string
  icon?: Component
}>()
</script>

<template>
  <div
    class="flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed p-10 text-center"
  >
    <div v-if="icon" class="rounded-full bg-muted p-3 text-muted-foreground">
      <component :is="icon" class="size-6" aria-hidden="true" />
    </div>
    <div class="space-y-1">
      <p class="font-medium">{{ title }}</p>
      <p v-if="description" class="max-w-md text-sm text-muted-foreground">{{ description }}</p>
    </div>
    <slot />
  </div>
</template>
