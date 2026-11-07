<script setup lang="ts">
import { ref } from 'vue'
import {
  AlertCircle,
  BookOpen,
  CheckCircle2,
  LoaderCircle,
  Mail,
  Printer,
  Search,
  Send,
} from '@lucide/vue'

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
import { formatInstant } from '@prestamos/utils'

import { useComplaints } from '../composables/useComplaints'

const activeTab = ref<'FORM' | 'QUERY'>('FORM')

const {
  form,
  isSubmitting,
  isQuerying,
  errorMessage,
  registeredRecord,
  queriedCorrelative,
  queriedRecord,
  queryError,
  submitClaim,
  queryByCorrelative,
  resetForm,
} = useComplaints()

const handlePrint = () => {
  window.print()
}
</script>

<template>
  <div class="space-y-6">
    <!-- Encabezado Oficial Libro de Reclamaciones (Ley 29571 / Indecopi) -->
    <div class="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm space-y-4">
      <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between border-b border-slate-100 pb-4">
        <div class="flex items-center gap-3">
          <div class="flex h-12 w-12 items-center justify-center rounded-xl bg-slate-900 text-white">
            <BookOpen class="h-6 w-6" />
          </div>
          <div>
            <h1 class="text-xl font-bold tracking-tight text-slate-900 sm:text-2xl">
              Libro de Reclamaciones Virtual
            </h1>
            <p class="text-xs text-slate-500">
              Conforme a lo dispuesto en la Ley N° 29571 - Código de Protección y Defensa del Consumidor
            </p>
          </div>
        </div>

        <!-- Membrete institucional -->
        <div class="rounded-xl bg-slate-50 p-3 text-right text-xs text-slate-600 space-y-0.5 border border-slate-100">
          <p class="font-bold text-slate-900">PRESTAMOS PERU S.A.C.</p>
          <p>RUC: 20601234567</p>
          <p class="text-[11px] text-slate-400">Av. Las Camelias 820, San Isidro, Lima</p>
        </div>
      </div>

      <!-- Pestañas de modo -->
      <div class="flex items-center gap-2">
        <button
          type="button"
          :class="[
            activeTab === 'FORM'
              ? 'bg-slate-900 text-white'
              : 'bg-slate-100 text-slate-600 hover:bg-slate-200',
            'rounded-lg px-3 py-1.5 text-xs font-semibold transition-colors',
          ]"
          @click="activeTab = 'FORM'"
        >
          Registrar Reclamación
        </button>
        <button
          type="button"
          :class="[
            activeTab === 'QUERY'
              ? 'bg-slate-900 text-white'
              : 'bg-slate-100 text-slate-600 hover:bg-slate-200',
            'rounded-lg px-3 py-1.5 text-xs font-semibold transition-colors',
          ]"
          @click="activeTab = 'QUERY'"
        >
          Consultar Estado de Reclamación
        </button>
      </div>
    </div>

    <!-- PESTAÑA A: REGISTRO DE RECLAMACIÓN -->
    <div v-if="activeTab === 'FORM'" class="space-y-6">
      <!-- RESULTADO TRAS EL REGISTRO CON ÉXITO -->
      <div v-if="registeredRecord" class="mx-auto max-w-2xl">
        <Card class="border-emerald-200 bg-gradient-to-b from-emerald-50/50 to-white shadow-md">
          <CardHeader class="text-center pb-2">
            <div class="mx-auto mb-2 flex h-14 w-14 items-center justify-center rounded-full bg-emerald-100 text-emerald-600">
              <CheckCircle2 class="h-8 w-8" />
            </div>
            <CardTitle class="text-xl text-emerald-950">Hoja de Reclamación Registrada</CardTitle>
            <CardDescription class="text-emerald-800">
              Hemos recibido tu {{ registeredRecord.claimType.toLowerCase() }} conforme a ley.
            </CardDescription>
          </CardHeader>

          <CardContent class="space-y-4 pt-2">
            <div class="rounded-xl border border-emerald-100 bg-white p-5 space-y-3 text-xs">
              <div class="flex justify-between border-b border-slate-100 pb-2">
                <span class="text-slate-500">N° de Hoja de Reclamación:</span>
                <span class="font-mono text-base font-extrabold text-primary">
                  {{ registeredRecord.correlativeNumber }}
                </span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-500">Tipo de Reclamación:</span>
                <Badge class="bg-slate-900 text-white">{{ registeredRecord.claimType }}</Badge>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-500">Consumidor:</span>
                <span class="font-medium text-slate-900">{{ registeredRecord.consumerFullName }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-500">Documento:</span>
                <span class="text-slate-800">{{ registeredRecord.docType }} {{ registeredRecord.docNumber }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-500">Fecha y hora de registro:</span>
                <span class="text-slate-800">{{ formatInstant(registeredRecord.createdAt) }}</span>
              </div>
              <div class="flex justify-between border-t border-slate-100 pt-2 text-emerald-900 font-semibold">
                <span>Plazo legal máximo de respuesta:</span>
                <span>15 días hábiles</span>
              </div>
            </div>

            <div class="rounded-lg bg-emerald-100/60 p-3 text-xs text-emerald-900 flex items-center gap-2">
              <Mail class="h-4 w-4 shrink-0 text-emerald-700" />
              <span>
                Se ha enviado una copia íntegra de esta constancia al correo <strong>{{ registeredRecord.email }}</strong>.
              </span>
            </div>
          </CardContent>

          <CardFooter class="flex flex-col gap-2 sm:flex-row sm:justify-between border-t border-slate-100 pt-4">
            <Button variant="outline" size="sm" @click="handlePrint">
              <Printer class="mr-1.5 h-4 w-4" />
              <span>Imprimir Constancia</span>
            </Button>
            <Button size="sm" @click="resetForm">
              Registrar otra reclamación
            </Button>
          </CardFooter>
        </Card>
      </div>

      <!-- FORMULARIO OFICIAL -->
      <Card v-else class="max-w-4xl mx-auto shadow-sm">
        <CardHeader class="pb-4 border-b border-slate-100">
          <CardTitle class="text-base text-slate-900">
            Formulario de Hoja de Reclamación
          </CardTitle>
          <CardDescription class="text-xs">
            Todos los campos con (*) son obligatorios según el Reglamento del Libro de Reclamaciones.
          </CardDescription>
        </CardHeader>

        <CardContent class="space-y-6 pt-5">
          <!-- 1. TIPO DE RECLAMACIÓN (RECLAMO VS QUEJA - DEFINICIONES LEGALES) -->
          <div class="space-y-2">
            <span class="text-xs font-bold uppercase tracking-wider text-slate-700 block">
              1. Tipo de Reclamación (*)
            </span>

            <div class="grid gap-3 sm:grid-cols-2">
              <label
                :class="[
                  form.claimType === 'RECLAMO'
                    ? 'border-primary bg-primary/5 ring-2 ring-primary'
                    : 'border-slate-200 bg-white hover:bg-slate-50',
                  'flex items-start gap-3 rounded-xl border p-3.5 cursor-pointer transition-all',
                ]"
              >
                <input
                  v-model="form.claimType"
                  type="radio"
                  value="RECLAMO"
                  class="mt-1 text-primary focus:ring-primary"
                />
                <div class="space-y-0.5 text-xs">
                  <span class="font-bold text-slate-900 block">RECLAMO</span>
                  <span class="text-slate-500 leading-relaxed block text-[11px]">
                    Disconformidad relacionada a los productos o servicios ofrecidos (intereses, cronograma, cobros).
                  </span>
                </div>
              </label>

              <label
                :class="[
                  form.claimType === 'QUEJA'
                    ? 'border-primary bg-primary/5 ring-2 ring-primary'
                    : 'border-slate-200 bg-white hover:bg-slate-50',
                  'flex items-start gap-3 rounded-xl border p-3.5 cursor-pointer transition-all',
                ]"
              >
                <input
                  v-model="form.claimType"
                  type="radio"
                  value="QUEJA"
                  class="mt-1 text-primary focus:ring-primary"
                />
                <div class="space-y-0.5 text-xs">
                  <span class="font-bold text-slate-900 block">QUEJA</span>
                  <span class="text-slate-500 leading-relaxed block text-[11px]">
                    Malestar o descontento respecto a la atención al público brindada por nuestros canales o personal.
                  </span>
                </div>
              </label>
            </div>
          </div>

          <!-- 2. IDENTIFICACIÓN DEL CONSUMIDOR RECLAMANTE -->
          <div class="space-y-3 border-t border-slate-100 pt-5">
            <span class="text-xs font-bold uppercase tracking-wider text-slate-700 block">
              2. Identificación del Consumidor Reclamante (*)
            </span>

            <div class="grid gap-3 sm:grid-cols-3">
              <div class="sm:col-span-2">
                <label class="block text-xs font-semibold text-slate-600 mb-1">Nombres y Apellidos (*)</label>
                <input
                  v-model="form.consumerFullName"
                  type="text"
                  placeholder="Ej. María Quispe Huamán"
                  class="w-full rounded-lg border border-slate-300 p-2 text-xs"
                />
              </div>

              <div>
                <label class="block text-xs font-semibold text-slate-600 mb-1">Tipo de Documento</label>
                <select v-model="form.docType" class="w-full rounded-lg border border-slate-300 p-2 text-xs">
                  <option value="DNI">DNI</option>
                  <option value="CE">Carné de Extranjería</option>
                  <option value="PASAPORTE">Pasaporte</option>
                  <option value="RUC">RUC</option>
                </select>
              </div>
            </div>

            <div class="grid gap-3 sm:grid-cols-3">
              <div>
                <label class="block text-xs font-semibold text-slate-600 mb-1">N° de Documento (*)</label>
                <input
                  v-model="form.docNumber"
                  type="text"
                  placeholder="71234567"
                  class="w-full rounded-lg border border-slate-300 p-2 text-xs font-mono"
                />
              </div>

              <div>
                <label class="block text-xs font-semibold text-slate-600 mb-1">Teléfono o Celular (*)</label>
                <input
                  v-model="form.phone"
                  type="tel"
                  placeholder="987654321"
                  class="w-full rounded-lg border border-slate-300 p-2 text-xs"
                />
              </div>

              <div>
                <label class="block text-xs font-semibold text-slate-600 mb-1">Correo Electrónico (*)</label>
                <input
                  v-model="form.email"
                  type="email"
                  placeholder="cliente@ejemplo.com"
                  class="w-full rounded-lg border border-slate-300 p-2 text-xs"
                />
              </div>
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-600 mb-1">Domicilio Actual</label>
              <input
                v-model="form.address"
                type="text"
                placeholder="Av. Los Pinos 123, Urb. San Antonio, Lima"
                class="w-full rounded-lg border border-slate-300 p-2 text-xs"
              />
            </div>
          </div>

          <!-- 3. IDENTIFICACIÓN DEL BIEN CONTRATADO -->
          <div class="space-y-3 border-t border-slate-100 pt-5">
            <span class="text-xs font-bold uppercase tracking-wider text-slate-700 block">
              3. Identificación del Bien Contratado
            </span>

            <div class="grid gap-3 sm:grid-cols-2">
              <div>
                <label class="block text-xs font-semibold text-slate-600 mb-1">Tipo de Bien</label>
                <select v-model="form.contractedGood" class="w-full rounded-lg border border-slate-300 p-2 text-xs">
                  <option value="PRODUCTO">Producto (Préstamo o Crédito)</option>
                  <option value="SERVICIO">Servicio (Plataforma, Cobranza o Atención)</option>
                </select>
              </div>

              <div>
                <label class="block text-xs font-semibold text-slate-600 mb-1">Monto Reclamado en Soles (opcional)</label>
                <input
                  v-model="form.amountClaimed"
                  type="number"
                  placeholder="0.00"
                  class="w-full rounded-lg border border-slate-300 p-2 text-xs"
                />
              </div>
            </div>
          </div>

          <!-- 4. DETALLE DE LA RECLAMACIÓN Y PEDIDO DEL CONSUMIDOR -->
          <div class="space-y-3 border-t border-slate-100 pt-5">
            <span class="text-xs font-bold uppercase tracking-wider text-slate-700 block">
              4. Detalle de la Reclamación y Pedido Concreto (*)
            </span>

            <div>
              <label class="block text-xs font-semibold text-slate-600 mb-1">Detalle del Reclamo o Queja (*)</label>
              <textarea
                v-model="form.description"
                rows="4"
                placeholder="Describe los hechos y circunstancias que motivan tu reclamo..."
                class="w-full rounded-lg border border-slate-300 p-2 text-xs"
              />
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-600 mb-1">Pedido del Consumidor (*)</label>
              <textarea
                v-model="form.consumerOrder"
                rows="2"
                placeholder="Indica qué solución o acción solicitas concretamente a la empresa..."
                class="w-full rounded-lg border border-slate-300 p-2 text-xs"
              />
            </div>
          </div>

          <!-- Error banner -->
          <div
            v-if="errorMessage"
            class="rounded-lg border border-rose-200 bg-rose-50 p-3 text-xs text-rose-800 flex items-center gap-2"
          >
            <AlertCircle class="h-4 w-4 shrink-0 text-rose-600" />
            <span>{{ errorMessage }}</span>
          </div>
        </CardContent>

        <CardFooter class="flex flex-col gap-3 sm:flex-row sm:justify-between border-t border-slate-100 pt-4">
          <p class="text-[11px] text-slate-500">
            La formulación del reclamo no impide acudir a otras vías de solución de controversias ni es requisito previo para denunciar ante el INDECOPI.
          </p>
          <Button :disabled="isSubmitting" @click="submitClaim">
            <LoaderCircle v-if="isSubmitting" class="mr-1.5 h-4 w-4 animate-spin" />
            <Send v-else class="mr-1.5 h-4 w-4" />
            <span>Registrar Hoja de Reclamación</span>
          </Button>
        </CardFooter>
      </Card>
    </div>

    <!-- PESTAÑA B: CONSULTAR ESTADO DE RECLAMACIÓN -->
    <div v-else-if="activeTab === 'QUERY'" class="max-w-2xl mx-auto space-y-6">
      <Card>
        <CardHeader class="pb-3">
          <CardTitle class="text-base flex items-center gap-2">
            <Search class="h-4 w-4 text-primary" />
            <span>Consultar Estado de tu Reclamo</span>
          </CardTitle>
          <CardDescription>
            Ingresa el código correlativo de tu hoja de reclamación (ej: LR-2026-00001).
          </CardDescription>
        </CardHeader>

        <CardContent class="space-y-4">
          <div class="flex gap-2">
            <input
              v-model="queriedCorrelative"
              type="text"
              placeholder="LR-2026-00001"
              class="flex-1 rounded-lg border border-slate-300 p-2 text-xs font-mono uppercase"
              @keyup.enter="queryByCorrelative(queriedCorrelative)"
            />
            <Button
              size="sm"
              :disabled="isQuerying || !queriedCorrelative.trim()"
              @click="queryByCorrelative(queriedCorrelative)"
            >
              <LoaderCircle v-if="isQuerying" class="mr-1.5 h-4 w-4 animate-spin" />
              <Search v-else class="mr-1.5 h-4 w-4" />
              <span>Consultar</span>
            </Button>
          </div>

          <div v-if="queryError" class="rounded-lg border border-rose-200 bg-rose-50 p-3 text-xs text-rose-800">
            {{ queryError }}
          </div>

          <!-- Resultado de consulta -->
          <div v-if="queriedRecord" class="rounded-xl border border-slate-200 bg-slate-50 p-4 space-y-3 text-xs">
            <div class="flex justify-between items-center border-b border-slate-200 pb-2">
              <span class="font-mono font-bold text-slate-900 text-sm">
                {{ queriedRecord.correlativeNumber }}
              </span>
              <Badge class="bg-primary text-primary-foreground">
                {{ queriedRecord.status }}
              </Badge>
            </div>

            <div class="space-y-1.5 text-slate-700">
              <p><strong>Tipo:</strong> {{ queriedRecord.claimType }}</p>
              <p><strong>Reclamante:</strong> {{ queriedRecord.consumerFullName }} ({{ queriedRecord.docType }} {{ queriedRecord.docNumber }})</p>
              <p><strong>Fecha de Ingreso:</strong> {{ formatInstant(queriedRecord.createdAt) }}</p>
              <p><strong>Fecha Límite de Atención:</strong> {{ formatInstant(queriedRecord.responseDeadline) }}</p>
              <div class="border-t border-slate-200 pt-2 text-[11px] text-slate-500">
                <p><strong>Detalle:</strong> {{ queriedRecord.description }}</p>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  </div>
</template>
