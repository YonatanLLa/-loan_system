<script setup lang="ts">
import { ref } from 'vue'
import {
  AlertCircle,
  ArrowRight,
  Banknote,
  Building,
  CheckCircle2,
  Clock,
  KeyRound,
  LoaderCircle,
  Mail,
  Phone,
  Plus,
  RefreshCw,
  ShieldAlert,
  ShieldCheck,
  User,
} from '@lucide/vue'

import { Badge } from '@prestamos/ui/components/ui/badge'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { formatInstant } from '@prestamos/utils'

import { useProfile } from '../composables/useProfile'

const {
  profile,
  bankAccounts,
  refunds,
  primaryAccount,
  coolingOffAccount,
  hasMarketingConsent,
  contactOtpIssued,
  isVerifyingContact,
  isLoading,
  isUpdatingAccount,
  isRequestingRefund,
  errorMessage,
  successMessage,
  loadData,
  changePrimaryAccount,
  submitRefundRequest,
  startContactOtp,
  confirmContactOtp,
  toggleMarketing,
} = useProfile()

// Modales y formularios
const showChangeAccountModal = ref(false)
const showRefundModal = ref(false)

const accountMode = ref<'EXISTING' | 'NEW'>('EXISTING')
const selectedExistingAccountId = ref<string>('')
const newBankName = ref('BCP')
const newAccountType = ref<'CUENTA_AHORRO' | 'CUENTA_CORRIENTE' | 'BILLETERA_DIGITAL'>('CUENTA_AHORRO')
const newCci = ref('')
const newPhoneWallet = ref('')
const otpCode = ref('')
const accountFormError = ref<string | null>(null)
const contactOtpCode = ref('')

const refundAmount = ref('')
const refundBankAccountId = ref('')
const refundFormError = ref<string | null>(null)

const openChangeAccountModal = () => {
  accountMode.value = 'EXISTING'
  selectedExistingAccountId.value = primaryAccount.value?.id ?? ''
  otpCode.value = '123456' // Precargado para demo fluida
  accountFormError.value = null
  showChangeAccountModal.value = true
}

const handleConfirmAccountChange = async () => {
  if (!otpCode.value || otpCode.value.length !== 6) {
    accountFormError.value = 'Debes ingresar un código OTP válido de 6 dígitos.'
    return
  }

  accountFormError.value = null

  if (accountMode.value === 'EXISTING') {
    if (!selectedExistingAccountId.value) {
      accountFormError.value = 'Selecciona una cuenta de la lista.'
      return
    }
    const res = await changePrimaryAccount({
      bankAccountId: selectedExistingAccountId.value,
      otpCode: otpCode.value,
    })
    if (res) showChangeAccountModal.value = false
  } else {
    if (newAccountType.value === 'BILLETERA_DIGITAL' && !newPhoneWallet.value) {
      accountFormError.value = 'Indica el número de teléfono de tu billetera digital.'
      return
    }
    if (newAccountType.value !== 'BILLETERA_DIGITAL' && (!newCci.value || newCci.value.length !== 20)) {
      accountFormError.value = 'El CCI debe contener exactamente 20 dígitos numéricos.'
      return
    }

    const res = await changePrimaryAccount({
      newAccount: {
        bankName: newBankName.value,
        accountType: newAccountType.value,
        cci: newAccountType.value !== 'BILLETERA_DIGITAL' ? newCci.value : undefined,
        phoneWallet: newAccountType.value === 'BILLETERA_DIGITAL' ? newPhoneWallet.value : undefined,
      },
      otpCode: otpCode.value,
    })
    if (res) showChangeAccountModal.value = false
  }
}

const handleConfirmContactOtp = async () => {
  if (contactOtpCode.value.trim().length !== 6) return
  const ok = await confirmContactOtp(contactOtpCode.value.trim())
  if (ok) contactOtpCode.value = ''
}

const openRefundModal = () => {
  if (primaryAccount.value) {
    refundBankAccountId.value = primaryAccount.value.id
  }
  refundAmount.value = profile.value?.creditBalance ?? '0.00'
  refundFormError.value = null
  showRefundModal.value = true
}

const handleConfirmRefund = async () => {
  const num = parseFloat(refundAmount.value)
  if (isNaN(num) || num <= 0) {
    refundFormError.value = 'Ingresa un monto de devolución mayor a cero.'
    return
  }
  if (!refundBankAccountId.value) {
    refundFormError.value = 'Selecciona la cuenta bancaria de destino.'
    return
  }

  refundFormError.value = null
  const res = await submitRefundRequest(refundAmount.value, refundBankAccountId.value)
  if (res) showRefundModal.value = false
}
</script>

<template>
  <div class="space-y-6">
    <!-- Encabezado -->
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
      <div>
        <h1 class="text-xl font-bold text-slate-900">Mi Perfil y Cuentas Bancarias</h1>
        <p class="text-xs text-slate-500">
          Gestiona tus cuentas de desembolso, canales verificados, privacidad LPDP y saldo a favor.
        </p>
      </div>
      <Button variant="outline" size="sm" :disabled="isLoading" @click="loadData">
        <RefreshCw class="mr-1.5 h-3.5 w-3.5" :class="{ 'animate-spin': isLoading }" />
        <span>Actualizar</span>
      </Button>
    </div>

    <!-- Mensajes de feedback -->
    <div
      v-if="errorMessage"
      class="flex items-start gap-2.5 rounded-xl border border-rose-200 bg-rose-50 p-3.5 text-xs text-rose-800"
    >
      <AlertCircle class="h-4 w-4 shrink-0 text-rose-600 mt-0.5" />
      <span>{{ errorMessage }}</span>
    </div>

    <div
      v-if="successMessage"
      class="flex items-start gap-2.5 rounded-xl border border-emerald-200 bg-emerald-50 p-3.5 text-xs text-emerald-800"
    >
      <CheckCircle2 class="h-4 w-4 shrink-0 text-emerald-600 mt-0.5" />
      <span>{{ successMessage }}</span>
    </div>

    <!-- Alerta de Periodo de Enfriamiento (Cooling-off 24h - FE-CP-18) -->
    <div
      v-if="coolingOffAccount"
      class="flex items-start gap-3 rounded-xl border border-amber-300 bg-amber-50 p-4 text-xs text-amber-900"
    >
      <Clock class="h-5 w-5 shrink-0 text-amber-600 mt-0.5" />
      <div class="space-y-1">
        <p class="font-bold">Periodo de Seguridad Activo (24 horas)</p>
        <p>
          Tu nueva cuenta principal <strong>{{ coolingOffAccount.bankName }} ({{ coolingOffAccount.accountNumberMasked }})</strong>
          se encuentra en periodo de validación por seguridad. Podrá recibir desembolsos a partir de:
          <strong>{{ formatInstant(coolingOffAccount.coolingOffUntil!) }}</strong>.
        </p>
      </div>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <!-- COLUMNA 1: Datos Personales y Saldo a Favor -->
      <div class="space-y-6">
        <!-- Datos Personales -->
        <Card v-if="profile">
          <CardHeader class="pb-3">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2 text-primary">
                <User class="h-4 w-4" />
                <CardTitle class="text-sm">Datos del Titular</CardTitle>
              </div>
              <Badge
                :class="[
                  profile.kycStatus === 'VERIFIED'
                    ? 'bg-emerald-100 text-emerald-800'
                    : 'bg-amber-100 text-amber-800',
                ]"
              >
                <ShieldCheck v-if="profile.kycStatus === 'VERIFIED'" class="mr-1 h-3 w-3" />
                {{ profile.kycStatus === 'VERIFIED' ? 'KYC Verificado' : profile.kycStatus }}
              </Badge>
            </div>
          </CardHeader>
          <CardContent class="space-y-3 text-xs">
            <div>
              <span class="text-slate-400 block">Nombre Completo</span>
              <span class="font-semibold text-slate-800">{{ profile.fullName }}</span>
            </div>
            <div class="grid grid-cols-2 gap-2">
              <div>
                <span class="text-slate-400 block">Documento</span>
                <span class="font-mono font-medium text-slate-800">
                  {{ profile.docType }} {{ profile.docNumber }}
                </span>
              </div>
              <div>
                <span class="text-slate-400 block">Teléfono</span>
                <div class="flex items-center gap-1">
                  <span class="font-mono font-medium text-slate-800">{{ profile.phone }}</span>
                  <button
                    v-if="profile.phoneVerified === false"
                    type="button"
                    class="text-[10px] text-primary underline"
                    @click="startContactOtp('SMS')"
                  >
                    <Phone class="inline h-3 w-3" /> Verificar
                  </button>
                </div>
              </div>
            </div>
            <div>
              <span class="text-slate-400 block">Correo Electrónico</span>
              <div class="flex items-center justify-between">
                <span class="font-medium text-slate-800">{{ profile.email }}</span>
                <button
                  v-if="profile.emailVerified === false"
                  type="button"
                  class="text-[10px] text-primary underline"
                  @click="startContactOtp('EMAIL')"
                >
                  <Mail class="inline h-3 w-3" /> Verificar
                </button>
              </div>
            </div>
            <div v-if="profile.address">
              <span class="text-slate-400 block">Domicilio Declarado</span>
              <span class="font-medium text-slate-800">
                {{ profile.address }} {{ profile.district ? `- ${profile.district}` : '' }}
              </span>
            </div>

            <!-- Confirmación OTP de contacto inline -->
            <div
              v-if="contactOtpIssued"
              class="rounded-lg border border-primary/30 bg-primary/5 p-2.5 space-y-2"
            >
              <p class="text-[11px] text-slate-700">
                Código OTP enviado a <strong>{{ contactOtpIssued.maskedDestination }}</strong>:
              </p>
              <div class="flex items-center gap-2">
                <input
                  v-model="contactOtpCode"
                  type="text"
                  maxlength="6"
                  placeholder="123456"
                  class="w-24 rounded border border-slate-300 px-2 py-1 font-mono text-xs"
                />
                <Button
                  size="sm"
                  class="h-7 text-xs"
                  :disabled="isVerifyingContact || contactOtpCode.trim().length !== 6"
                  @click="handleConfirmContactOtp"
                >
                  Confirmar
                </Button>
              </div>
            </div>

            <!-- Preferencia LPDP Marketing -->
            <div class="flex items-center justify-between pt-2 border-t border-slate-100">
              <div>
                <span class="font-semibold text-slate-700 block">Comunicaciones Comerciales (LPDP)</span>
                <span class="text-[10px] text-slate-500">
                  {{ hasMarketingConsent ? 'Autorizado' : 'No autorizado' }}
                </span>
              </div>
              <Button variant="outline" size="sm" class="h-7 text-[11px]" @click="toggleMarketing">
                {{ hasMarketingConsent ? 'Revocar' : 'Autorizar' }}
              </Button>
            </div>
          </CardContent>
        </Card>

        <!-- Tarjeta de Saldo a Favor (FE-CP-19) -->
        <Card class="border-emerald-200 bg-gradient-to-br from-emerald-50/70 to-white">
          <CardHeader class="pb-2">
            <div class="flex items-center gap-2 text-emerald-700">
              <Banknote class="h-4 w-4" />
              <CardTitle class="text-sm">Saldo a Favor Disponible</CardTitle>
            </div>
            <CardDescription class="text-xs">
              Excedentes por pagos anticipados o redondeo a tu favor (RF-17).
            </CardDescription>
          </CardHeader>
          <CardContent class="space-y-4">
            <div class="text-2xl font-extrabold text-emerald-800">
              S/ {{ profile?.creditBalance ?? '0.00' }}
            </div>

            <Button
              v-if="parseFloat(profile?.creditBalance ?? '0') > 0"
              size="sm"
              class="w-full bg-emerald-600 hover:bg-emerald-700 text-xs"
              @click="openRefundModal"
            >
              <span>Solicitar Devolución a mi Cuenta</span>
              <ArrowRight class="ml-1.5 h-3.5 w-3.5" />
            </Button>
            <p v-else class="text-[11px] text-slate-500">
              No tienes saldo a favor pendiente de devolución en este momento.
            </p>
          </CardContent>
        </Card>
      </div>

      <!-- COLUMNA 2 y 3: Cuentas Bancarias y Solicitudes de Devolución -->
      <div class="lg:col-span-2 space-y-6">
        <!-- Cuentas Bancarias de Desembolso (FE-CP-18) -->
        <Card>
          <CardHeader class="pb-3">
            <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
              <div>
                <div class="flex items-center gap-2 text-primary">
                  <Building class="h-4 w-4" />
                  <CardTitle class="text-sm">Cuentas Bancarias y Billeteras</CardTitle>
                </div>
                <CardDescription class="text-xs">
                  El cambio de cuenta principal requiere confirmación por OTP y aplica enfriamiento de 24h.
                </CardDescription>
              </div>
              <Button size="sm" class="text-xs" @click="openChangeAccountModal">
                <Plus class="mr-1.5 h-3.5 w-3.5" />
                <span>Cambiar Cuenta Principal</span>
              </Button>
            </div>
          </CardHeader>

          <CardContent class="space-y-3">
            <div
              v-for="acc in bankAccounts"
              :key="acc.id"
              :class="[
                'flex flex-col sm:flex-row sm:items-center justify-between gap-3 rounded-xl border p-3.5 text-xs transition-all',
                acc.isPrimary
                  ? 'border-primary/40 bg-primary/5 shadow-2xs'
                  : 'border-slate-200 bg-white',
              ]"
            >
              <div class="space-y-1">
                <div class="flex items-center gap-2">
                  <span class="font-bold text-slate-900">{{ acc.bankName }}</span>
                  <Badge v-if="acc.isPrimary" class="bg-primary text-white text-[10px]">
                    Principal de Desembolso
                  </Badge>
                  <Badge
                    v-if="acc.isVerified"
                    variant="outline"
                    class="border-emerald-300 text-emerald-700 text-[10px]"
                  >
                    Verificada
                  </Badge>
                </div>
                <div class="text-slate-600 font-mono">
                  <span v-if="acc.phoneWallet">Billetera: {{ acc.phoneWallet }}</span>
                  <span v-else>Cuenta: {{ acc.accountNumberMasked }} · CCI: {{ acc.cci }}</span>
                </div>
              </div>

              <div class="text-right">
                <span class="text-[11px] uppercase font-medium text-slate-400">
                  {{ acc.accountType.replace('_', ' ') }}
                </span>
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- Historial de Solicitudes de Devolución de Saldo a Favor (FE-CP-19) -->
        <Card>
          <CardHeader class="pb-3">
            <CardTitle class="text-sm">Historial de Devoluciones de Saldo a Favor</CardTitle>
            <CardDescription class="text-xs">
              Seguimiento de tus solicitudes de abono de excedentes.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div v-if="refunds.length === 0" class="py-6 text-center text-xs text-slate-400">
              No has realizado solicitudes de devolución de saldo a favor.
            </div>
            <div v-else class="divide-y divide-slate-100 text-xs">
              <div
                v-for="ref in refunds"
                :key="ref.id"
                class="flex items-center justify-between py-3"
              >
                <div class="space-y-0.5">
                  <span class="font-bold text-slate-800">S/ {{ ref.amount }}</span>
                  <span class="text-slate-500 block text-[11px]">
                    Destino: {{ ref.bankName }} ({{ ref.accountNumberMasked }}) · {{ formatInstant(ref.requestedAt) }}
                  </span>
                </div>
                <Badge
                  :class="[
                    ref.status === 'DISBURSED' ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800',
                  ]"
                >
                  {{ ref.status === 'PENDING_APPROVAL' ? 'En proceso' : ref.status }}
                </Badge>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>

    <!-- MODAL: CAMBIAR CUENTA DE DESEMBOLSO CON OTP (FE-CP-18) -->
    <div
      v-if="showChangeAccountModal"
      class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm"
    >
      <div class="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl space-y-5">
        <div class="flex items-center justify-between border-b border-slate-100 pb-3">
          <h3 class="text-base font-bold text-slate-900 flex items-center gap-2">
            <ShieldAlert class="h-5 w-5 text-amber-600" />
            <span>Cambio Seguro de Cuenta Principal</span>
          </h3>
          <button type="button" class="text-slate-400 hover:text-slate-600" @click="showChangeAccountModal = false">
            ✕
          </button>
        </div>

        <div class="space-y-4 text-xs">
          <!-- Toggle tipo de cuenta -->
          <div class="grid grid-cols-2 gap-2">
            <button
              type="button"
              :class="[
                accountMode === 'EXISTING'
                  ? 'bg-slate-900 text-white'
                  : 'bg-slate-100 text-slate-700 hover:bg-slate-200',
                'rounded-lg py-2 font-medium transition-colors',
              ]"
              @click="accountMode = 'EXISTING'"
            >
              Cuenta Existente
            </button>
            <button
              type="button"
              :class="[
                accountMode === 'NEW'
                  ? 'bg-slate-900 text-white'
                  : 'bg-slate-100 text-slate-700 hover:bg-slate-200',
                'rounded-lg py-2 font-medium transition-colors',
              ]"
              @click="accountMode = 'NEW'"
            >
              Registrar Nueva
            </button>
          </div>

          <!-- Modo Existente -->
          <div v-if="accountMode === 'EXISTING'" class="space-y-2">
            <label class="font-semibold text-slate-700">Selecciona la nueva cuenta principal:</label>
            <select
              v-model="selectedExistingAccountId"
              class="w-full rounded-lg border border-slate-300 p-2 text-xs"
            >
              <option v-for="a in bankAccounts" :key="a.id" :value="a.id">
                {{ a.bankName }} - {{ a.phoneWallet ? `Billetera ${a.phoneWallet}` : a.accountNumberMasked }}
              </option>
            </select>
          </div>

          <!-- Modo Nueva Cuenta -->
          <div v-else class="space-y-3">
            <div>
              <label class="block font-semibold text-slate-700 mb-1">Entidad Bancaria</label>
              <select v-model="newBankName" class="w-full rounded-lg border border-slate-300 p-2 text-xs">
                <option value="BCP">Banco de Crédito BCP</option>
                <option value="BBVA">BBVA Perú</option>
                <option value="Interbank">Interbank</option>
                <option value="Scotiabank">Scotiabank</option>
                <option value="Yape">Yape</option>
                <option value="Plin">Plin</option>
              </select>
            </div>

            <div>
              <label class="block font-semibold text-slate-700 mb-1">Tipo de Cuenta</label>
              <select v-model="newAccountType" class="w-full rounded-lg border border-slate-300 p-2 text-xs">
                <option value="CUENTA_AHORRO">Cuenta de Ahorros</option>
                <option value="CUENTA_CORRIENTE">Cuenta Corriente</option>
                <option value="BILLETERA_DIGITAL">Billetera Digital (Yape / Plin)</option>
              </select>
            </div>

            <div v-if="newAccountType === 'BILLETERA_DIGITAL'">
              <label class="block font-semibold text-slate-700 mb-1">Número de Celular</label>
              <input
                v-model="newPhoneWallet"
                type="tel"
                maxlength="9"
                placeholder="987654321"
                class="w-full rounded-lg border border-slate-300 p-2 text-xs"
              />
            </div>

            <div v-else>
              <label class="block font-semibold text-slate-700 mb-1">Código de Cuenta Interbancario (CCI - 20 dígitos)</label>
              <input
                v-model="newCci"
                type="text"
                maxlength="20"
                placeholder="00219000000000010000"
                class="w-full rounded-lg border border-slate-300 p-2 font-mono text-xs"
              />
            </div>
          </div>

          <!-- Campo de OTP obligatorio (FE-CP-18) -->
          <div class="rounded-xl border border-primary/20 bg-primary/5 p-3 space-y-2">
            <div class="flex items-center gap-1.5 font-bold text-navy">
              <KeyRound class="h-4 w-4 text-primary" />
              <span>Verificación de Seguridad por OTP</span>
            </div>
            <p class="text-[11px] text-slate-600">
              Ingresa el código OTP de 6 dígitos enviado a tu número registrado (demo: <strong>123456</strong>):
            </p>
            <input
              v-model="otpCode"
              type="text"
              maxlength="6"
              placeholder="123456"
              class="w-full rounded-lg border border-primary/30 bg-white p-2 text-center text-lg font-bold tracking-widest text-slate-900 focus:border-primary focus:outline-none"
            />
          </div>

          <!-- Aviso de periodo de espera -->
          <p class="text-[11px] text-slate-500 text-center leading-relaxed">
            Aviso: Se activará un periodo de espera de 24 horas antes de que la nueva cuenta pueda recibir desembolsos.
          </p>

          <div v-if="accountFormError" class="rounded-lg border border-rose-200 bg-rose-50 p-2.5 text-[11px] text-rose-800">
            {{ accountFormError }}
          </div>
        </div>

        <div class="flex justify-end gap-2 border-t border-slate-100 pt-3">
          <Button variant="ghost" size="sm" @click="showChangeAccountModal = false">
            Cancelar
          </Button>
          <Button size="sm" :disabled="isUpdatingAccount" @click="handleConfirmAccountChange">
            <LoaderCircle v-if="isUpdatingAccount" class="mr-1.5 h-4 w-4 animate-spin" />
            <span>Confirmar Cambio</span>
          </Button>
        </div>
      </div>
    </div>

    <!-- MODAL: SOLICITAR DEVOLUCIÓN DE SALDO A FAVOR (FE-CP-19) -->
    <div
      v-if="showRefundModal"
      class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm"
    >
      <div class="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl space-y-5">
        <div class="flex items-center justify-between border-b border-slate-100 pb-3">
          <h3 class="text-base font-bold text-slate-900 flex items-center gap-2">
            <Banknote class="h-5 w-5 text-emerald-600" />
            <span>Solicitud de Devolución de Saldo a Favor</span>
          </h3>
          <button type="button" class="text-slate-400 hover:text-slate-600" @click="showRefundModal = false">
            ✕
          </button>
        </div>

        <div class="space-y-4 text-xs">
          <div>
            <label class="block font-semibold text-slate-700 mb-1">Monto a Devolver (S/)</label>
            <div class="relative">
              <span class="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3 font-semibold text-slate-500">
                S/
              </span>
              <input
                v-model="refundAmount"
                type="number"
                step="0.01"
                class="w-full rounded-lg border border-slate-300 py-2.5 pl-9 pr-3 text-base font-bold text-slate-900 focus:border-primary"
              />
            </div>
            <span class="text-[11px] text-slate-500 mt-1 block">
              Máximo disponible: S/ {{ profile?.creditBalance }}
            </span>
          </div>

          <div>
            <label class="block font-semibold text-slate-700 mb-1">Cuenta Bancaria de Destino</label>
            <select
              v-model="refundBankAccountId"
              class="w-full rounded-lg border border-slate-300 p-2 text-xs"
            >
              <option v-for="a in bankAccounts" :key="a.id" :value="a.id">
                {{ a.bankName }} - {{ a.phoneWallet ? `Billetera ${a.phoneWallet}` : a.accountNumberMasked }}
              </option>
            </select>
          </div>

          <div class="rounded-lg bg-emerald-50 p-3 border border-emerald-100 text-[11px] text-emerald-900">
            La devolución será transferida de forma directa por tesorería una vez aprobada.
          </div>

          <div v-if="refundFormError" class="rounded-lg border border-rose-200 bg-rose-50 p-2.5 text-[11px] text-rose-800">
            {{ refundFormError }}
          </div>
        </div>

        <div class="flex justify-end gap-2 border-t border-slate-100 pt-3">
          <Button variant="ghost" size="sm" @click="showRefundModal = false">
            Cancelar
          </Button>
          <Button
            size="sm"
            class="bg-emerald-600 hover:bg-emerald-700"
            :disabled="isRequestingRefund"
            @click="handleConfirmRefund"
          >
            <LoaderCircle v-if="isRequestingRefund" class="mr-1.5 h-4 w-4 animate-spin" />
            <span>Enviar Solicitud</span>
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>
