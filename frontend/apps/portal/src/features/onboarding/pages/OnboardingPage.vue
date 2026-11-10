<script setup lang="ts">
import { RouterLink } from 'vue-router'
import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
  Building2,
  Camera,
  CheckCircle2,
  Clock,
  FileCheck2,
  FilePlus,
  IdCard,
  LoaderCircle,
  RotateCcw,
  Smartphone,
  Sparkles,
  UserCheck,
  Wallet,
} from '@lucide/vue'

import { Alert, AlertDescription } from '@prestamos/ui/components/ui/alert'
import { Badge } from '@prestamos/ui/components/ui/badge'
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

import { useKycOnboarding } from '../composables'

// FE-CP-03 a FE-CP-05: Expediente KYC móvil, consentimientos legales y cuenta de desembolso.

const {
  userKycStatus,
  retryKyc,
  currentStep,
  isBusy,
  errorMessage,
  canProceed,
  nextStep,
  prevStep,
  completeOnboarding,
  // Paso 1
  dniFront,
  dniBack,
  simulateUploadDniFront,
  simulateUploadDniBack,
  // Paso 2
  selfie,
  simulateCaptureSelfie,
  // Paso 3
  consentDataProtection,
  consentCreditBureau,
  consentMarketing,
  // Paso 4
  payoutType,
  bankName,
  cci,
  phoneWallet,
} = useKycOnboarding()

const PERU_BANKS = [
  'Banco de Crédito BCP',
  'BBVA Perú',
  'Interbank',
  'Scotiabank Perú',
  'Banco de la Nación',
  'BanBif',
  'Banco Pichincha',
]
</script>

<template>
  <div class="space-y-4 py-2">
    <!-- ESTADO: IDENTIDAD VERIFICADA / APROBADA (Requisito 2) -->
    <Card v-if="userKycStatus === 'VERIFIED'" class="shadow-md border-success/30 bg-success/5 text-center p-6 space-y-4">
      <div class="mx-auto size-14 rounded-full bg-success/20 flex items-center justify-center text-success">
        <CheckCircle2 class="size-8" />
      </div>
      <div class="space-y-1">
        <Badge class="bg-emerald-100 text-emerald-800 font-semibold mb-1">
          KYC Verificado / Aprobado
        </Badge>
        <h2 class="text-lg font-bold text-foreground">Identidad Verificada Exitosamente</h2>
        <p class="text-xs text-muted-foreground max-w-sm mx-auto">
          Tus fotografías de DNI por ambos lados y prueba de vida biométrica han sido aprobadas conforme a la Ley N° 29733. Tu cuenta está habilitada para solicitar créditos.
        </p>
      </div>

      <div class="flex flex-col sm:flex-row gap-2 justify-center pt-2 max-w-xs mx-auto">
        <Button as-child class="h-9 text-xs font-semibold">
          <RouterLink to="/solicitar">
            <FilePlus class="size-3.5 mr-1.5" />
            Solicitar Mi Préstamo
          </RouterLink>
        </Button>
        <Button as-child variant="outline" class="h-9 text-xs">
          <RouterLink to="/inicio">
            Ir a Inicio
          </RouterLink>
        </Button>
      </div>
    </Card>

    <!-- ESTADO: IDENTIDAD EN EVALUACIÓN / PENDIENTE (Requisito 2) -->
    <Card v-else-if="userKycStatus === 'PENDING'" class="shadow-md border-amber-200 bg-amber-50/50 text-center p-6 space-y-4">
      <div class="mx-auto size-14 rounded-full bg-amber-100 flex items-center justify-center text-amber-600">
        <Clock class="size-8 animate-pulse" />
      </div>
      <div class="space-y-1">
        <Badge class="bg-amber-100 text-amber-800 font-semibold mb-1">
          KYC en Evaluación
        </Badge>
        <h2 class="text-lg font-bold text-amber-950">Verificación de Identidad en Proceso</h2>
        <p class="text-xs text-amber-900 max-w-sm mx-auto">
          Hemos recibido tus fotografías de DNI y selfie biométrica. Nuestro equipo de cumplimiento se encuentra validando tu expediente contra RENIEC. El resultado se reflejará en breve.
        </p>
      </div>

      <div class="flex flex-col sm:flex-row gap-2 justify-center pt-2 max-w-xs mx-auto">
        <Button as-child variant="outline" class="h-9 text-xs">
          <RouterLink to="/inicio">
            Ir a Mi Inicio
          </RouterLink>
        </Button>
      </div>
    </Card>

    <!-- ESTADO: IDENTIDAD RECHAZADA (Requisito 2) -->
    <Card v-else-if="userKycStatus === 'REJECTED'" class="shadow-md border-rose-200 bg-rose-50/50 text-center p-6 space-y-4">
      <div class="mx-auto size-14 rounded-full bg-rose-100 flex items-center justify-center text-rose-600">
        <AlertCircle class="size-8" />
      </div>
      <div class="space-y-1">
        <Badge class="bg-rose-100 text-rose-800 font-semibold mb-1">
          KYC Rechazado
        </Badge>
        <h2 class="text-lg font-bold text-rose-950">Verificación de Identidad No Aprobada</h2>
        <p class="text-xs text-rose-900 max-w-sm mx-auto">
          No pudimos verificar la nitidez o correspondencia de las fotos de tu documento. Por favor, asegúrate de que las fotos no tengan reflejos y tu rostro esté bien iluminado.
        </p>
      </div>

      <div class="pt-2">
        <Button type="button" class="h-9 text-xs font-semibold" @click="retryKyc">
          <RotateCcw class="size-3.5 mr-1.5" />
          Volver a Tomar Fotos
        </Button>
      </div>
    </Card>

    <!-- FORMULARIO SECUENCIAL DE 4 PASOS -->
    <template v-else>
    <!-- Header y Stepper Móvil -->
    <div class="text-center space-y-1">
      <Badge variant="outline" class="font-mono text-[10px] px-2 py-0.5 border-primary/40 text-primary">
        PASO {{ currentStep }} DE 4
      </Badge>
      <h2 class="text-lg font-bold">Verifica tu Identidad</h2>
      <p class="text-xs text-muted-foreground">
        Completarás este proceso solo una vez para activar tu perfil crediticio.
      </p>
    </div>

    <!-- Barra de progreso visual -->
    <div class="grid grid-cols-4 gap-1.5 px-1">
      <div
        v-for="s in [1, 2, 3, 4]"
        :key="s"
        class="h-1.5 rounded-full transition-all duration-300"
        :class="
          s < currentStep
            ? 'bg-success'
            : s === currentStep
            ? 'bg-primary'
            : 'bg-muted'
        "
      />
    </div>

    <Alert v-if="errorMessage" variant="destructive" class="py-2.5">
      <AlertCircle class="size-4" />
      <AlertDescription class="text-xs">{{ errorMessage }}</AlertDescription>
    </Alert>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- PASO 1: FOTOS DE DNI (FE-CP-03)                            -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <Card v-if="currentStep === 1" class="shadow-md">
      <CardHeader class="pb-3">
        <div class="flex items-center gap-2 text-primary">
          <IdCard class="size-5" />
          <CardTitle class="text-base">Foto de tu Documento (DNI / CE)</CardTitle>
        </div>
        <CardDescription class="text-xs">
          Asegúrate de que la imagen sea clara, sin reflejos y con los 4 bordes visibles.
        </CardDescription>
      </CardHeader>

      <CardContent class="space-y-4 text-xs">
        <!-- DNI Anverso -->
        <div class="space-y-2 rounded-xl border border-dashed p-3.5 text-center bg-muted/10">
          <span class="font-semibold block text-foreground">1. Lado Frontal (Anverso)</span>
          <div v-if="dniFront" class="space-y-2">
            <div class="h-28 rounded-lg bg-success/10 border border-success/30 flex flex-col items-center justify-center text-success gap-1.5 p-2">
              <CheckCircle2 class="size-6" />
              <span class="font-medium text-[11px]">Foto frontal capturada correctamente</span>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              class="h-7 text-[11px]"
              @click="dniFront = null"
            >
              Volver a tomar foto
            </Button>
          </div>
          <div v-else class="space-y-2">
            <p class="text-muted-foreground text-[11px]">
              Toma una foto de la parte frontal donde aparece tu rostro y DNI.
            </p>
            <Button
              type="button"
              size="sm"
              class="h-8 text-xs w-full sm:w-auto"
              @click="simulateUploadDniFront"
            >
              <Camera class="size-3.5 mr-1.5" />
              Capturar Foto Anverso
            </Button>
          </div>
        </div>

        <!-- DNI Reverso -->
        <div class="space-y-2 rounded-xl border border-dashed p-3.5 text-center bg-muted/10">
          <span class="font-semibold block text-foreground">2. Lado Posterior (Reverso)</span>
          <div v-if="dniBack" class="space-y-2">
            <div class="h-28 rounded-lg bg-success/10 border border-success/30 flex flex-col items-center justify-center text-success gap-1.5 p-2">
              <CheckCircle2 class="size-6" />
              <span class="font-medium text-[11px]">Foto posterior capturada correctamente</span>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              class="h-7 text-[11px]"
              @click="dniBack = null"
            >
              Volver a tomar foto
            </Button>
          </div>
          <div v-else class="space-y-2">
            <p class="text-muted-foreground text-[11px]">
              Toma una foto del reverso donde se encuentra el código de barras y ubigeo.
            </p>
            <Button
              type="button"
              size="sm"
              class="h-8 text-xs w-full sm:w-auto"
              @click="simulateUploadDniBack"
            >
              <Camera class="size-3.5 mr-1.5" />
              Capturar Foto Reverso
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- PASO 2: SELFIE CON PRUEBA DE VIDA (FE-CP-03)              -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <Card v-else-if="currentStep === 2" class="shadow-md">
      <CardHeader class="pb-3 text-center">
        <div class="mx-auto size-12 rounded-full bg-primary/10 flex items-center justify-center text-primary mb-1">
          <Camera class="size-6" />
        </div>
        <CardTitle class="text-base">Selfie con Reconocimiento Facial</CardTitle>
        <CardDescription class="text-xs">
          Validaremos biométricamente tu rostro frente a la foto de tu documento.
        </CardDescription>
      </CardHeader>

      <CardContent class="space-y-4 text-xs text-center">
        <div class="mx-auto max-w-xs p-4 rounded-2xl border border-dashed bg-muted/20 space-y-3">
          <div
            class="mx-auto size-44 rounded-full border-4 border-primary/40 flex items-center justify-center overflow-hidden transition-all"
            :class="selfie ? 'border-success bg-success/10' : 'bg-muted/40'"
          >
            <div v-if="selfie" class="text-center p-2 text-success">
              <CheckCircle2 class="size-12 mx-auto mb-1" />
              <span class="font-bold text-xs block">Biometría Aprobada</span>
              <span class="text-[10px] text-muted-foreground">Prueba de vida verificada</span>
            </div>
            <div v-else class="text-muted-foreground p-3">
              <UserCheck class="size-12 mx-auto mb-1 opacity-40" />
              <span class="text-[11px] block">Ubica tu rostro dentro del círculo</span>
            </div>
          </div>

          <div v-if="!selfie">
            <Button
              type="button"
              size="sm"
              class="w-full h-9 text-xs"
              @click="simulateCaptureSelfie"
            >
              <Sparkles class="size-3.5 mr-1.5" />
              Iniciar Captura y Prueba de Vida
            </Button>
          </div>
          <div v-else>
            <Button
              type="button"
              variant="outline"
              size="sm"
              class="h-8 text-xs"
              @click="selfie = null"
            >
              Tomar selfie nuevamente
            </Button>
          </div>
        </div>

        <ul class="text-[11px] text-muted-foreground space-y-1 text-left max-w-xs mx-auto">
          <li>• Quítate lentes oscuros, gorras o mascarillas.</li>
          <li>• Sitúate en un ambiente con buena iluminación natural.</li>
        </ul>
      </CardContent>
    </Card>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- PASO 3: CONSENTIMIENTOS LEGALES (FE-CP-04)                -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <Card v-else-if="currentStep === 3" class="shadow-md">
      <CardHeader class="pb-3">
        <div class="flex items-center gap-2 text-primary">
          <FileCheck2 class="size-5" />
          <CardTitle class="text-base">Consentimientos y Autorizaciones</CardTitle>
        </div>
        <CardDescription class="text-xs">
          Separados por finalidad de acuerdo a la Ley N° 29733 de Protección de Datos Personales.
        </CardDescription>
      </CardHeader>

      <CardContent class="space-y-4 text-xs">
        <!-- Tratamiento de Datos -->
        <label
          class="flex items-start gap-3 p-3 rounded-lg border cursor-pointer hover:bg-muted/10 transition-colors"
          :class="consentDataProtection ? 'border-primary/50 bg-primary/5' : 'border-border'"
        >
          <input
            v-model="consentDataProtection"
            type="checkbox"
            class="size-4 mt-0.5 rounded border-input text-primary focus:ring-primary"
          />
          <div class="space-y-1">
            <span class="font-semibold block text-foreground">
              1. Tratamiento de Datos Personales (Obligatorio)
            </span>
            <p class="text-[11px] text-muted-foreground leading-relaxed">
              Autorizo el almacenamiento y tratamiento de mis datos personales para la evaluación y gestión crediticia conforme a la Ley N° 29733.
            </p>
          </div>
        </label>

        <!-- Centrales de Riesgo -->
        <label
          class="flex items-start gap-3 p-3 rounded-lg border cursor-pointer hover:bg-muted/10 transition-colors"
          :class="consentCreditBureau ? 'border-primary/50 bg-primary/5' : 'border-border'"
        >
          <input
            v-model="consentCreditBureau"
            type="checkbox"
            class="size-4 mt-0.5 rounded border-input text-primary focus:ring-primary"
          />
          <div class="space-y-1">
            <span class="font-semibold block text-foreground">
              2. Consulta a Centrales Privadas de Riesgo y SBS (Obligatorio)
            </span>
            <p class="text-[11px] text-muted-foreground leading-relaxed">
              Autorizo a consultar mi historial crediticio, récord de endeudamiento y score ante la SBS y centrales de riesgo privadas (Equifax / Experian).
            </p>
          </div>
        </label>

        <!-- Fines Comerciales -->
        <label
          class="flex items-start gap-3 p-3 rounded-lg border cursor-pointer hover:bg-muted/10 transition-colors"
          :class="consentMarketing ? 'border-primary/50 bg-primary/5' : 'border-border'"
        >
          <input
            v-model="consentMarketing"
            type="checkbox"
            class="size-4 mt-0.5 rounded border-input text-primary focus:ring-primary"
          />
          <div class="space-y-1">
            <span class="font-semibold block text-foreground">
              3. Comunicaciones y Ofertas Comerciales (Opcional)
            </span>
            <p class="text-[11px] text-muted-foreground leading-relaxed">
              Deseo recibir ofertas exclusivas de refinanciamiento, incremento de línea y beneficios a través de WhatsApp o correo electrónico.
            </p>
          </div>
        </label>
      </CardContent>
    </Card>

    <!-- ══════════════════════════════════════════════════════════ -->
    <!-- PASO 4: CUENTA DE DESEMBOLSO (FE-CP-05)                   -->
    <!-- ══════════════════════════════════════════════════════════ -->
    <Card v-else-if="currentStep === 4" class="shadow-md">
      <CardHeader class="pb-3">
        <div class="flex items-center gap-2 text-primary">
          <Wallet class="size-5" />
          <CardTitle class="text-base">¿Dónde Recibirás tus Préstamos?</CardTitle>
        </div>
        <CardDescription class="text-xs">
          La cuenta debe estar a tu nombre. Desembolsamos inmediatamente una vez aprobado.
        </CardDescription>
      </CardHeader>

      <CardContent class="space-y-4 text-xs">
        <!-- Selector de Método -->
        <div class="grid grid-cols-2 gap-2">
          <button
            type="button"
            class="p-3 rounded-lg border text-center transition-all flex flex-col items-center gap-1.5"
            :class="
              payoutType === 'BANK'
                ? 'border-primary bg-primary/10 text-primary font-semibold'
                : 'border-border text-muted-foreground hover:bg-muted/10'
            "
            @click="payoutType = 'BANK'"
          >
            <Building2 class="size-5" />
            <span>Cuenta Bancaria (CCI)</span>
          </button>

          <button
            type="button"
            class="p-3 rounded-lg border text-center transition-all flex flex-col items-center gap-1.5"
            :class="
              payoutType === 'WALLET'
                ? 'border-primary bg-primary/10 text-primary font-semibold'
                : 'border-border text-muted-foreground hover:bg-muted/10'
            "
            @click="payoutType = 'WALLET'"
          >
            <Smartphone class="size-5" />
            <span>Billetera Yape / Plin</span>
          </button>
        </div>

        <!-- Campos para Cuenta Bancaria -->
        <div v-if="payoutType === 'BANK'" class="space-y-3 pt-2">
          <div class="space-y-1">
            <Label for="payoutBank" class="text-xs font-medium">Entidad Financiera</Label>
            <select
              id="payoutBank"
              v-model="bankName"
              class="flex h-10 w-full rounded-md border border-input bg-transparent px-3 py-1 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option v-for="b in PERU_BANKS" :key="b" :value="b">{{ b }}</option>
            </select>
          </div>

          <div class="space-y-1">
            <Label for="payoutCci" class="text-xs font-medium">Código de Cuenta Interbancario (CCI - 20 dígitos)</Label>
            <Input
              id="payoutCci"
              v-model="cci"
              type="text"
              maxlength="20"
              placeholder="00219000000000010000"
              required
              class="h-10 text-xs font-mono"
            />
            <p class="text-[10px] text-muted-foreground">
              Puedes encontrar tu CCI de 20 dígitos en la app o web de tu banco.
            </p>
          </div>
        </div>

        <!-- Campos para Billetera Digital -->
        <div v-else class="space-y-3 pt-2">
          <div class="space-y-1">
            <Label for="payoutWallet" class="text-xs font-medium">Número Celular Afiliado a Yape o Plin</Label>
            <Input
              id="payoutWallet"
              v-model="phoneWallet"
              type="tel"
              maxlength="9"
              placeholder="987654321"
              required
              class="h-10 text-xs font-mono"
            />
            <p class="text-[10px] text-muted-foreground">
              Válido para montos autorizados por la billetera digital hasta S/ 500 por operación.
            </p>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Botones de Navegación Inferiores -->
    <div class="flex items-center justify-between gap-3 pt-2">
      <Button
        v-if="currentStep > 1"
        type="button"
        variant="outline"
        size="sm"
        class="h-9 text-xs"
        :disabled="isBusy"
        @click="prevStep"
      >
        <ArrowLeft class="size-3.5 mr-1" />
        Anterior
      </Button>
      <div v-else />

      <Button
        v-if="currentStep < 4"
        type="button"
        size="sm"
        class="h-9 text-xs"
        :disabled="!canProceed"
        @click="nextStep"
      >
        <span>Siguiente</span>
        <ArrowRight class="size-3.5 ml-1" />
      </Button>

      <Button
        v-else
        type="button"
        size="sm"
        class="h-9 text-xs font-semibold"
        :disabled="!canProceed || isBusy"
        @click="completeOnboarding"
      >
        <LoaderCircle v-if="isBusy" class="mr-1.5 size-3.5 animate-spin" />
        <CheckCircle2 v-else class="mr-1.5 size-3.5" />
        Finalizar Verificación
      </Button>
    </div>
    </template>
  </div>
</template>
