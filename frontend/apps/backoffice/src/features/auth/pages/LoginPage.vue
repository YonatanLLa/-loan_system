<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { toast } from 'vue-sonner'
import { HandCoins, LoaderCircle } from '@lucide/vue'

import { toApiError, toFormErrors } from '@prestamos/api-client'
import { Alert, AlertDescription, AlertTitle } from '@prestamos/ui/components/ui/alert'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Field, FieldError, FieldGroup, FieldLabel } from '@prestamos/ui/components/ui/field'
import { Input } from '@prestamos/ui/components/ui/input'

import { safeRedirect } from '@/app/router/guards'
import { useSessionStore } from '@/app/stores/session'
import { loginSchema } from '../schemas/login'

// FE-BO-01. El segundo factor (FE-BO-02) se agrega cuando el backend defina su contrato.
const route = useRoute()
const router = useRouter()
const session = useSessionStore()

const sessionExpired = computed(() => route.query.motivo === 'sesion-expirada')

const { defineField, errors, handleSubmit, isSubmitting, setErrors } = useForm({
  validationSchema: toTypedSchema(loginSchema),
  // Sin valores iniciales los campos son undefined y Zod responde "Required" en inglés.
  initialValues: { email: '', password: '' },
})
const [email, emailAttrs] = defineField('email')
const [password, passwordAttrs] = defineField('password')

function goNext() {
  return router.replace(safeRedirect(route.query.redirect, '/'))
}

const onSubmit = handleSubmit(async (values) => {
  try {
    await session.login(values.email, values.password)
    await goNext()
  } catch (error) {
    const apiError = toApiError(error)
    setErrors(toFormErrors(apiError))
    toast.error(apiError.title, { description: apiError.detail })
  }
})

function enterDemo() {
  session.loginDemo()
  void goNext()
}
</script>

<template>
  <main class="grid min-h-svh place-items-center bg-muted/40 p-4">
    <div class="w-full max-w-sm space-y-6">
      <div class="flex items-center justify-center gap-2 text-lg font-semibold">
        <span
          class="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground"
        >
          <HandCoins class="size-4" aria-hidden="true" />
        </span>
        Sistema de Préstamos
      </div>

      <Alert v-if="sessionExpired">
        <AlertTitle>Tu sesión expiró</AlertTitle>
        <AlertDescription>Ingresa nuevamente para continuar.</AlertDescription>
      </Alert>

      <Card>
        <CardHeader>
          <CardTitle>
            <h1>Iniciar sesión</h1>
          </CardTitle>
          <CardDescription>Panel interno para el equipo de la fintech.</CardDescription>
        </CardHeader>
        <CardContent>
          <form novalidate @submit="onSubmit">
            <FieldGroup>
              <Field :data-invalid="!!errors.email || undefined">
                <FieldLabel for="email">Correo corporativo</FieldLabel>
                <Input
                  id="email"
                  v-model="email"
                  v-bind="emailAttrs"
                  type="email"
                  autocomplete="username"
                  :aria-invalid="!!errors.email"
                />
                <FieldError :errors="[errors.email]" />
              </Field>
              <Field :data-invalid="!!errors.password || undefined">
                <FieldLabel for="password">Contraseña</FieldLabel>
                <Input
                  id="password"
                  v-model="password"
                  v-bind="passwordAttrs"
                  type="password"
                  autocomplete="current-password"
                  :aria-invalid="!!errors.password"
                />
                <FieldError :errors="[errors.password]" />
              </Field>
              <Button type="submit" class="w-full" :disabled="isSubmitting">
                <LoaderCircle v-if="isSubmitting" class="animate-spin" aria-hidden="true" />
                Ingresar
              </Button>
            </FieldGroup>
          </form>
        </CardContent>
      </Card>

      <div v-if="session.demoEnabled" class="space-y-2 text-center">
        <Button variant="outline" class="w-full" @click="enterDemo">Entrar en modo demo</Button>
        <p class="text-xs text-muted-foreground">
          Solo en desarrollo: recorre las pantallas sin backend.
        </p>
      </div>
    </div>
  </main>
</template>
