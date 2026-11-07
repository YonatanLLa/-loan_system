<script setup lang="ts">
import { RouterLink } from 'vue-router'
import {
  AlertCircle,
  ArrowRight,
  CheckCircle2,
  KeyRound,
  LoaderCircle,
  RotateCcw,
  UserPlus,
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

import { useClientAuth } from '../composables'

// FE-CP-01: Registro con documento, nombres, correo, celular y contraseña; verificación por OTP.

const {
  registerForm,
  isOtpModalOpen,
  otpCode,
  otpCountdown,
  isOtpBusy,
  registerError,
  requestOtp,
  verifyOtpAndRegister,
} = useClientAuth()

const selectClass =
  'flex h-10 w-full rounded-md border border-input bg-transparent px-3 py-1 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring'
</script>

<template>
  <div class="min-h-[80vh] flex flex-col justify-center py-4">
    <Card class="w-full max-w-md mx-auto shadow-xl border-border">
      <CardHeader class="pb-3 text-center">
        <div class="mx-auto size-11 rounded-2xl bg-primary/10 flex items-center justify-center text-primary mb-1">
          <UserPlus class="size-5" />
        </div>
        <CardTitle class="text-xl font-bold">Crea tu Cuenta</CardTitle>
        <CardDescription class="text-xs">
          Regístrate en minutos para acceder a préstamos 100% digitales.
        </CardDescription>
      </CardHeader>

      <CardContent class="space-y-4">
        <Alert v-if="registerError && !isOtpModalOpen" variant="destructive" class="py-2.5">
          <AlertCircle class="size-4" />
          <AlertDescription class="text-xs">{{ registerError }}</AlertDescription>
        </Alert>

        <form class="space-y-3" @submit.prevent="requestOtp">
          <div class="grid grid-cols-3 gap-2">
            <div class="space-y-1">
              <Label for="regDocType" class="text-xs font-medium">Documento</Label>
              <select id="regDocType" v-model="registerForm.docType" :class="selectClass">
                <option value="DNI">DNI</option>
                <option value="CE">C.E.</option>
                <option value="PASAPORTE">Pasap.</option>
              </select>
            </div>
            <div class="col-span-2 space-y-1">
              <Label for="regDocNumber" class="text-xs font-medium">Número</Label>
              <Input
                id="regDocNumber"
                v-model="registerForm.docNumber"
                type="text"
                maxlength="12"
                placeholder="Ej. 72345678"
                required
                class="h-10 text-xs font-mono"
              />
            </div>
          </div>

          <div class="space-y-1">
            <Label for="regFullName" class="text-xs font-medium">Nombres y Apellidos Completos</Label>
            <Input
              id="regFullName"
              v-model="registerForm.fullName"
              placeholder="Ej. Juan Carlos Pérez Morales"
              required
              class="h-10 text-xs"
            />
          </div>

          <div class="space-y-1">
            <Label for="regPhone" class="text-xs font-medium">Celular (para validación OTP)</Label>
            <Input
              id="regPhone"
              v-model="registerForm.phone"
              type="tel"
              maxlength="9"
              placeholder="987654321"
              required
              class="h-10 text-xs font-mono"
            />
            <p class="text-[10px] text-muted-foreground">
              Te enviaremos un código de seguridad por SMS o WhatsApp.
            </p>
          </div>

          <div class="space-y-1">
            <Label for="regEmail" class="text-xs font-medium">Correo Electrónico</Label>
            <Input
              id="regEmail"
              v-model="registerForm.email"
              type="email"
              placeholder="tunombre@correo.pe"
              required
              class="h-10 text-xs"
            />
          </div>

          <div class="space-y-1">
            <Label for="regPassword" class="text-xs font-medium">Crea una Contraseña</Label>
            <Input
              id="regPassword"
              v-model="registerForm.password"
              type="password"
              placeholder="Mínimo 8 caracteres"
              required
              class="h-10 text-xs"
            />
          </div>

          <Button type="submit" class="w-full h-10 mt-3 font-semibold text-xs" :disabled="isOtpBusy">
            <LoaderCircle v-if="isOtpBusy" class="mr-2 size-4 animate-spin" />
            <span>Validar Celular con OTP</span>
            <ArrowRight v-if="!isOtpBusy" class="ml-2 size-4" />
          </Button>
        </form>

        <div class="text-center pt-3 border-t text-xs text-muted-foreground">
          ¿Ya te registraste anteriormente?
          <RouterLink to="/login" class="text-primary font-semibold hover:underline block mt-1">
            Inicia sesión aquí
          </RouterLink>
        </div>
      </CardContent>
    </Card>

    <!-- Modal de Verificación OTP (FE-CP-01) -->
    <div
      v-if="isOtpModalOpen"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
    >
      <Card class="w-full max-w-sm shadow-2xl">
        <CardHeader class="text-center pb-3 border-b">
          <div class="mx-auto size-12 rounded-full bg-primary/10 flex items-center justify-center text-primary mb-1">
            <KeyRound class="size-6" />
          </div>
          <CardTitle class="text-lg">Confirma tu Celular</CardTitle>
          <CardDescription class="text-xs">
            Ingresa el código OTP de 6 dígitos enviado al <span class="font-bold text-foreground font-mono">+51 {{ registerForm.phone }}</span>
          </CardDescription>
        </CardHeader>

        <form @submit.prevent="verifyOtpAndRegister">
          <CardContent class="space-y-4 pt-4 text-xs">
            <Alert v-if="registerError" variant="destructive" class="py-2">
              <AlertCircle class="size-4" />
              <AlertDescription class="text-xs">{{ registerError }}</AlertDescription>
            </Alert>

            <div class="space-y-1.5 text-center">
              <Input
                id="otpCode"
                v-model="otpCode"
                type="text"
                maxlength="6"
                placeholder="123456"
                required
                autofocus
                class="h-12 text-center text-2xl font-mono tracking-widest font-bold"
              />
              <p class="text-[11px] text-muted-foreground">
                (En entorno de prueba/demo, puedes usar el código <span class="font-bold font-mono text-primary">123456</span>)
              </p>
            </div>

            <div class="flex items-center justify-between text-xs text-muted-foreground pt-1">
              <span>¿No te llegó el código?</span>
              <button
                type="button"
                class="font-semibold text-primary hover:underline flex items-center gap-1"
                :disabled="otpCountdown > 0 || isOtpBusy"
                @click="requestOtp"
              >
                <RotateCcw class="size-3" />
                <span v-if="otpCountdown > 0">Reenviar en {{ otpCountdown }}s</span>
                <span v-else>Reenviar código</span>
              </button>
            </div>
          </CardContent>

          <div class="flex justify-end gap-2 border-t bg-muted/20 p-4">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              :disabled="isOtpBusy"
              @click="isOtpModalOpen = false"
            >
              Corregir Datos
            </Button>
            <Button
              type="submit"
              size="sm"
              :disabled="isOtpBusy || otpCode.length < 6"
            >
              <LoaderCircle v-if="isOtpBusy" class="mr-1.5 size-3.5 animate-spin" />
              <CheckCircle2 v-else class="mr-1.5 size-3.5" />
              Confirmar y Registrar
            </Button>
          </div>
        </form>
      </Card>
    </div>
  </div>
</template>
