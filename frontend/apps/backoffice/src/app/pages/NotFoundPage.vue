<script setup lang="ts">
import { FileText } from '@lucide/vue'
import { EmptyState } from '@prestamos/ui'
import { Button } from '@prestamos/ui/components/ui/button'
</script>

<template>
  <main class="grid min-h-svh place-items-center p-6">
    <EmptyState
      :icon="FileText"
      title="Página no encontrada"
      description="La dirección no existe o fue movida."
    >
      <Button as-child>
        <RouterLink to="/">Ir al inicio</RouterLink>
      </Button>
    </EmptyState>
  </main>
</template>
