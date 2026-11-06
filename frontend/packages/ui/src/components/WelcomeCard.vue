<script setup lang="ts">
import { LoaderCircle } from '@lucide/vue'

import { Alert, AlertDescription, AlertTitle } from './ui/alert'
import { Button } from './ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from './ui/card'

// Tarjeta de bienvenida de prueba: muestra al usuario que devuelve la API.
defineProps<{
  appName: string
  user: { fullName: string; email: string; role: string } | null
  loading?: boolean
  error?: string | null
}>()

defineEmits<{ load: [] }>()
</script>

<template>
  <Card class="w-full max-w-md">
    <CardHeader>
      <CardTitle>
        <h2>Te damos la bienvenida a {{ appName }}</h2>
      </CardTitle>
      <CardDescription>Comprueba la conexión con el servidor.</CardDescription>
    </CardHeader>
    <CardContent class="space-y-4">
      <dl v-if="user" class="space-y-1 text-sm">
        <div class="flex justify-between gap-4">
          <dt class="text-muted-foreground">Nombre</dt>
          <dd class="font-medium">{{ user.fullName }}</dd>
        </div>
        <div class="flex justify-between gap-4">
          <dt class="text-muted-foreground">Correo</dt>
          <dd>{{ user.email }}</dd>
        </div>
        <div class="flex justify-between gap-4">
          <dt class="text-muted-foreground">Rol</dt>
          <dd>{{ user.role }}</dd>
        </div>
      </dl>
      <Alert v-if="error" variant="destructive">
        <AlertTitle>No se pudo cargar</AlertTitle>
        <AlertDescription>{{ error }}</AlertDescription>
      </Alert>
      <Button class="w-full" :disabled="loading" @click="$emit('load')">
        <LoaderCircle v-if="loading" class="animate-spin" aria-hidden="true" />
        Cargar mi usuario
      </Button>
    </CardContent>
  </Card>
</template>
