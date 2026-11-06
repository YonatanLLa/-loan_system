<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink } from 'vue-router'
import { AlertCircle, ArrowRight, LoaderCircle, Lock, Smartphone, UserCheck } from '@lucide/vue'

import { Alert, AlertDescription } from '@prestamos/ui/components/ui/alert'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Input } from '@prestamos/ui/components/ui/input'
import { Label } from '@prestamos/ui/components/ui/label'

import { useClientAuth } from '../composables'

// FE-CP-02: Login del cliente prestatario con email o celular y contraseña.

const { loginIdentifier, loginPassword, loginLoading, loginError, login } = useClientAuth()

function fillDemoClient() {
  loginIdentifier.value = 'cliente@correo.pe'
  loginPassword.value = 'Demo1234!'
}
</script>

<template>
  <div class="min-h-[80vh] flex flex-col justify-center py-6">
    <Card class="w-full max-w-sm mx-auto shadow-xl border-border">
      <CardHeader class="text-center pb-4">
        <div class="mx-auto size-12 rounded-2xl bg-primary/10 flex items-center justify-center text-primary mb-2">
          <Smartphone class="size-6" />
        </div>
        <CardTitle class="text-xl font-bold">Portal Prestatario</CardTitle>
        <CardDescription class="text-xs">
          Ingresa a tu cuenta para solicitar préstamos, ver cuotas y pagar en línea.
        </CardDescription>
      </CardHeader>

      <CardContent class="space-y-4">
        <Alert v-if="loginError" variant="destructive" class="py-2.5">
          <AlertCircle class="size-4" />
          <AlertDescription class="text-xs">{{ loginError }}</AlertDescription>
        </Alert>

        <form class="space-y-3.5" @submit.prevent="login">
          <div class="space-y-1.5">
            <Label for="loginEmail" class="text-xs font-medium">Correo o Celular</Label>
            <Input
              id="loginEmail"
              v-model="loginIdentifier"
              type="text"
              placeholder="ejemplo@correo.pe o 987654321"
              required
              class="h-10 text-xs"
            />
          </div>

          <div class="space-y-1.5">
            <div class="flex items-center justify-between">
              <Label for="loginPass" class="text-xs font-medium">Contraseña</Label>
            </div>
            <Input
              id="loginPass"
              v-model="loginPassword"
              type="password"
              placeholder="••••••••"
              required
              class="h-10 text-xs"
            />
          </div>

          <Button type="submit" class="w-full h-10 mt-2 font-semibold text-xs" :disabled="loginLoading">
            <LoaderCircle v-if="loginLoading" class="mr-2 size-4 animate-spin" />
            <span>Ingresar</span>
            <ArrowRight v-if="!loginLoading" class="ml-2 size-4" />
          </Button>
        </form>

        <!-- Botón Demo rápido -->
        <div class="pt-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            class="w-full text-xs text-muted-foreground border-dashed"
            @click="fillDemoClient"
          >
            <UserCheck class="mr-1.5 size-3.5" />
            Llenar con cliente demo (María Quispe)
          </Button>
        </div>

        <div class="text-center pt-3 border-t text-xs text-muted-foreground">
          ¿No tienes una cuenta aún?
          <RouterLink to="/registro" class="text-primary font-semibold hover:underline block mt-1">
            Crea tu cuenta aquí (FE-CP-01)
          </RouterLink>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
