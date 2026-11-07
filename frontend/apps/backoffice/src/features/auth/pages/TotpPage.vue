<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { toast } from 'vue-sonner'
import { HandCoins, KeyRound, LoaderCircle } from '@lucide/vue'

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

// FE-BO-02. Placeholder: se conecta al backend cuando defina el contrato TOTP.

const router = useRouter()

const TOTP_EXPIRY_SECONDS = 30
const CODE_LENGTH = 6

const code = ref('')
const isVerifying = ref(false)
const codeError = ref<string | undefined>()
const remainingSeconds = ref(TOTP_EXPIRY_SECONDS)

let timer: ReturnType<typeof setInterval> | null = null

const formattedTime = computed(() => {
  const mins = Math.floor(remainingSeconds.value / 60)
  const secs = remainingSeconds.value % 60
  return `${String(mins).padStart(2, '0')}:${String(secs).padStart(2, '0')}`
})

const isCodeComplete = computed(() => code.value.replace(/\s/g, '').length === CODE_LENGTH)

function startTimer() {
  remainingSeconds.value = TOTP_EXPIRY_SECONDS
  timer = setInterval(() => {
    if (remainingSeconds.value > 0) {
      remainingSeconds.value--
    } else {
      resetTimer()
    }
  }, 1000)
}

function resetTimer() {
  if (timer) {
    clearInterval(timer)
    timer = null
  }
  remainingSeconds.value = TOTP_EXPIRY_SECONDS
}

function onInput(event: Event) {
  const target = event.target as HTMLInputElement
  // Solo permitir dígitos
  target.value = target.value.replace(/\D/g, '').slice(0, CODE_LENGTH)
  code.value = target.value
  codeError.value = undefined
}

async function onSubmit(event: Event) {
  event.preventDefault()

  if (!isCodeComplete.value) {
    codeError.value = `Ingresa los ${CODE_LENGTH} dígitos`
    return
  }

  isVerifying.value = true
  codeError.value = undefined

  // TODO: FE-BO-02 – Conectar al endpoint TOTP del backend cuando esté definido.
  // Por ahora simulamos un delay y mostramos mensaje informativo.
  await new Promise((resolve) => setTimeout(resolve, 1000))

  toast.info('Verificación TOTP pendiente', {
    description: 'El endpoint de verificación TOTP aún no está implementado en el backend.',
  })

  isVerifying.value = false
}

function goBack() {
  void router.replace({ name: 'login' })
}

onMounted(() => {
  startTimer()
})

onUnmounted(() => {
  resetTimer()
})
</script>

<template>
  <main class="grid min-h-svh place-items-center bg-muted/40 p-4">
    <div class="w-full max-w-sm space-y-6">
      <div class="flex items-center justify-center gap-2 text-lg font-semibold">
        <span class="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
          <HandCoins class="size-4" aria-hidden="true" />
        </span>
        Sistema de Préstamos
      </div>

      <Alert>
        <AlertTitle>Placeholder – FE-BO-02</AlertTitle>
        <AlertDescription>
          Esta pantalla se conectará al backend cuando se defina el endpoint de verificación TOTP.
        </AlertDescription>
      </Alert>

      <Card>
        <CardHeader class="items-center text-center">
          <span class="mb-2 flex size-12 items-center justify-center rounded-full bg-primary/10 text-primary">
            <KeyRound class="size-6" aria-hidden="true" />
          </span>
          <CardTitle>
            <h1>Verificación en dos pasos</h1>
          </CardTitle>
          <CardDescription>
            Ingresa el código de {{ CODE_LENGTH }} dígitos de tu aplicación autenticadora.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form novalidate @submit="onSubmit">
            <FieldGroup>
              <Field :data-invalid="!!codeError || undefined">
                <FieldLabel for="totp-code">Código TOTP</FieldLabel>
                <Input id="totp-code" :value="code" type="text" inputmode="numeric" autocomplete="one-time-code"
                  :maxlength="CODE_LENGTH" placeholder="000000" class="text-center text-lg tracking-[0.3em]"
                  :aria-invalid="!!codeError" @input="onInput" />
                <FieldError :errors="[codeError]" />
              </Field>

              <div class="flex items-center justify-center gap-2 text-sm text-muted-foreground">
                <span>El código expira en</span>
                <span class="font-mono font-semibold"
                  :class="remainingSeconds <= 5 ? 'text-destructive' : 'text-foreground'">
                  {{ formattedTime }}
                </span>
              </div>

              <Button type="submit" class="w-full" :disabled="isVerifying || !isCodeComplete">
                <LoaderCircle v-if="isVerifying" class="animate-spin" aria-hidden="true" />
                Verificar
              </Button>

              <Button type="button" variant="ghost" class="w-full" @click="goBack">
                Volver al inicio de sesión
              </Button>
            </FieldGroup>
          </form>
        </CardContent>
      </Card>
    </div>
  </main>
</template>
