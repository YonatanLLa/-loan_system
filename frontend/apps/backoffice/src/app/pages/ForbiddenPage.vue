<script setup lang="ts">
import { ShieldCheck } from '@lucide/vue'
import { EmptyState } from '@prestamos/ui'
import { Button } from '@prestamos/ui/components/ui/button'
</script>

<template>
  <EmptyState
    :icon="ShieldCheck"
    title="No tienes permiso para ver esta sección"
    description="Si necesitas acceso, pídelo a un administrador del sistema."
  >
    <Button as-child variant="outline">
      <RouterLink :to="{ name: 'dashboard' }">Volver al inicio</RouterLink>
    </Button>
  </EmptyState>
</template>
