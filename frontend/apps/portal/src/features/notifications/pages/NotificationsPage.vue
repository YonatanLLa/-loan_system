<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  Bell,
  CheckCheck,
  CheckCircle2,
  Clock,
  LoaderCircle,
  Mail,
  MessageCircle,
  MessageSquare,
  RefreshCw,
  Save,
  ShieldAlert,
  Wallet,
} from '@lucide/vue'

import type { ClientNotification } from '@prestamos/api-client'
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

import { useNotifications } from '../composables/useNotifications'

const {
  notifications,
  preferences,
  unreadCount,
  isLoading,
  isSavingPreferences,
  errorMessage,
  successMessage,
  loadNotifications,
  markAsRead,
  markAllAsRead,
  savePreferences,
} = useNotifications()

const filterMode = ref<'ALL' | 'UNREAD'>('ALL')

const filteredNotifications = computed(() => {
  if (filterMode.value === 'UNREAD') {
    return notifications.value.filter((n) => !n.read)
  }
  return notifications.value
})

const getCategoryIcon = (category: ClientNotification['category']) => {
  switch (category) {
    case 'DISBURSEMENT':
      return CheckCircle2
    case 'PAYMENT':
      return Wallet
    case 'LOAN':
      return Clock
    case 'SECURITY':
      return ShieldAlert
    default:
      return Bell
  }
}

const getCategoryClass = (category: ClientNotification['category']) => {
  switch (category) {
    case 'DISBURSEMENT':
      return 'bg-success/15 text-success border-success/30'
    case 'PAYMENT':
      return 'bg-primary/10 text-primary border-primary/20'
    case 'LOAN':
      return 'bg-amber-100 text-amber-700 border-amber-200'
    case 'SECURITY':
      return 'bg-rose-100 text-rose-700 border-rose-200'
    default:
      return 'bg-slate-100 text-slate-700 border-slate-200'
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl flex items-center gap-2">
          <span>Centro de Notificaciones</span>
          <Badge v-if="unreadCount > 0" class="bg-primary text-white">
            {{ unreadCount }} nuevas
          </Badge>
        </h1>
        <p class="text-sm text-slate-600 sm:text-base">
          Avisos de tus vencimientos, desembolsos y pagos, con configuración de tus canales preferidos.
        </p>
      </div>

      <div class="flex items-center gap-2">
        <Button
          v-if="unreadCount > 0"
          variant="outline"
          size="sm"
          @click="markAllAsRead"
        >
          <CheckCheck class="mr-1.5 h-4 w-4" />
          <span>Marcar todo como leído</span>
        </Button>

        <Button variant="ghost" size="sm" :disabled="isLoading" @click="loadNotifications">
          <RefreshCw :class="['h-4 w-4', isLoading && 'animate-spin']" />
        </Button>
      </div>
    </div>

    <!-- Feedback messages -->
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
      <ShieldAlert class="h-4 w-4 shrink-0 text-rose-600" />
      <span>{{ errorMessage }}</span>
    </div>

    <div class="grid gap-6 lg:grid-cols-3">
      <!-- LISTA DE NOTIFICACIONES -->
      <div class="lg:col-span-2 space-y-4">
        <Card>
          <CardHeader class="pb-3 border-b border-slate-100">
            <div class="flex items-center justify-between">
              <CardTitle class="text-base">Bandeja de Avisos</CardTitle>
              <div class="flex items-center gap-1.5 text-xs">
                <button
                  type="button"
                  :class="[
                    filterMode === 'ALL'
                      ? 'bg-slate-900 text-white'
                      : 'bg-slate-100 text-slate-600 hover:bg-slate-200',
                    'rounded-lg px-2.5 py-1 font-medium transition-colors',
                  ]"
                  @click="filterMode = 'ALL'"
                >
                  Todas ({{ notifications.length }})
                </button>
                <button
                  type="button"
                  :class="[
                    filterMode === 'UNREAD'
                      ? 'bg-primary text-white'
                      : 'bg-primary/10 text-primary hover:bg-primary/20',
                    'rounded-lg px-2.5 py-1 font-medium transition-colors',
                  ]"
                  @click="filterMode = 'UNREAD'"
                >
                  No leídas ({{ unreadCount }})
                </button>
              </div>
            </div>
          </CardHeader>

          <CardContent class="p-0">
            <!-- Loading -->
            <div v-if="isLoading" class="p-8 text-center">
              <LoaderCircle class="mx-auto h-6 w-6 animate-spin text-primary" />
            </div>

            <!-- Empty -->
            <div
              v-else-if="filteredNotifications.length === 0"
              class="p-10 text-center text-slate-400 text-xs"
            >
              No tienes notificaciones en esta bandeja.
            </div>

            <!-- Lista -->
            <div v-else class="divide-y divide-slate-100">
              <div
                v-for="n in filteredNotifications"
                :key="n.id"
                :class="[
                  n.read ? 'bg-white' : 'bg-primary/5',
                  'flex items-start gap-3.5 p-4 transition-colors hover:bg-slate-50/80 cursor-pointer',
                ]"
                @click="!n.read && markAsRead(n.id)"
              >
                <!-- Icono de categoría -->
                <div
                  :class="[
                    getCategoryClass(n.category),
                    'flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border',
                  ]"
                >
                  <component :is="getCategoryIcon(n.category)" class="h-4 w-4" />
                </div>

                <!-- Contenido -->
                <div class="flex-1 min-w-0 space-y-1">
                  <div class="flex items-center justify-between gap-2">
                    <span :class="['text-xs', n.read ? 'font-medium text-slate-700' : 'font-bold text-slate-900']">
                      {{ n.title }}
                    </span>
                    <span class="text-[10px] text-slate-400 shrink-0">
                      {{ formatInstant(n.createdAt) }}
                    </span>
                  </div>

                  <p class="text-xs text-slate-600 leading-relaxed">
                    {{ n.message }}
                  </p>
                </div>

                <!-- Punto de no leída -->
                <span
                  v-if="!n.read"
                  class="h-2 w-2 rounded-full bg-accent shrink-0 mt-1.5"
                  title="No leída"
                />
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      <!-- PANEL DE PREFERENCIAS DE CANAL (FE-CP-20) -->
      <div class="space-y-4">
        <Card>
          <CardHeader class="pb-3 border-b border-slate-100">
            <CardTitle class="text-base flex items-center gap-2">
              <MessageSquare class="h-4 w-4 text-primary" />
              <span>Canales de Contacto</span>
            </CardTitle>
            <CardDescription>
              Elige por dónde deseas recibir tus alertas de vencimiento y pagos.
            </CardDescription>
          </CardHeader>

          <CardContent class="space-y-4 pt-4 text-xs">
            <!-- WhatsApp -->
            <label class="flex items-center justify-between rounded-xl border border-slate-200 p-3.5 cursor-pointer hover:bg-slate-50 transition-colors">
              <div class="flex items-center gap-2.5">
                <div class="flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-100 text-emerald-700">
                  <MessageCircle class="h-4 w-4" />
                </div>
                <div>
                  <span class="font-bold text-slate-900 block">WhatsApp</span>
                  <span class="text-[11px] text-slate-500">Recordatorios con botón de pago directo</span>
                </div>
              </div>
              <input
                v-model="preferences.whatsappEnabled"
                type="checkbox"
                class="h-4 w-4 rounded border-slate-300 text-primary focus:ring-primary"
              />
            </label>

            <!-- Email -->
            <label class="flex items-center justify-between rounded-xl border border-slate-200 p-3.5 cursor-pointer hover:bg-slate-50 transition-colors">
              <div class="flex items-center gap-2.5">
                <div class="flex h-8 w-8 items-center justify-center rounded-lg bg-blue-100 text-blue-700">
                  <Mail class="h-4 w-4" />
                </div>
                <div>
                  <span class="font-bold text-slate-900 block">Correo Electrónico</span>
                  <span class="text-[11px] text-slate-500">Comprobantes y recibos oficiales PDF</span>
                </div>
              </div>
              <input
                v-model="preferences.emailEnabled"
                type="checkbox"
                class="h-4 w-4 rounded border-slate-300 text-primary focus:ring-primary"
              />
            </label>

            <!-- SMS -->
            <label class="flex items-center justify-between rounded-xl border border-slate-200 p-3.5 cursor-pointer hover:bg-slate-50 transition-colors">
              <div class="flex items-center gap-2.5">
                <div class="flex h-8 w-8 items-center justify-center rounded-lg bg-slate-100 text-slate-700">
                  <MessageSquare class="h-4 w-4" />
                </div>
                <div>
                  <span class="font-bold text-slate-900 block">Mensajes SMS</span>
                  <span class="text-[11px] text-slate-500">Alertas de texto breves al celular</span>
                </div>
              </div>
              <input
                v-model="preferences.smsEnabled"
                type="checkbox"
                class="h-4 w-4 rounded border-slate-300 text-primary focus:ring-primary"
              />
            </label>

            <!-- Anticipación -->
            <div class="rounded-xl border border-slate-200 bg-slate-50 p-3.5 space-y-2">
              <label class="font-bold text-slate-800 block">
                Anticipación del recordatorio de vencimiento:
              </label>
              <select
                v-model.number="preferences.notifyDaysBeforeDue"
                class="w-full rounded-lg border border-slate-300 bg-white p-2 text-xs text-slate-800"
              >
                <option :value="1">1 día antes del vencimiento</option>
                <option :value="2">2 días antes del vencimiento</option>
                <option :value="3">3 días antes del vencimiento (recomendado)</option>
                <option :value="5">5 días antes del vencimiento</option>
              </select>
            </div>
          </CardContent>

          <CardFooter class="border-t border-slate-100 pt-3">
            <Button
              class="w-full"
              size="sm"
              :disabled="isSavingPreferences"
              @click="savePreferences"
            >
              <LoaderCircle v-if="isSavingPreferences" class="mr-1.5 h-4 w-4 animate-spin" />
              <Save v-else class="mr-1.5 h-4 w-4" />
              <span>Guardar Preferencias</span>
            </Button>
          </CardFooter>
        </Card>
      </div>
    </div>
  </div>
</template>
