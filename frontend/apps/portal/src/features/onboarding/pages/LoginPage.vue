<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import {
  AlertCircle,
  ArrowRight,
  LoaderCircle,
  Smartphone,
  Sparkles,
} from '@lucide/vue'

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

import { useSessionStore } from '@/app/stores/session'

import { useClientAuth } from '../composables'

// FE-CP-02: Login del cliente prestatario con correo y contraseña.

const route = useRoute()
const session = useSessionStore()
const sessionMotivo = computed(() =>
  route.query.motivo === 'sesion-comprometida'
    ? 'Detectamos reutilización de tu sesión y cerramos tu acceso por seguridad. Ingresa nuevamente.'
    : route.query.motivo === 'sesion-expirada'
      ? 'Tu sesión expiró. Ingresa nuevamente para continuar.'
      : null,
)

const {
  loginIdentifier,
  loginPassword,
  loginLoading,
  loginError,
  mfaChallengeToken,
  mfaCode,
  login,
  verifyMfaLogin,
  cancelMfaChallenge,
} = useClientAuth()

interface DemoAccount {
  name: string
  email: string
  phone: string
  badge: string
  badgeClass: string
  description: string
  initials: string
  avatarBg: string
}

const demoAccounts: DemoAccount[] = [
  {
    name: 'María Quispe Huamán',
    email: 'cliente@correo.pe',
    phone: '987000000',
    badge: 'Con Préstamo Activo',
    badgeClass: 'bg-emerald-500/10 text-emerald-600 border-emerald-500/20 dark:bg-emerald-950 dark:text-emerald-400',
    description: 'Crédito vigente #loan-001 (cuotas por pagar y cronograma)',
    initials: 'MQ',
    avatarBg: 'bg-emerald-500/10 text-emerald-600',
  },
  {
    name: 'Juan Pérez Alva',
    email: 'nuevo@correo.pe',
    phone: '987000101',
    badge: 'Registrado sin Préstamos',
    badgeClass: 'bg-sky-500/10 text-sky-600 border-sky-500/20 dark:bg-sky-950 dark:text-sky-400',
    description: 'Usuario nuevo con KYC aprobado, listo para solicitar su primer crédito',
    initials: 'JP',
    avatarBg: 'bg-sky-500/10 text-sky-600',
  },
]

function fillDemoClient(account: DemoAccount) {
  loginIdentifier.value = account.email
  loginPassword.value = 'Demo1234!'
}
</script>

<template>
  <div class="min-h-[85vh] flex flex-col justify-center py-6 px-4">
    <Card class="w-full max-w-md mx-auto shadow-xl border-border">
      <CardHeader class="text-center pb-4">
        <div class="mx-auto size-12 rounded-2xl bg-primary/10 flex items-center justify-center text-primary mb-2 shadow-xs">
          <Smartphone class="size-6" />
        </div>
        <CardTitle class="text-xl font-bold tracking-tight">Portal Prestatario</CardTitle>
        <CardDescription class="text-xs">
          Ingresa a tu cuenta para solicitar préstamos, ver cuotas y pagar en línea.
        </CardDescription>
      </CardHeader>

      <CardContent class="space-y-4">
        <Alert v-if="sessionMotivo && !loginError" variant="destructive" class="py-2.5">
          <AlertCircle class="size-4" />
          <AlertDescription class="text-xs">{{ sessionMotivo }}</AlertDescription>
        </Alert>

        <Alert v-if="loginError" variant="destructive" class="py-2.5">
          <AlertCircle class="size-4" />
          <AlertDescription class="text-xs">{{ loginError }}</AlertDescription>
        </Alert>

        <form v-if="mfaChallengeToken" class="space-y-3.5" @submit.prevent="verifyMfaLogin">
          <div class="space-y-1.5">
            <Label for="clientMfaCode" class="text-xs font-medium">
              Código de verificación (6 dígitos)
            </Label>
            <Input
              id="clientMfaCode"
              v-model="mfaCode"
              type="text"
              inputmode="numeric"
              maxlength="6"
              autocomplete="one-time-code"
              placeholder="000000"
              required
              class="h-10 text-xs font-mono tracking-widest"
            />
          </div>
          <Button type="submit" class="w-full h-10 font-semibold text-xs" :disabled="loginLoading">
            <LoaderCircle v-if="loginLoading" class="mr-2 size-4 animate-spin" />
            <span>Confirmar código</span>
          </Button>
          <Button
            type="button"
            variant="ghost"
            class="w-full h-9 text-xs"
            @click="cancelMfaChallenge"
          >
            Volver al inicio de sesión
          </Button>
        </form>

        <form v-else class="space-y-3.5" @submit.prevent="login">
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
            <span>Ingresar al Portal</span>
            <ArrowRight v-if="!loginLoading" class="ml-2 size-4" />
          </Button>
        </form>

        <!-- Accesos directos Demo para pruebas (solo cuando VITE_DEMO_LOGIN=true) -->
        <div v-if="session.demoEnabled" class="pt-3 border-t border-border space-y-2.5">
          <div class="flex items-center justify-between">
            <span class="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
              <Sparkles class="size-3 text-primary" />
              Cuentas Demo para Pruebas
            </span>
            <span class="text-[10px] text-muted-foreground">Clic para autocompletar</span>
          </div>

          <div class="grid grid-cols-1 gap-2">
            <button
              v-for="acc in demoAccounts"
              :key="acc.email"
              type="button"
              class="group relative flex items-start gap-3 rounded-xl border border-border bg-card p-3 text-left transition-all hover:border-primary/50 hover:bg-muted/40 focus:outline-none focus:ring-2 focus:ring-primary/20"
              :class="{
                'ring-2 ring-primary border-primary bg-primary/5 shadow-xs': loginIdentifier === acc.email,
              }"
              @click="fillDemoClient(acc)"
            >
              <div
                class="size-9 rounded-xl flex items-center justify-center font-bold text-xs shrink-0 mt-0.5"
                :class="acc.avatarBg"
              >
                {{ acc.initials }}
              </div>

              <div class="flex-1 min-w-0">
                <div class="flex items-center justify-between gap-1 mb-0.5">
                  <span class="text-xs font-semibold text-foreground truncate">
                    {{ acc.name }}
                  </span>
                  <span
                    class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[9px] font-semibold shrink-0"
                    :class="acc.badgeClass"
                  >
                    {{ acc.badge }}
                  </span>
                </div>

                <p class="text-[11px] text-muted-foreground leading-snug">
                  {{ acc.description }}
                </p>

                <div class="mt-1 flex items-center gap-2 text-[10px] font-mono text-muted-foreground">
                  <span>{{ acc.email }}</span>
                  <span>·</span>
                  <span>Clave: Demo1234!</span>
                </div>
              </div>
            </button>
          </div>
        </div>

        <div class="text-center pt-2 text-xs text-muted-foreground">
          ¿No tienes una cuenta aún?
          <RouterLink to="/registro" class="text-primary font-semibold hover:underline block mt-1">
            Crea tu cuenta aquí
          </RouterLink>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
