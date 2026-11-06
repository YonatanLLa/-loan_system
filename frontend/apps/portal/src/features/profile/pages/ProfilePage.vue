<script setup lang="ts">
import { computed, ref } from 'vue'
import {
  AlertCircle,
  AlertTriangle,
  ArrowRight,
  Banknote,
  Building,
  CheckCircle2,
  Clock,
  CreditCard,
  HelpCircle,
  Info,
  KeyRound,
  LoaderCircle,
  Mail,
  Phone,
  Plus,
  RefreshCw,
  ShieldAlert,
  ShieldCheck,
  User,
  Wallet,
} from '@lucide/vue'

import { StatusBadge } from '@prestamos/ui'
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
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import { formatInstant } from '@prestamos/utils'

import { useProfile } from '../composables/useProfile'

const {
  profile,
  bankAccounts,
  refunds,
  primaryAccount,
  coolingOffAccount,
  isLoading,
  isUpdatingAccount,
  isRequestingRefund,
  errorMessage,
  successMessage,
  loadData,
  changePrimaryAccount,
  submitRefundRequest,
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
  const res = await submitRefundRequest(num.toFixed(2), refundBankAccountId.value)
  if (res) showRefundModal.value = false
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">
          Mi Perfil y Cuentas
        </h1>
        <p class="text-sm text-slate-600 sm:text-base">
          Gestiona tus datos personales, tus cuentas de desembolso y el saldo a favor de tu crédito.
        </p>
      </div>

      <Button variant="outline" size="sm" :disabled="isLoading" @click="loadData">
        <RefreshCw :class="['mr-2 h-4 w-4', isLoading && 'animate-spin']" />
        <span>Actualizar</span>
      </Button>
    </div>

    <!-- Feedback banners -->
    <div
      v-if="successMessage"
      class="flex items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-xs font-medium text-emerald-800"
    >
      <CheckCircle2 class="h-4 w-4 shrink-0 text-emerald-600" />
      <span>{{ successMessage }}</span>
    </div>

    <div
      v-if="errorMessage"
      class="flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 p-4 text-xs font-medium text-rose-800"
    >
      <AlertCircle class="h-4 w-4 shrink-0 text-rose-600" />
      <span>{{ errorMessage }}</span>
    </div>

    <div class="grid gap-6 lg:grid-cols-3">
      <!-- COLUMNA IZQUIERDA: DATOS PERSONALES -->
      <Card class="lg:col-span-1">
        <CardHeader class="pb-3">
          <div class="flex items-center justify-between">
            <CardTitle class="text-base flex items-center gap-2">
              <User class="h-4 w-4 text-brand-600" />
              <span>Datos Personales</span>
            </CardTitle>
            <Badge
              v-if="profile?.kycStatus === 'VERIFIED'"
              class="bg-emerald-100 text-emerald-800"
            >
              KYC Verificado
            </Badge>
          </div>
          <CardDescription>Información registrada en tu onboarding.</CardDescription>
        </CardHeader>

        <CardContent class="space-y-4 text-xs">
          <div class="space-y-1">
            <span class="text-slate-400 uppercase tracking-wider text-[10px] font-bold">Nombre Completo</span>
            <p class="font-semibold text-slate-900 text-sm">{{ profile?.fullName ?? 'María Quispe' }}</p>
          </div>

          <div class="grid grid-cols-2 gap-2 border-t border-slate-100 pt-3">
            <div>
              <span class="text-slate-400 uppercase tracking-wider text-[10px] font-bold">Documento</span>
              <p class="font-medium text-slate-800">{{ profile?.docType }} {{ profile?.docNumber }}</p>
            </div>
            <div>
              <span class="text-slate-400 uppercase tracking-wider text-[10px] font-bold">Teléfono</span>
              <p class="font-medium text-slate-800">{{ profile?.phone }}</p>
            </div>
          </div>

          <div class="space-y-1 border-t border-slate-100 pt-3">
            <span class="text-slate-400 uppercase tracking-wider text-[10px] font-bold">Correo Electrónico</span>
            <p class="font-medium text-slate-800">{{ profile?.email }}</p>
          </div>

          <div class="grid grid-cols-2 gap-2 border-t border-slate-100 pt-3">
            <div>
              <span class="text-slate-400 uppercase tracking-wider text-[10px] font-bold">Ocupación</span>
              <p class="font-medium text-slate-800">{{ profile?.occupation ?? 'Comerciante' }}</p>
            </div>
            <div>
              <span class="text-slate-400 uppercase tracking-wider text-[10px] font-bold">Empleador</span>
              <p class="font-medium text-slate-800">{{ profile?.employerName ?? 'Independiente' }}</p>
            </div>
          </div>

          <div class="rounded-lg bg-slate-50 p-3 border border-slate-100 text-[11px] text-slate-500">
            <ShieldCheck class="h-4 w-4 text-emerald-600 inline mr-1" />
            Identidad validada con RENIEC y biometría facial bajo Ley 29733.
          </div>
        </CardContent>
      </Card>

      <!-- COLUMNA CENTRAL Y DERECHA: CUENTAS BANCARIAS Y SALDO A FAVOR -->
      <div class="lg:col-span-2 space-y-6">
        <!-- 1. CUENTAS BANCARIAS DE DESEMBOLSO (FE-CP-18) -->
        <Card>
          <CardHeader class="pb-3">
            <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <CardTitle class="text-base flex items-center gap-2">
                  <Building class="h-4 w-4 text-brand-600" />
                  <span>Cuentas de Desembolso</span>
                </CardTitle>
                <CardDescription>
                  Cuentas autorizadas para recibir desembolsos y transferencias.
                </CardDescription>
              </div>

              <Button size="sm" @click="openChangeAccountModal">
                <Plus class="mr-1.5 h-4 w-4" />
                <span>Cambiar Cuenta Principal</span>
              </Button>
            </div>
          </CardHeader>

          <CardContent class="space-y-4">
            <!-- ALERTA DE PERIODO DE ENFRIAMIENTO (FE-CP-18 / ARQUITECTURA_GENERAL §9) -->
            <div
              v-if="coolingOffAccount"
              class="rounded-xl border border-amber-200 bg-amber-50 p-4 text-xs text-amber-900 space-y-1.5"
            >
              <div class="flex items-center gap-2 font-bold text-amber-950">
                <Clock class="h-4 w-4 text-amber-600" />
                <span>Periodo de Espera de Seguridad Activo (Cooling-off)</span>
              </div>
              <p class="leading-relaxed">
                Por normativa de prevención de fraudes y protección de tu cuenta (ARQUITECTURA_GENERAL §9), los cambios de cuenta principal se activan tras un periodo de espera de 24 horas.
              </p>
              <p class="font-semibold text-amber-800">
                Tu cuenta estará habilitada para desembolsos el:
                {{ coolingOffAccount.coolingOffUntil ? formatInstant(coolingOffAccount.coolingOffUntil) : 'Mañana' }}
              </p>
            </div>

            <!-- Lista de Cuentas -->
            <div class="space-y-2">
              <div
                v-for="acc in bankAccounts"
                :key="acc.id"
                :class="[
                  acc.isPrimary
                    ? 'border-brand-500 bg-brand-50/30'
                    : 'border-slate-200 bg-white',
                  'flex items-center justify-between rounded-xl border p-4 text-xs transition-all',
                ]"
              >
                <div class="space-y-1">
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-slate-900 text-sm">{{ acc.bankName }}</span>
                    <Badge v-if="acc.isPrimary" class="bg-brand-600 text-white text-[10px]">
                      Cuenta Principal
                    </Badge>
                    <Badge v-if="acc.isVerified" variant="outline" class="border-emerald-200 bg-emerald-50 text-emerald-700 text-[10px]">
                      Verificada
                    </Badge>
                  </div>
                  <p class="text-slate-600 font-mono text-[11px]">
                    {{ acc.phoneWallet ? `Billetera: ${acc.phoneWallet}` : `CCI: ${acc.cci ?? acc.accountNumberMasked}` }}
                  </p>
                  <p class="text-[10px] text-slate-400 capitalize">
                    Tipo: {{ acc.accountType.replace('_', ' ').toLowerCase() }}
                  </p>
                </div>

                <div class="text-right">
                  <span
                    v-if="acc.isPrimary && !acc.coolingOffUntil"
                    class="text-emerald-700 font-semibold text-[11px] flex items-center gap-1"
                  >
                    <CheckCircle2 class="h-3.5 w-3.5" />
                    Activa para desembolso
                  </span>
                  <span
                    v-else-if="acc.isPrimary && acc.coolingOffUntil"
                    class="text-amber-700 font-medium text-[11px] flex items-center gap-1"
                  >
                    <Clock class="h-3.5 w-3.5" />
                    En enfriamiento
                  </span>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- 2. SALDO A FAVOR Y DEVOLUCIONES (FE-CP-19 / RF-17.3) -->
        <Card>
          <CardHeader class="pb-3">
            <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <CardTitle class="text-base flex items-center gap-2">
                  <Banknote class="h-4 w-4 text-emerald-600" />
                  <span>Saldo a Favor y Devoluciones</span>
                </CardTitle>
                <CardDescription>
                  Si pagaste un monto mayor al saldo de tu crédito, puedes solicitar la devolución de tu dinero.
                </CardDescription>
              </div>

              <Button
                :disabled="!profile || parseFloat(profile.creditBalance) <= 0"
                variant="default"
                size="sm"
                class="bg-emerald-600 hover:bg-emerald-700"
                @click="openRefundModal"
              >
                <ArrowRight class="mr-1.5 h-4 w-4" />
                <span>Solicitar Devolución</span>
              </Button>
            </div>
          </CardHeader>

          <CardContent class="space-y-4">
            <!-- Saldo disponible -->
            <div class="rounded-xl border border-emerald-100 bg-emerald-50/60 p-4 flex items-center justify-between">
              <div>
                <span class="text-xs uppercase font-bold text-emerald-800 tracking-wider">
                  Saldo a favor disponible
                </span>
                <p class="text-2xl font-extrabold text-emerald-900">
                  S/ {{ profile?.creditBalance ?? '0.00' }}
                </p>
              </div>
              <div class="text-right text-xs text-emerald-700 max-w-xs">
                Transferible directamente a tu cuenta bancaria verificada según Ley 29571.
              </div>
            </div>

            <!-- Historial de Devoluciones -->
            <div class="space-y-2">
              <span class="text-xs font-semibold text-slate-700">Solicitudes de Devolución Recientes</span>

              <div v-if="refunds.length === 0" class="rounded-lg border border-dashed border-slate-200 p-4 text-center text-xs text-slate-400">
                No tienes solicitudes de devolución pendientes.
              </div>

              <div
                v-for="ref in refunds"
                :key="ref.id"
                class="flex items-center justify-between rounded-lg border border-slate-200 p-3 text-xs"
              >
                <div>
                  <span class="font-bold text-slate-900">S/ {{ ref.amount }}</span>
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
          <div class="rounded-xl border border-brand-200 bg-brand-50/50 p-3 space-y-2">
            <div class="flex items-center gap-1.5 font-bold text-brand-900">
              <KeyRound class="h-4 w-4 text-brand-600" />
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
              class="w-full rounded-lg border border-brand-300 bg-white p-2 text-center text-lg font-bold tracking-widest text-slate-900 focus:border-brand-500 focus:outline-none"
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
                class="w-full rounded-lg border border-slate-300 py-2.5 pl-9 pr-3 text-base font-bold text-slate-900 focus:border-brand-500"
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
