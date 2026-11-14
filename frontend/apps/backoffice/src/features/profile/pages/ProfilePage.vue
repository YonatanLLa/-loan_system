<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { useRouter } from 'vue-router'
import { toast } from 'vue-sonner'
import {
  Check,
  Copy,
  KeyRound,
  LoaderCircle,
  LogOut,
  Mail,
  Monitor,
  Moon,
  RefreshCw,
  ShieldAlert,
  ShieldCheck,
  Sun,
  UserCheck,
} from '@lucide/vue'

import { toApiError } from '@prestamos/api-client'
import { PageHeader } from '@prestamos/ui'
import { Avatar, AvatarFallback } from '@prestamos/ui/components/ui/avatar'
import { Badge } from '@prestamos/ui/components/ui/badge'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Separator } from '@prestamos/ui/components/ui/separator'

import { type ThemeMode, useTheme } from '@/app/composables/useTheme'
import { useSessionStore } from '@/app/stores/session'

import { getPermissionLabel, getRoleLabel } from '../utils/profileCatalog'

const THEME_OPTIONS: ReadonlyArray<{
  value: ThemeMode
  label: string
  description: string
  icon: typeof Sun
}> = [
  {
    value: 'light',
    label: 'Claro',
    description: 'Superficies luminosas ideales para entornos bien iluminados.',
    icon: Sun,
  },
  {
    value: 'dark',
    label: 'Oscuro',
    description: 'Reduce el brillo y mejora la comodidad visual en baja iluminación.',
    icon: Moon,
  },
  {
    value: 'system',
    label: 'Sistema',
    description: 'Sigue automáticamente la preferencia configurada en tu sistema.',
    icon: Monitor,
  },
]

const router = useRouter()
const session = useSessionStore()
const { themeMode, resolvedTheme, setTheme } = useTheme()

const refreshing = ref(false)
const copiedUserId = ref(false)
let copyResetTimer: ReturnType<typeof setTimeout> | null = null

const confirmingLogoutAll = ref(false)
const loggingOut = ref(false)
const loggingOutAll = ref(false)

const effectiveUserId = computed(() => session.user?.userId ?? session.user?.id ?? '')
const roleLabel = computed(() => getRoleLabel(session.user?.role))
const mfaEnabled = computed(() => Boolean(session.user?.mfaEnabled))

const permissionItems = computed(() =>
  session.permissions.map((key) => ({
    key,
    label: getPermissionLabel(key),
  })),
)

const permissionsCountLabel = computed(() => {
  const count = session.permissions.length
  return `${count} ${count === 1 ? 'permiso' : 'permisos'}`
})

const sessionTabStatus = computed(() => {
  if (session.isDemo) {
    return {
      label: 'Modo demo',
      variant: 'outline' as const,
      detail:
        'Sesión simulada en memoria para desarrollo. No mantiene refresh token ni consulta el backend de seguridad.',
    }
  }
  if (session.refreshToken) {
    return {
      label: 'Sesión activa en esta pestaña',
      variant: 'secondary' as const,
      detail:
        'El refresh token rotatorio se resguarda por pestaña en sessionStorage y el access token permanece solo en memoria.',
    }
  }
  return {
    label: 'Sin refresh token en pestaña',
    variant: 'outline' as const,
    detail:
      'No hay refresh token guardado en esta pestaña. Si recargas la página deberás iniciar sesión nuevamente.',
  }
})

const isLogoutBusy = computed(() => loggingOut.value || loggingOutAll.value)

async function handleRefreshProfile() {
  if (refreshing.value) return
  refreshing.value = true
  try {
    await session.refreshProfile()
    toast.success('Perfil actualizado', {
      description: session.isDemo
        ? 'Modo demo activo: se conservaron los datos locales de la sesión.'
        : 'Identidad y permisos sincronizados desde GET /api/me.',
    })
  } catch (error) {
    const apiError = toApiError(error)
    toast.error(apiError.title || 'No se pudo actualizar el perfil', {
      description: apiError.detail,
    })
  } finally {
    refreshing.value = false
  }
}

async function copyUserId() {
  const id = effectiveUserId.value
  if (!id) return
  try {
    if (!navigator?.clipboard?.writeText) {
      throw new Error('Clipboard unavailable')
    }
    await navigator.clipboard.writeText(id)
    copiedUserId.value = true
    if (copyResetTimer) clearTimeout(copyResetTimer)
    copyResetTimer = setTimeout(() => {
      copiedUserId.value = false
    }, 2000)
    toast.success('ID de usuario copiado al portapapeles')
  } catch {
    toast.error('No se pudo copiar automáticamente', {
      description: 'Cópialo manualmente desde el identificador visible.',
    })
  }
}

async function handleLogoutCurrent() {
  if (isLogoutBusy.value) return
  loggingOut.value = true
  try {
    await session.logout()
    await router.replace({ name: 'login' })
  } finally {
    loggingOut.value = false
  }
}

function requestLogoutAll() {
  confirmingLogoutAll.value = true
}

function cancelLogoutAll() {
  confirmingLogoutAll.value = false
}

async function confirmLogoutAll() {
  if (isLogoutBusy.value) return
  loggingOutAll.value = true
  try {
    await session.logoutAll()
    toast.success('Se cerraron todas las sesiones activas')
    await router.replace({ name: 'login' })
  } finally {
    loggingOutAll.value = false
    confirmingLogoutAll.value = false
  }
}

onBeforeUnmount(() => {
  if (copyResetTimer) clearTimeout(copyResetTimer)
})
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Mi perfil"
      description="Consulta tu identidad, permisos efectivos asignados por el backend y administra tus sesiones activas."
    >
      <template #actions>
        <Button
          type="button"
          variant="outline"
          size="sm"
          :disabled="refreshing"
          @click="handleRefreshProfile"
        >
          <RefreshCw class="size-4" :class="refreshing && 'animate-spin'" aria-hidden="true" />
          Actualizar
        </Button>
      </template>
    </PageHeader>

    <div class="grid gap-6 lg:grid-cols-2">
      <!-- Identidad y cuenta -->
      <Card>
        <CardHeader>
          <CardTitle class="flex items-center gap-2 text-base">
            <UserCheck class="size-4 text-primary" aria-hidden="true" />
            Identidad y cuenta
          </CardTitle>
          <CardDescription>
            Información del usuario autenticado proveniente de
            <code class="text-xs">GET /api/me</code>.
          </CardDescription>
        </CardHeader>
        <CardContent class="space-y-5">
          <div class="flex items-start gap-4">
            <Avatar class="size-12 rounded-xl">
              <AvatarFallback class="rounded-xl text-base font-semibold">
                {{ session.initials || 'US' }}
              </AvatarFallback>
            </Avatar>
            <div class="min-w-0 flex-1 space-y-1.5">
              <p class="truncate text-base font-semibold leading-tight">
                {{ session.user?.fullName || 'Usuario sin nombre' }}
              </p>
              <p class="flex items-center gap-1.5 truncate text-sm text-muted-foreground">
                <Mail class="size-3.5 shrink-0" aria-hidden="true" />
                <span class="truncate">{{ session.user?.email || 'Sin correo registrado' }}</span>
              </p>
              <div class="flex flex-wrap items-center gap-2 pt-0.5">
                <Badge variant="secondary" class="font-mono text-xs">
                  {{ session.user?.role || 'SIN_ROL' }}
                </Badge>
                <span class="text-xs font-medium text-muted-foreground">{{ roleLabel }}</span>
              </div>
            </div>
          </div>

          <Separator />

          <div class="space-y-2">
            <span class="text-xs font-medium text-muted-foreground">ID de usuario</span>
            <div class="flex flex-wrap items-center justify-between gap-2 rounded-lg border bg-muted/30 px-3 py-2">
              <code
                data-testid="profile-user-id"
                class="break-all font-mono text-xs text-foreground"
              >
                {{ effectiveUserId || 'No disponible' }}
              </code>
              <Button
                type="button"
                variant="outline"
                size="sm"
                class="h-7 px-2.5 text-xs"
                :disabled="!effectiveUserId"
                @click="copyUserId"
              >
                <Check
                  v-if="copiedUserId"
                  class="size-3.5 text-emerald-600 dark:text-emerald-400"
                  aria-hidden="true"
                />
                <Copy v-else class="size-3.5" aria-hidden="true" />
                {{ copiedUserId ? 'Copiado' : 'Copiar ID' }}
              </Button>
            </div>
          </div>

          <Separator />

          <div class="space-y-2">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <span class="text-xs font-medium text-muted-foreground">
                Autenticación en dos pasos (MFA)
              </span>
              <Badge
                v-if="mfaEnabled"
                variant="secondary"
                class="gap-1.5 border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400"
              >
                <ShieldCheck class="size-3.5" aria-hidden="true" />
                MFA TOTP activo
              </Badge>
              <Badge
                v-else
                variant="outline"
                class="gap-1.5 border-amber-500/40 bg-amber-500/10 text-amber-700 dark:text-amber-400"
              >
                <ShieldAlert class="size-3.5" aria-hidden="true" />
                MFA no configurado
              </Badge>
            </div>
            <p class="text-xs text-muted-foreground">
              {{
                mfaEnabled
                  ? 'Tu cuenta tiene verificador TOTP habilitado para el inicio de sesión y aprobaciones de cuatro ojos.'
                  : 'La verificación en dos pasos no está configurada para esta cuenta. Los roles privilegiados deben enrolarla al iniciar sesión; un administrador puede reiniciarla si requieres reconfigurar tu autenticador.'
              }}
            </p>
          </div>
        </CardContent>
      </Card>

      <!-- Seguridad y sesiones -->
      <Card>
        <CardHeader>
          <CardTitle class="flex items-center gap-2 text-base">
            <KeyRound class="size-4 text-primary" aria-hidden="true" />
            Seguridad y sesiones
          </CardTitle>
          <CardDescription>
            Control de tu sesión actual y revocación global mediante
            <code class="text-xs">POST /api/auth/logout</code>.
          </CardDescription>
        </CardHeader>
        <CardContent class="space-y-5">
          <div class="space-y-2 rounded-lg border bg-muted/30 p-3">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <span class="text-xs font-medium text-muted-foreground">Estado en esta pestaña</span>
              <Badge :variant="sessionTabStatus.variant">{{ sessionTabStatus.label }}</Badge>
            </div>
            <p class="text-xs text-muted-foreground">{{ sessionTabStatus.detail }}</p>
          </div>

          <Separator />

          <div class="space-y-3">
            <div class="flex flex-wrap gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                :disabled="isLogoutBusy"
                @click="handleLogoutCurrent"
              >
                <LoaderCircle v-if="loggingOut" class="size-4 animate-spin" aria-hidden="true" />
                <LogOut v-else class="size-4" aria-hidden="true" />
                Cerrar sesión actual
              </Button>
              <Button
                type="button"
                variant="destructive"
                size="sm"
                :disabled="isLogoutBusy || confirmingLogoutAll"
                @click="requestLogoutAll"
              >
                <LogOut class="size-4" aria-hidden="true" />
                Cerrar todas las sesiones
              </Button>
            </div>

            <div
              v-if="confirmingLogoutAll"
              class="space-y-3 rounded-lg border border-destructive/40 bg-destructive/10 p-3 text-sm"
              role="alertdialog"
              aria-label="Confirmar cierre de todas las sesiones"
            >
              <p class="font-medium">¿Cerrar todas las sesiones activas?</p>
              <p class="text-xs text-muted-foreground">
                Se revocarán todos tus refresh tokens activos en esta y otras pestañas o dispositivos
                y deberás volver a iniciar sesión.
              </p>
              <div class="flex flex-wrap gap-2">
                <Button
                  type="button"
                  variant="destructive"
                  size="sm"
                  :disabled="isLogoutBusy"
                  @click="confirmLogoutAll"
                >
                  <LoaderCircle v-if="loggingOutAll" class="size-4 animate-spin" aria-hidden="true" />
                  Confirmar cierre de todas las sesiones
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  :disabled="isLogoutBusy"
                  @click="cancelLogoutAll"
                >
                  Cancelar
                </Button>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- Apariencia -->
    <Card>
      <CardHeader class="flex flex-row flex-wrap items-start justify-between gap-4 space-y-0">
        <div class="space-y-1">
          <CardTitle class="flex items-center gap-2 text-base">
            <Sun v-if="resolvedTheme === 'light'" class="size-4 text-primary" aria-hidden="true" />
            <Moon v-else class="size-4 text-primary" aria-hidden="true" />
            Apariencia
          </CardTitle>
          <CardDescription>
            Elige entre tema claro, oscuro o seguir la configuración de tu sistema operativo.
          </CardDescription>
        </div>
        <Badge variant="secondary" data-testid="active-theme-badge">
          {{ resolvedTheme === 'dark' ? 'Tema oscuro activo' : 'Tema claro activo' }}
        </Badge>
      </CardHeader>
      <CardContent class="space-y-3">
        <div class="grid gap-3 sm:grid-cols-3" role="radiogroup" aria-label="Tema de la interfaz">
          <button
            v-for="option in THEME_OPTIONS"
            :key="option.value"
            type="button"
            role="radio"
            :aria-checked="themeMode === option.value"
            class="flex flex-col items-start gap-2 rounded-lg border p-3.5 text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            :class="
              themeMode === option.value
                ? 'border-primary bg-primary/5 text-foreground'
                : 'border-border bg-background text-muted-foreground hover:bg-muted/50'
            "
            @click="setTheme(option.value)"
          >
            <div class="flex w-full items-center justify-between gap-2">
              <span class="flex items-center gap-2 text-sm font-medium text-foreground">
                <component :is="option.icon" class="size-4" aria-hidden="true" />
                {{ option.label }}
              </span>
              <Check v-if="themeMode === option.value" class="size-4 text-primary" aria-hidden="true" />
            </div>
            <span class="text-xs text-muted-foreground">{{ option.description }}</span>
          </button>
        </div>
        <p v-if="themeMode === 'system'" class="text-xs text-muted-foreground">
          Modo automático activo: actualmente se aplica el tema
          <strong class="text-foreground">{{ resolvedTheme === 'dark' ? 'oscuro' : 'claro' }}</strong>
          según tu sistema.
        </p>
      </CardContent>
    </Card>

    <!-- Permisos efectivos -->
    <Card>
      <CardHeader class="flex flex-row flex-wrap items-start justify-between gap-4 space-y-0">
        <div class="space-y-1">
          <CardTitle class="flex items-center gap-2 text-base">
            <ShieldCheck class="size-4 text-primary" aria-hidden="true" />
            Permisos efectivos
          </CardTitle>
          <CardDescription>
            Permisos asignados por el backend para tu rol actual y evaluados en cada endpoint.
          </CardDescription>
        </div>
        <Badge variant="secondary" data-testid="permissions-count">
          {{ permissionsCountLabel }}
        </Badge>
      </CardHeader>
      <CardContent>
        <p v-if="permissionItems.length === 0" class="text-sm text-muted-foreground">
          No hay permisos efectivos asignados a esta cuenta.
        </p>
        <ul v-else class="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
          <li
            v-for="item in permissionItems"
            :key="item.key"
            class="flex flex-col gap-1 rounded-lg border bg-muted/20 p-3"
          >
            <Badge variant="outline" class="w-fit font-mono text-xs">
              {{ item.key }}
            </Badge>
            <span class="text-xs text-muted-foreground">
              {{ item.label }}
            </span>
          </li>
        </ul>
      </CardContent>
    </Card>
  </div>
</template>
