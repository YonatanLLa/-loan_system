<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { toast } from 'vue-sonner'
import { HandCoins, LoaderCircle, Lock } from '@lucide/vue'

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

import ThemeToggle from '@/app/components/ThemeToggle.vue'
import { safeRedirect } from '@/app/router/guards'
import { useSessionStore } from '@/app/stores/session'
import { loginSchema } from '../schemas/login'

// FE-BO-01 / FE-BO-02: Autenticación con flujo MFA condicional (TOTP / enrollamiento).
const route = useRoute()
const router = useRouter()
const session = useSessionStore()

const sessionExpired = computed(() => route.query.motivo === 'sesion-expirada')
const sessionCompromised = computed(() => route.query.motivo === 'sesion-comprometida')

/* ── Account locked state (HTTP 423) ── */
const accountLocked = ref<{ title: string; detail: string } | null>(null)

function resetLocked() {
  accountLocked.value = null
}

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
    const status = await session.login(values.email, values.password)
    if (status === 'MFA_REQUIRED' || status === 'MFA_ENROLLMENT_REQUIRED') {
      await router.replace({
        name: 'totp',
        query: route.query.redirect ? { redirect: String(route.query.redirect) } : undefined,
      })
      return
    }
    await goNext()
  } catch (error) {
    const apiError = toApiError(error)

    // HTTP 423 – cuenta bloqueada: mostrar pantalla dedicada
    if (apiError.status === 423) {
      accountLocked.value = {
        title: apiError.title,
        detail: apiError.detail,
      }
      return
    }

    // Para cualquier otro error (401, etc.) usar mensajes genéricos
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
  <main class="relative grid min-h-svh place-items-center bg-muted/40 p-4 pt-14 sm:pt-4">
    <div class="absolute top-4 right-4">
      <ThemeToggle variant="outline" />
    </div>
    <div class="w-full max-w-sm space-y-6">
      <div class="flex items-center justify-center gap-2 text-lg font-semibold">
        <span
          class="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground"
        >
          <HandCoins class="size-4" aria-hidden="true" />
        </span>
        Sistema de Préstamos
      </div>

      <Alert v-if="sessionCompromised && !accountLocked" variant="destructive">
        <AlertTitle>Sesión revocada por seguridad</AlertTitle>
        <AlertDescription>
          Detectamos reutilización de tu token de sesión. Por seguridad se cerraron tus sesiones
          activas; inicia sesión nuevamente.
        </AlertDescription>
      </Alert>

      <Alert v-if="sessionExpired && !sessionCompromised && !accountLocked">
        <AlertTitle>Tu sesión expiró</AlertTitle>
        <AlertDescription>Ingresa nuevamente para continuar.</AlertDescription>
      </Alert>

      <!-- ── Cuenta bloqueada (HTTP 423) ── -->
      <Card v-if="accountLocked">
        <CardHeader class="items-center text-center">
          <span
            class="mb-2 flex size-12 items-center justify-center rounded-full bg-destructive/10 text-destructive"
          >
            <Lock class="size-6" aria-hidden="true" />
          </span>
          <CardTitle>
            <h1>{{ accountLocked.title }}</h1>
          </CardTitle>
          <CardDescription>{{ accountLocked.detail }}</CardDescription>
        </CardHeader>
        <CardContent class="flex justify-center">
          <Button variant="outline" class="w-full" @click="resetLocked">
            Reintentar
          </Button>
        </CardContent>
      </Card>

      <!-- ── Formulario de login ── -->
      <Card v-else>
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

      <div v-if="session.demoEnabled && !accountLocked" class="space-y-2 text-center">
        <Button variant="outline" class="w-full" @click="enterDemo">Entrar en modo demo</Button>
        <p class="text-xs text-muted-foreground">
          Solo en desarrollo: recorre las pantallas sin backend.
        </p>
      </div>
    </div>
  </main>
</template>
