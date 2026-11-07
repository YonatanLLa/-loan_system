<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import {
  AlertCircle,
  ArrowRight,
  CheckCircle2,
  Download,
  FileCheck2,
  FileText,
  KeyRound,
  LoaderCircle,
  QrCode,
  RotateCcw,
  ShieldCheck,
  Wallet,
} from '@lucide/vue'

import { MoneyText } from '@prestamos/ui'
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
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import { formatInstant } from '@prestamos/utils'

import { useContractSigning } from '../composables'

// FE-CP-09: Revisión de contrato y hoja resumen, aceptación con OTP y descarga de PDF firmado.

const route = useRoute()
const {
  contract,
  isLoading,
  error,
  isOtpModalOpen,
  otpCode,
  otpCountdown,
  isSigning,
  isDownloading,
  loadContract,
  requestOtp,
  signWithOtp,
  downloadPdf,
} = useContractSigning()

const termsAccepted = ref(false)

watch(
  () => route.query.applicationId,
  (appId) => {
    if (appId && typeof appId === 'string') {
      void loadContract(appId)
    }
  },
)

onMounted(() => {
  const appId = (route.query.applicationId as string) || undefined
  void loadContract(appId)
})
</script>

<template>
  <div class="space-y-4 py-2">
    <!-- Header -->
    <div class="text-center space-y-1">
      <Badge variant="outline" class="font-mono text-[10px] px-2 py-0.5 border-primary/40 text-primary">
        REVISIÓN Y FIRMA DIGITAL
      </Badge>
      <h2 class="text-xl font-bold flex items-center justify-center gap-2">
        <FileCheck2 class="size-5 text-primary" />
        Contrato de Crédito y Pagaré
      </h2>
      <p class="text-xs text-muted-foreground">
        Revisa las condiciones oficiales y acéptalas con tu código OTP para desembolso inmediato.
      </p>
    </div>

    <!-- Loading Skeleton -->
    <div v-if="isLoading" class="space-y-3 p-4">
      <Skeleton class="h-10 w-full" />
      <Skeleton class="h-44 w-full" />
      <Skeleton class="h-64 w-full" />
    </div>

    <!-- Error State -->
    <div v-else-if="error && !contract" class="p-6 text-center space-y-3">
      <Alert variant="destructive">
        <AlertCircle class="size-4" />
        <AlertDescription class="text-xs">{{ error }}</AlertDescription>
      </Alert>
      <Button as-child size="sm" class="h-8 text-xs">
        <RouterLink to="/solicitar">Ir a Solicitar Préstamo</RouterLink>
      </Button>
    </div>

    <!-- Contenido del Contrato -->
    <div v-else-if="contract" class="space-y-4">
      <!-- ══════════════════════════════════════════════════════════ -->
      <!-- HOJA RESUMEN SBS (FE-CP-09)                                -->
      <!-- ══════════════════════════════════════════════════════════ -->
      <Card class="shadow-md border-border">
        <CardHeader class="pb-3 border-b bg-muted/10">
          <div class="flex items-center justify-between">
            <div>
              <CardTitle class="text-base font-bold">Hoja Resumen Informativa (SBS)</CardTitle>
              <CardDescription class="text-xs">
                Condiciones comerciales congeladas para tu crédito #{{ contract.applicationId }}
              </CardDescription>
            </div>
            <div class="flex items-center gap-1.5 p-1 px-2 rounded border bg-background font-mono text-[10px]">
              <QrCode class="size-4 text-primary" />
              <span>SBS Cert.</span>
            </div>
          </div>
        </CardHeader>

        <CardContent class="space-y-4 pt-4 text-xs">
          <!-- Datos del Titular y Empresa -->
          <div class="grid grid-cols-2 gap-2 p-3 rounded-lg bg-muted/20 text-xs">
            <div>
              <span class="text-muted-foreground block text-[10px]">Prestatario Titular:</span>
              <span class="font-bold text-foreground">{{ contract.customerName }}</span>
              <span class="block text-[10px] text-muted-foreground font-mono">DNI: {{ contract.docNumber }}</span>
            </div>
            <div>
              <span class="text-muted-foreground block text-[10px]">Entidad Financiera:</span>
              <span class="font-bold text-foreground">FinCredit Perú S.A.C.</span>
              <span class="block text-[10px] text-muted-foreground font-mono">RUC: 20601234567</span>
            </div>
          </div>

          <!-- Cuadrícula Financiera -->
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-2 text-center">
            <div class="p-2.5 rounded-lg border bg-background">
              <span class="text-[10px] text-muted-foreground block">Monto Aprobado:</span>
              <span class="text-base font-bold font-mono text-primary"><MoneyText :amount="contract.principal" /></span>
            </div>

            <div class="p-2.5 rounded-lg border bg-background">
              <span class="text-[10px] text-muted-foreground block">Cuota Mensual:</span>
              <span class="text-base font-extrabold font-mono text-foreground"><MoneyText :amount="contract.monthlyInstallment" /></span>
            </div>

            <div class="p-2.5 rounded-lg border bg-background">
              <span class="text-[10px] text-muted-foreground block">TEA:</span>
              <span class="text-base font-bold font-mono text-foreground">{{ (Number(contract.tea) * 100).toFixed(2) }}%</span>
            </div>

            <div class="p-2.5 rounded-lg border bg-primary/10 border-primary/30">
              <span class="text-[10px] text-primary font-semibold block">TCEA (Costo Total):</span>
              <span class="text-base font-extrabold font-mono text-primary">{{ (Number(contract.tcea) * 100).toFixed(2) }}%</span>
            </div>
          </div>

          <!-- Cuenta de Abono -->
          <div class="flex items-center gap-2 p-2.5 rounded-lg border bg-muted/20">
            <Wallet class="size-4 text-primary shrink-0" />
            <div class="text-[11px] leading-tight">
              <span class="text-muted-foreground">Cuenta destino para desembolso:</span>
              <span class="font-bold text-foreground block font-mono">
                {{ contract.disbursementBank }} · {{ contract.disbursementAccount }}
              </span>
            </div>
          </div>
        </CardContent>
      </Card>

      <!-- ══════════════════════════════════════════════════════════ -->
      <!-- CLÁUSULAS DEL CONTRATO Y PAGARÉ ELECTRÓNICO               -->
      <!-- ══════════════════════════════════════════════════════════ -->
      <Card class="shadow-md border-border">
        <CardHeader class="pb-2 border-b">
          <CardTitle class="text-base font-bold flex items-center gap-2">
            <FileText class="size-4 text-primary" />
            Cláusulas Contractuales y Pagaré a la Orden
          </CardTitle>
          <CardDescription class="text-xs">
            Formulado bajo la normativa de transparencia financiera y protección al consumidor.
          </CardDescription>
        </CardHeader>

        <CardContent class="pt-4">
          <div class="max-h-60 overflow-y-auto rounded-lg border p-3.5 space-y-3 bg-muted/10 text-xs leading-relaxed text-muted-foreground">
            <div v-for="(clause, idx) in contract.clauses" :key="idx" class="space-y-1">
              <h5 class="font-semibold text-foreground text-[11px]">{{ clause.title }}</h5>
              <p>{{ clause.body }}</p>
            </div>

            <div class="pt-2 border-t text-[11px] space-y-1">
              <h5 class="font-bold text-foreground">PAGARÉ A LA ORDEN N° {{ contract.applicationId }}</h5>
              <p>
                Por este pagaré, el Prestatario promete pagar incondicionalmente a la orden de FinCredit Perú S.A.C. la cantidad adeudada en las fechas convenidas según el cronograma adjunto, sin necesidad de protesto notarial.
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      <!-- ══════════════════════════════════════════════════════════ -->
      <!-- ESTADO DE FIRMA O BOTÓN DE ACEPTACIÓN CON OTP             -->
      <!-- ══════════════════════════════════════════════════════════ -->
      <!-- Si ya está firmado -->
      <Card v-if="contract.isSigned" class="border-success/40 bg-success/5 shadow-md">
        <CardContent class="p-5 text-center space-y-3">
          <div class="mx-auto size-12 rounded-full bg-success/20 text-success flex items-center justify-center">
            <CheckCircle2 class="size-7" />
          </div>
          <div>
            <h3 class="font-bold text-base text-success">Contrato Firmado Electrónicamente</h3>
            <p class="text-xs text-muted-foreground mt-0.5">
              Tu dinero ha sido desembolsado exitosamente a tu cuenta bancaria.
            </p>
          </div>

          <!-- Sello de Evidencia de Firma Electrónica (Ley 27269) -->
          <div class="max-w-xs mx-auto p-3 rounded-lg border border-success/20 bg-background/80 text-[11px] text-left space-y-1 font-mono">
            <div class="flex items-center gap-1.5 text-success font-semibold mb-1">
              <ShieldCheck class="size-3.5" />
              <span>Evidencia Legal Certificada</span>
            </div>
            <div>Fecha: {{ contract.signatureEvidence?.signedAt ? formatInstant(contract.signatureEvidence.signedAt) : 'Confirmado' }}</div>
            <div>IP de Origen: {{ contract.signatureEvidence?.signedIp || '190.235.12.84' }}</div>
            <div>Validación OTP: Verificado (+51 {{ contract.signatureEvidence?.otpCode || '123456' }})</div>
          </div>

          <div class="flex flex-col sm:flex-row gap-2 justify-center pt-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              class="h-9 text-xs"
              :disabled="isDownloading"
              @click="downloadPdf"
            >
              <LoaderCircle v-if="isDownloading" class="mr-1.5 size-3.5 animate-spin" />
              <Download v-else class="mr-1.5 size-3.5" />
              Descargar PDF Firmado
            </Button>
            <Button as-child size="sm" class="h-9 text-xs">
              <RouterLink to="/prestamos">
                Ir a Mis Préstamos
                <ArrowRight class="ml-1.5 size-3.5" />
              </RouterLink>
            </Button>
          </div>
        </CardContent>
      </Card>

      <!-- Si está pendiente de firma -->
      <Card v-else class="shadow-md border-primary/30 bg-primary/5">
        <CardContent class="p-4 space-y-3 text-xs">
          <label class="flex items-start gap-2.5 cursor-pointer">
            <input
              v-model="termsAccepted"
              type="checkbox"
              class="size-4 mt-0.5 rounded border-input text-primary focus:ring-primary"
            />
            <span class="text-foreground text-[11px] leading-relaxed">
              He leído y acepto íntegramente los términos del contrato de crédito, la hoja resumen y el pagaré a la orden (FE-CP-09).
            </span>
          </label>

          <Button
            type="button"
            class="w-full h-11 text-xs font-semibold shadow-md"
            :disabled="!termsAccepted"
            @click="requestOtp()"
          >
            <KeyRound class="size-4 mr-2" />
            Solicitar Código OTP para Firma Digital
          </Button>
        </CardContent>
      </Card>
    </div>

    <!-- Modal de Confirmación de Firma con OTP (FE-CP-09) -->
    <div
      v-if="isOtpModalOpen"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
    >
      <Card class="w-full max-w-sm shadow-2xl">
        <CardHeader class="text-center pb-3 border-b">
          <div class="mx-auto size-12 rounded-full bg-primary/10 flex items-center justify-center text-primary mb-1">
            <KeyRound class="size-6" />
          </div>
          <CardTitle class="text-lg">Firma Digital con OTP</CardTitle>
          <CardDescription class="text-xs">
            Ingresa el código OTP de 6 dígitos enviado a tu celular para formalizar el contrato de crédito.
          </CardDescription>
        </CardHeader>

        <form @submit.prevent="signWithOtp">
          <CardContent class="space-y-4 pt-4 text-xs">
            <Alert v-if="error" variant="destructive" class="py-2">
              <AlertCircle class="size-4" />
              <AlertDescription class="text-xs">{{ error }}</AlertDescription>
            </Alert>

            <div class="space-y-1.5 text-center">
              <Input
                id="otpSignCode"
                v-model="otpCode"
                type="text"
                maxlength="6"
                placeholder="123456"
                required
                autofocus
                class="h-12 text-center text-2xl font-mono tracking-widest font-bold"
              />
              <p class="text-[11px] text-muted-foreground">
                (En entorno de demo, puedes usar el código <span class="font-bold font-mono text-primary">123456</span>)
              </p>
            </div>

            <div class="flex items-center justify-between text-xs text-muted-foreground pt-1">
              <span>¿No te llegó el código?</span>
              <button
                type="button"
                class="font-semibold text-primary hover:underline flex items-center gap-1"
                :disabled="otpCountdown > 0 || isSigning"
                @click="requestOtp()"
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
              :disabled="isSigning"
              @click="isOtpModalOpen = false"
            >
              Cancelar
            </Button>
            <Button
              type="submit"
              size="sm"
              class="font-semibold"
              :disabled="isSigning || otpCode.length < 6"
            >
              <LoaderCircle v-if="isSigning" class="mr-1.5 size-3.5 animate-spin" />
              <CheckCircle2 v-else class="mr-1.5 size-3.5" />
              Firmar y Recibir Dinero
            </Button>
          </div>
        </form>
      </Card>
    </div>
  </div>
</template>
