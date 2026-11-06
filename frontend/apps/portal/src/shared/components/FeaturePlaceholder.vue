<script setup lang="ts">
import { Construction } from '@lucide/vue'
import { EmptyState, PageHeader } from '@prestamos/ui'

// Pantalla provisional de una feature aún no implementada. Al construir la feature,
// su routes.ts apunta a la página real en features/<nombre>/pages/.
defineProps<{
  title: string
  description: string
  /** IDs de docs/REQUERIMIENTOS_FRONTEND.md que cubrirá esta sección. */
  requirements: string
}>()
</script>

<template>
  <div class="space-y-6">
    <PageHeader :title="title" :description="description" />
    <EmptyState
      :icon="Construction"
      title="Sección en construcción"
      :description="`Cubrirá ${requirements} de docs/REQUERIMIENTOS_FRONTEND.md.`"
    />
  </div>
</template>
