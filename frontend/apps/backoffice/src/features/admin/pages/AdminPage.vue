<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import {
  Activity,
  CheckCircle2,
  ExternalLink,
  KeyRound,
  RefreshCw,
  ShieldAlert,
  ShieldCheck,
  UserCheck,
  UserX,
} from '@lucide/vue'

import { PageHeader, StatusBadge } from '@prestamos/ui'
import { Alert, AlertDescription, AlertTitle } from '@prestamos/ui/components/ui/alert'
import { Badge } from '@prestamos/ui/components/ui/badge'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Field, FieldError, FieldGroup, FieldLabel } from '@prestamos/ui/components/ui/field'
import { Input } from '@prestamos/ui/components/ui/input'
import { formatInstant } from '@prestamos/utils'

import { PERMISSIONS } from '@/app/permissions'
import { useSessionStore } from '@/app/stores/session'

import { SECURITY_ROLES_MATRIX, useAdminSettings } from '../composables/useAdminSettings'

const session = useSessionStore()

const {
  pingResult,
  pingLoading,
  pingError,
  targetUserId,
  targetUserIdError,
  isValidUserId,
  pendingActionMeta,
  actionBusy,
  lastActionFeedback,
  roleForm,
  roleReasonError,
  roleSubmitting,
  createdApproval,
  checkSecurityPing,
  requestActionConfirmation,
  cancelActionConfirmation,
  executePendingAction,
  submitRoleChange,
} = useAdminSettings()

const canReadAudit = computed(() => session.can(PERMISSIONS.audit))
const canReadApprovals = computed(() => session.can(PERMISSIONS.approvalsRead))
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Administración de Seguridad"
      description="Operaciones de control de usuarios, cambios de rol con cuatro ojos y referencia de roles del módulo Security."
    >
      <template #actions>
        <Button variant="outline" size="sm" :disabled="pingLoading" @click="checkSecurityPing">
          <RefreshCw :class="['size-4', pingLoading && 'animate-spin']" aria-hidden="true" />
          Verificar módulo Security
        </Button>
      </template>
    </PageHeader>

    <!-- Estado del módulo Security -->
    <Card>
      <CardHeader class="flex flex-row items-center justify-between gap-4 space-y-0">
        <div class="space-y-1">
          <CardTitle class="flex items-center gap-2 text-base">
            <Activity class="size-4 text-primary" aria-hidden="true" />
            Estado del módulo Security
          </CardTitle>
          <CardDescription>
            Diagnóstico directo contra <code class="text-xs">GET /api/security/ping</code>.
          </CardDescription>
        </div>
        <Badge v-if="pingResult" variant="secondary" class="gap-1.5">
          <CheckCircle2 class="size-3.5 text-emerald-600 dark:text-emerald-400" aria-hidden="true" />
          {{ pingResult.status === 'Ready' || pingResult.status === 'ok' ? 'Operativo' : pingResult.status }}
        </Badge>
        <Badge v-else-if="pingError" variant="destructive">Sin respuesta</Badge>
      </CardHeader>
      <CardContent class="text-sm">
        <p v-if="pingLoading && !pingResult" class="text-muted-foreground">
          Consultando disponibilidad del módulo...
        </p>
        <p v-else-if="pingError" class="text-destructive">{{ pingError }}</p>
        <div v-else-if="pingResult" class="flex flex-wrap gap-6 text-xs text-muted-foreground">
          <span>
            Módulo: <strong class="text-foreground">{{ pingResult.module }}</strong>
          </span>
          <span>
            Estado: <strong class="text-foreground">{{ pingResult.status }}</strong>
          </span>
        </div>
      </CardContent>
    </Card>

    <div class="grid gap-6 lg:grid-cols-2">
      <!-- Operaciones sobre usuario por UUID -->
      <Card>
        <CardHeader>
          <CardTitle class="flex items-center gap-2 text-base">
            <ShieldCheck class="size-4 text-primary" aria-hidden="true" />
            Acciones de seguridad sobre usuario
          </CardTitle>
          <CardDescription>
            Ingresa el UUID del usuario para suspenderlo, reactivarlo o forzar un nuevo enrolamiento
            MFA TOTP.
          </CardDescription>
        </CardHeader>
        <CardContent class="space-y-4">
          <Field :data-invalid="!!targetUserIdError || undefined">
            <FieldLabel for="admin-target-user-id">UUID del usuario</FieldLabel>
            <Input
              id="admin-target-user-id"
              v-model="targetUserId"
              type="text"
              placeholder="00000000-0000-0000-0000-000000000000"
              class="font-mono text-xs"
              :aria-invalid="!!targetUserIdError"
            />
            <FieldError :errors="[targetUserIdError ?? undefined]" />
          </Field>

          <div class="flex flex-wrap gap-2">
            <Button
              type="button"
              variant="destructive"
              size="sm"
              :disabled="actionBusy"
              @click="requestActionConfirmation('suspend')"
            >
              <UserX class="size-4" aria-hidden="true" />
              Suspender usuario
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              :disabled="actionBusy"
              @click="requestActionConfirmation('activate')"
            >
              <UserCheck class="size-4" aria-hidden="true" />
              Activar usuario
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              :disabled="actionBusy"
              @click="requestActionConfirmation('resetMfa')"
            >
              <KeyRound class="size-4" aria-hidden="true" />
              Reiniciar MFA
            </Button>
          </div>

          <div
            v-if="pendingActionMeta"
            class="space-y-3 rounded-lg border border-amber-500/40 bg-amber-500/10 p-3 text-sm"
            role="alertdialog"
            aria-label="Confirmar operación de seguridad"
          >
            <p class="font-medium">{{ pendingActionMeta.title }}</p>
            <p class="text-xs text-muted-foreground">
              Se ejecutará sobre el usuario <code class="font-mono">{{ targetUserId.trim() }}</code>
              quedando registrado en el log de auditoría.
            </p>
            <div class="flex gap-2">
              <Button
                type="button"
                size="sm"
                :disabled="actionBusy"
                @click="executePendingAction"
              >
                {{ pendingActionMeta.confirmLabel }}
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                :disabled="actionBusy"
                @click="cancelActionConfirmation"
              >
                Cancelar
              </Button>
            </div>
          </div>

          <Alert v-if="lastActionFeedback">
            <AlertTitle>Operación registrada</AlertTitle>
            <AlertDescription>{{ lastActionFeedback }}</AlertDescription>
          </Alert>

          <div
            v-if="canReadAudit && isValidUserId"
            class="border-t pt-3 text-xs text-muted-foreground"
          >
            <RouterLink
              :to="{ name: 'audit', query: { entity: 'users', entityId: targetUserId.trim() } }"
              class="inline-flex items-center gap-1 font-medium text-primary hover:underline"
            >
              Ver trazabilidad del usuario en Auditoría
              <ExternalLink class="size-3.5" aria-hidden="true" />
            </RouterLink>
          </div>
        </CardContent>
      </Card>

      <!-- Solicitud de cambio de rol (Cuatro ojos) -->
      <Card>
        <CardHeader>
          <CardTitle class="flex items-center gap-2 text-base">
            <ShieldAlert class="size-4 text-primary" aria-hidden="true" />
            Cambio de rol (Flujo Cuatro Ojos)
          </CardTitle>
          <CardDescription>
            Toda modificación de rol crea una solicitud pendiente en
            <code class="text-xs">PUT /api/users/{id}/role</code> que requiere aprobación de otro
            autorizador con MFA.
          </CardDescription>
        </CardHeader>
        <CardContent class="space-y-4">
          <form novalidate class="space-y-4" @submit.prevent="submitRoleChange">
            <FieldGroup>
              <Field>
                <FieldLabel for="admin-new-role">Nuevo rol</FieldLabel>
                <select
                  id="admin-new-role"
                  v-model="roleForm.newRole"
                  class="flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-xs transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                >
                  <option
                    v-for="entry in SECURITY_ROLES_MATRIX"
                    :key="entry.role"
                    :value="entry.role"
                  >
                    {{ entry.role }} · {{ entry.label }}
                  </option>
                </select>
              </Field>

              <Field :data-invalid="!!roleReasonError || undefined">
                <FieldLabel for="admin-role-reason">Justificación (mínimo 10 caracteres)</FieldLabel>
                <textarea
                  id="admin-role-reason"
                  v-model="roleForm.reason"
                  rows="3"
                  placeholder="Describe el motivo operativo o ticket que respalda el cambio de rol..."
                  class="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-xs placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                  :aria-invalid="!!roleReasonError"
                />
                <FieldError :errors="[roleReasonError ?? undefined]" />
              </Field>

              <Button type="submit" :disabled="roleSubmitting">
                Solicitar cambio de rol
              </Button>
            </FieldGroup>
          </form>

          <Alert v-if="createdApproval" data-testid="created-role-approval">
            <AlertTitle class="flex items-center justify-between gap-2">
              <span>Solicitud de aprobación generada</span>
              <StatusBadge kind="approval" :status="createdApproval.status" />
            </AlertTitle>
            <AlertDescription class="mt-2 space-y-2 text-xs">
              <p>
                ID: <code class="font-mono text-foreground">{{ createdApproval.id }}</code>
              </p>
              <p>
                Expira: <strong class="text-foreground">{{ formatInstant(createdApproval.expiresAt) }}</strong>
              </p>
              <RouterLink
                v-if="canReadApprovals"
                :to="{ name: 'approvals', query: { id: createdApproval.id } }"
                class="inline-flex items-center gap-1 font-medium text-primary hover:underline"
              >
                Abrir solicitud en Aprobaciones
                <ExternalLink class="size-3.5" aria-hidden="true" />
              </RouterLink>
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>
    </div>

    <!-- Matriz de referencia de roles y permisos (solo lectura) -->
    <Card>
      <CardHeader>
        <CardTitle class="text-base">Catálogo de roles y permisos efectivos</CardTitle>
        <CardDescription>
          Definición autoritativa de permisos por rol en el backend (solo lectura). El portal de
          administración no modifica esta matriz en tiempo de ejecución.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div class="overflow-x-auto">
          <table class="w-full border-collapse text-left text-sm">
            <thead>
              <tr class="border-b text-xs text-muted-foreground">
                <th class="py-2 pr-4 font-medium">Rol</th>
                <th class="py-2 pr-4 font-medium">Descripción</th>
                <th class="py-2 font-medium">Permisos asignados</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="entry in SECURITY_ROLES_MATRIX"
                :key="entry.role"
                class="border-b align-top last:border-0"
              >
                <td class="py-3 pr-4 whitespace-nowrap">
                  <div class="font-mono text-xs font-semibold">{{ entry.role }}</div>
                  <div class="text-xs text-muted-foreground">{{ entry.label }}</div>
                </td>
                <td class="py-3 pr-4 text-xs text-muted-foreground">
                  {{ entry.description }}
                </td>
                <td class="py-3">
                  <div class="flex flex-wrap gap-1.5">
                    <Badge
                      v-for="perm in entry.permissions"
                      :key="perm"
                      variant="outline"
                      class="font-mono text-[11px]"
                    >
                      {{ perm }}
                    </Badge>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
