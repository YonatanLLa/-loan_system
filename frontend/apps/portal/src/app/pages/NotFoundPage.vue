<script setup lang="ts">
import { FileQuestion } from '@lucide/vue'
import { EmptyState } from '@prestamos/ui'
import { Button } from '@prestamos/ui/components/ui/button'
</script>

<template>
  <main class="grid min-h-[60vh] place-items-center p-6">
    <EmptyState
      :icon="FileQuestion"
      title="Página no encontrada"
      description="La ruta que buscas no existe o fue movida."
    >
      <Button as-child class="mt-4">
        <RouterLink to="/">Ir al inicio</RouterLink>
      </Button>
    </EmptyState>
  </main>
</template>
