<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import QRCode from 'qrcode'
import { toast } from 'vue-sonner'
import {
  ArrowLeft,
  Copy,
  Download,
  KeyRound,
  LoaderCircle,
  Lock,
  ShieldAlert,
  ShieldCheck,
} from '@lucide/vue'

import { type MfaEnrollResponse, toApiError } from '@prestamos/api-client'
import { Alert, AlertDescription, AlertTitle } from '@prestamos/ui/components/ui/alert'
import { Badge } from '@prestamos/ui/components/ui/badge'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Field, FieldError, FieldGroup, FieldLabel } from '@prestamos/ui/components/ui/field'
import { Input } from '@prestamos/ui/components/ui/input'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'

import ThemeToggle from '@/app/components/ThemeToggle.vue'
import { safeRedirect } from '@/app/router/guards'
import { useSessionStore } from '@/app/stores/session'

const route = useRoute()
const router = useRouter()
const session = useSessionStore()

const now = ref(Date.now())
let timer: ReturnType<typeof setInterval> | null = null

const challenge = computed(() => session.mfaChallenge)
const remainingSeconds = computed(() => {
  if (!challenge.value) return 0
  return Math.max(0, Math.ceil((challenge.value.expiresAt - now.value) / 1000))
})
const formattedCountdown = computed(() => {
  const total = remainingSeconds.value
  const minutes = Math.floor(total / 60)
  const seconds = total % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
})
const challengeAvailable = computed(
  () => challenge.value !== null && remainingSeconds.value > 0,
)

/* ── Estado común y bloqueo (HTTP 423) ── */
const accountLocked = ref<{ title: string; detail: string } | null>(null)
const formError = ref<string | null>(null)
const submitting = ref(false)

/* ── Modo Verificación (MFA_REQUIRED) ── */
const useRecoveryCode = ref(false)
const totpCode = ref('')
const recoveryCode = ref('')

/* ── Modo Enrollamiento (MFA_ENROLLMENT_REQUIRED) ── */
const enrollingLoading = ref(false)
const enrollInitError = ref<string | null>(null)
const enrollment = ref<MfaEnrollResponse | null>(null)
const qrDataUrl = ref<string | null>(null)
const confirmCode = ref('')
const recoveryCodes = ref<string[] | null>(null)

function goNext() {
  return router.replace(safeRedirect(route.query.redirect, '/'))
}

function goToLogin() {
  session.clearMfaChallenge()
  return router.replace({
    name: 'login',
    query: route.query.redirect ? { redirect: String(route.query.redirect) } : undefined,
  })
}

async function copyText(text: string, label: string) {
  try {
    await navigator.clipboard.writeText(text)
    toast.success(`${label} copiado al portapapeles`)
  } catch {
    toast.error('No se pudo copiar automáticamente', {
      description: 'Cópialo manualmente desde el campo visible.',
    })
  }
}

function downloadRecoveryCodes() {
  if (!recoveryCodes.value?.length) return
  const content = [
    'Códigos de recuperación MFA - Sistema de Préstamos',
    'Guárdalos en un lugar seguro. Cada código solo puede usarse una vez.',
    '',
    ...recoveryCodes.value,
    '',
  ].join('\n')
  const blob = new Blob([content], { type: 'text/plain;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = 'codigos-recuperacion-mfa.txt'
  anchor.click()
  URL.revokeObjectURL(url)
}

async function loadEnrollment() {
  if (!challengeAvailable.value || challenge.value?.mode !== 'enroll') return
  enrollingLoading.value = true
  enrollInitError.value = null
  try {
    const data = await session.startMfaEnroll()
    enrollment.value = data
    try {
      qrDataUrl.value = await QRCode.toDataURL(data.otpauthUri, { width: 196, margin: 1 })
    } catch {
      qrDataUrl.value = null
    }
  } catch (error) {
    const apiError = toApiError(error)
    enrollInitError.value = apiError.detail || apiError.title
  } finally {
    enrollingLoading.value = false
  }
}

function toggleVerifyMethod() {
  useRecoveryCode.value = !useRecoveryCode.value
  formError.value = null
}

async function submitVerification() {
  formError.value = null
  if (!challengeAvailable.value) {
    formError.value = 'El desafío MFA expiró. Vuelve a iniciar sesión.'
    return
  }

  if (useRecoveryCode.value) {
    const cleaned = recoveryCode.value.trim()
    if (!cleaned) {
      formError.value = 'Ingresa un código de recuperación.'
      return
    }
    submitting.value = true
    try {
      await session.verifyMfa({ recoveryCode: cleaned })
      await goNext()
    } catch (error) {
      handleAuthError(error)
    } finally {
      submitting.value = false
    }
    return
  }

  const cleaned = totpCode.value.trim()
  if (!/^\d{6}$/.test(cleaned)) {
    formError.value = 'Ingresa el código TOTP de 6 dígitos.'
    return
  }

  submitting.value = true
  try {
    await session.verifyMfa({ code: cleaned })
    await goNext()
  } catch (error) {
    handleAuthError(error)
  } finally {
    submitting.value = false
  }
}

async function submitEnrollConfirmation() {
  formError.value = null
  if (!challengeAvailable.value) {
    formError.value = 'La sesión de configuración MFA expiró. Vuelve a iniciar sesión.'
    return
  }

  const cleaned = confirmCode.value.trim()
  if (!/^\d{6}$/.test(cleaned)) {
    formError.value = 'Ingresa el código de 6 dígitos generado por tu aplicación autenticadora.'
    return
  }

  submitting.value = true
  try {
    recoveryCodes.value = await session.confirmMfaEnroll(cleaned)
    toast.success('MFA activado correctamente')
  } catch (error) {
    handleAuthError(error)
  } finally {
    submitting.value = false
  }
}

function handleAuthError(error: unknown) {
  const apiError = toApiError(error)
  if (apiError.status === 423) {
    accountLocked.value = {
      title: apiError.title,
      detail: apiError.detail,
    }
    session.clearMfaChallenge()
    return
  }

  formError.value = apiError.detail || apiError.title
  toast.error(apiError.title, { description: apiError.detail })
}

onMounted(() => {
  timer = setInterval(() => {
    now.value = Date.now()
  }, 1000)

  if (challenge.value?.mode === 'enroll' && challengeAvailable.value) {
    void loadEnrollment()
  }
})

onBeforeUnmount(() => {
  if (timer) clearInterval(timer)
})
</script>

<template>
  <main class="relative grid min-h-svh place-items-center bg-muted/40 p-4 pt-14 sm:pt-4">
    <div class="absolute top-4 right-4">
      <ThemeToggle variant="outline" />
    </div>
    <div class="w-full max-w-md space-y-4">
      <!-- ── Pantalla final de códigos de recuperación tras enrollamiento ── -->
      <Card v-if="recoveryCodes">
        <CardHeader>
          <div class="flex items-center gap-2 text-primary">
            <ShieldCheck class="size-5" aria-hidden="true" />
            <CardTitle>
              <h1>Guarda tus códigos de recuperación</h1>
            </CardTitle>
          </div>
          <CardDescription>
            Tu autenticación en dos pasos está activa. Usa estos códigos de un solo uso si pierdes
            acceso a tu dispositivo autenticador.
          </CardDescription>
        </CardHeader>
        <CardContent class="space-y-4">
          <Alert variant="destructive">
            <ShieldAlert class="size-4" aria-hidden="true" />
            <AlertTitle>No volverán a mostrarse</AlertTitle>
            <AlertDescription>
              Cópialos o descárgalos antes de continuar. Cada código se invalida al utilizarse.
            </AlertDescription>
          </Alert>

          <div
            class="grid grid-cols-2 gap-2 rounded-lg border bg-muted/40 p-3 font-mono text-sm"
            data-testid="recovery-codes-list"
          >
            <span
              v-for="codeItem in recoveryCodes"
              :key="codeItem"
              class="rounded bg-background px-2 py-1 text-center"
            >
              {{ codeItem }}
            </span>
          </div>

          <div class="flex flex-wrap gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              class="flex-1"
              @click="copyText(recoveryCodes.join('\n'), 'Códigos de recuperación')"
            >
              <Copy class="size-4" aria-hidden="true" />
              Copiar códigos
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              class="flex-1"
              @click="downloadRecoveryCodes"
            >
              <Download class="size-4" aria-hidden="true" />
              Descargar (.txt)
            </Button>
          </div>
        </CardContent>
        <CardFooter>
          <Button type="button" class="w-full" @click="goNext">Continuar al panel</Button>
        </CardFooter>
      </Card>

      <!-- ── Cuenta bloqueada por intentos fallidos (HTTP 423) ── -->
      <Card v-else-if="accountLocked">
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
        <CardContent>
          <Button variant="outline" class="w-full" @click="goToLogin">
            Volver a iniciar sesión
          </Button>
        </CardContent>
      </Card>

      <!-- ── Desafío ausente o expirado ── -->
      <Card v-else-if="!challengeAvailable">
        <CardHeader>
          <CardTitle>
            <h1>Desafío de verificación no disponible</h1>
          </CardTitle>
          <CardDescription>
            Tu sesión de verificación en dos pasos expiró o no fue iniciada desde el acceso
            corporativo.
          </CardDescription>
        </CardHeader>
        <CardFooter>
          <Button type="button" variant="outline" class="w-full" @click="goToLogin">
            <ArrowLeft class="size-4" aria-hidden="true" />
            Volver a iniciar sesión
          </Button>
        </CardFooter>
      </Card>

      <!-- ── Flujo MFA_ENROLLMENT_REQUIRED ── -->
      <Card v-else-if="challenge?.mode === 'enroll'">
        <CardHeader>
          <div class="flex items-center justify-between gap-2">
            <CardTitle>
              <h1>Configurar MFA (TOTP)</h1>
            </CardTitle>
            <Badge variant="outline" class="font-mono text-xs">
              Expira en {{ formattedCountdown }}
            </Badge>
          </div>
          <CardDescription>
            Escanea el código QR con tu aplicación autenticadora para
            <span class="font-medium text-foreground">{{ challenge.email }}</span> y confirma con el
            primer código de 6 dígitos.
          </CardDescription>
        </CardHeader>
        <CardContent class="space-y-4">
          <Alert v-if="enrollInitError" variant="destructive">
            <AlertTitle>No se pudo iniciar el registro MFA</AlertTitle>
            <AlertDescription class="space-y-2">
              <p>{{ enrollInitError }}</p>
              <Button type="button" size="sm" variant="outline" @click="loadEnrollment">
                Reintentar
              </Button>
            </AlertDescription>
          </Alert>

          <template v-else>
            <div class="flex flex-col items-center justify-center rounded-lg border bg-background p-4">
              <Skeleton v-if="enrollingLoading" class="size-48 rounded-md" />
              <div v-else-if="qrDataUrl" class="rounded-md bg-white p-2">
                <img
                  :src="qrDataUrl"
                  alt="Código QR para aplicación autenticadora"
                  class="size-48 rounded-sm"
                />
              </div>
              <p v-else class="text-xs text-muted-foreground">
                Usa la clave secreta manual si no puedes escanear el QR.
              </p>
            </div>

            <div v-if="enrollment" class="space-y-2 rounded-lg border bg-muted/30 p-3 text-xs">
              <div class="flex items-center justify-between gap-2">
                <span class="font-medium text-muted-foreground">Clave secreta (Base32)</span>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  class="h-7 px-2 text-xs"
                  @click="copyText(enrollment.secret, 'Secreto MFA')"
                >
                  <Copy class="size-3.5" aria-hidden="true" />
                  Copiar
                </Button>
              </div>
              <p class="break-all font-mono text-sm font-medium">{{ enrollment.secret }}</p>

              <details class="pt-1">
                <summary class="cursor-pointer text-muted-foreground hover:text-foreground">
                  Ver URI otpauth://
                </summary>
                <div class="mt-2 space-y-1">
                  <p class="break-all font-mono text-[11px] text-muted-foreground">
                    {{ enrollment.otpauthUri }}
                  </p>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    class="h-7 text-xs"
                    @click="copyText(enrollment.otpauthUri, 'URI otpauth')"
                  >
                    Copiar URI
                  </Button>
                </div>
              </details>
            </div>

            <form novalidate class="space-y-4" @submit.prevent="submitEnrollConfirmation">
              <FieldGroup>
                <Field :data-invalid="!!formError || undefined">
                  <FieldLabel for="mfa-enroll-code">Código de confirmación (6 dígitos)</FieldLabel>
                  <Input
                    id="mfa-enroll-code"
                    v-model="confirmCode"
                    type="text"
                    inputmode="numeric"
                    maxlength="6"
                    autocomplete="one-time-code"
                    placeholder="000000"
                    class="font-mono tracking-widest"
                    :aria-invalid="!!formError"
                  />
                  <FieldError :errors="[formError ?? undefined]" />
                </Field>
                <Button
                  type="submit"
                  class="w-full"
                  :disabled="submitting || enrollingLoading || !enrollment"
                >
                  <LoaderCircle v-if="submitting" class="animate-spin" aria-hidden="true" />
                  Confirmar y activar MFA
                </Button>
              </FieldGroup>
            </form>
          </template>
        </CardContent>
        <CardFooter>
          <Button type="button" variant="ghost" class="w-full" @click="goToLogin">
            <ArrowLeft class="size-4" aria-hidden="true" />
            Volver al inicio de sesión
          </Button>
        </CardFooter>
      </Card>

      <!-- ── Flujo MFA_REQUIRED ── -->
      <Card v-else>
        <CardHeader>
          <div class="flex items-center justify-between gap-2">
            <CardTitle>
              <h1>Verificación en dos pasos</h1>
            </CardTitle>
            <Badge variant="outline" class="font-mono text-xs">
              Expira en {{ formattedCountdown }}
            </Badge>
          </div>
          <CardDescription>
            Cuenta: <span class="font-medium text-foreground">{{ challenge?.email }}</span>
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form novalidate class="space-y-4" @submit.prevent="submitVerification">
            <FieldGroup>
              <Field v-if="!useRecoveryCode" :data-invalid="!!formError || undefined">
                <FieldLabel for="mfa-code">Código de aplicación autenticadora</FieldLabel>
                <Input
                  id="mfa-code"
                  v-model="totpCode"
                  type="text"
                  inputmode="numeric"
                  maxlength="6"
                  autocomplete="one-time-code"
                  placeholder="123456"
                  class="font-mono tracking-widest"
                  :aria-invalid="!!formError"
                />
                <FieldError :errors="[formError ?? undefined]" />
              </Field>

              <Field v-else :data-invalid="!!formError || undefined">
                <FieldLabel for="mfa-recovery-code">Código de recuperación de un solo uso</FieldLabel>
                <Input
                  id="mfa-recovery-code"
                  v-model="recoveryCode"
                  type="text"
                  autocomplete="off"
                  placeholder="ABCD-EFGH-IJKL"
                  class="font-mono uppercase tracking-wider"
                  :aria-invalid="!!formError"
                />
                <FieldError :errors="[formError ?? undefined]" />
              </Field>

              <Button type="submit" class="w-full" :disabled="submitting">
                <LoaderCircle v-if="submitting" class="animate-spin" aria-hidden="true" />
                Verificar e ingresar
              </Button>
            </FieldGroup>
          </form>
        </CardContent>
        <CardFooter class="flex flex-col gap-2">
          <Button type="button" variant="outline" class="w-full" @click="toggleVerifyMethod">
            <KeyRound class="size-4" aria-hidden="true" />
            {{
              useRecoveryCode
                ? 'Usar código de aplicación (TOTP)'
                : 'Usar un código de recuperación'
            }}
          </Button>
          <Button type="button" variant="ghost" class="w-full" @click="goToLogin">
            <ArrowLeft class="size-4" aria-hidden="true" />
            Volver al inicio de sesión
          </Button>
        </CardFooter>
      </Card>
    </div>
  </main>
</template>
