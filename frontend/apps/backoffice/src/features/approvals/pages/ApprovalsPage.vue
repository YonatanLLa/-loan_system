<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import {
  Ban,
  CheckCircle2,
  CheckSquare,
  ChevronLeft,
  ChevronRight,
  ExternalLink,
  Eye,
  Filter,
  LoaderCircle,
  RefreshCw,
  Search,
  ShieldCheck,
  X,
  XCircle,
} from '@lucide/vue'

import type { ApprovalScope } from '@prestamos/api-client'
import { EmptyState, PageHeader, StatusBadge, TableGenerator } from '@prestamos/ui'
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
import { Field, FieldError, FieldGroup, FieldLabel } from '@prestamos/ui/components/ui/field'
import { Input } from '@prestamos/ui/components/ui/input'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@prestamos/ui/components/ui/sheet'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import { formatInstant } from '@prestamos/utils'

import { PERMISSIONS } from '@/app/permissions'
import { useSessionStore } from '@/app/stores/session'

import { APPROVAL_STATUS_OPTIONS, useApprovals } from '../composables/useApprovals'
import {
  APPROVAL_OPERATION_LABELS,
  approvalColumns,
  getOperationLabel,
} from '../utils/approvalsColumns'

const route = useRoute()
const session = useSessionStore()
const canReadAudit = computed(() => session.can(PERMISSIONS.audit))

const {
  filters,
  loading,
  approvalsPage,
  totalPages,
  isDetailOpen,
  detailLoading,
  selectedDetail,
  approveTarget,
  approveMfaCode,
  approveComment,
  approveError,
  approveSubmitting,
  rejectTarget,
  rejectComment,
  rejectError,
  rejectSubmitting,
  cancelTarget,
  cancelSubmitting,
  getDecideBlockReason,
  canDecide,
  canCancel,
  loadApprovals,
  setScope,
  applyFilters,
  resetFilters,
  goToPage,
  openDetail,
  closeDetail,
  openApproveModal,
  closeApproveModal,
  submitApprove,
  openRejectModal,
  closeRejectModal,
  submitReject,
  openCancelDialog,
  closeCancelDialog,
  confirmCancel,
} = useApprovals()

const scopeTabs: readonly { value: ApprovalScope; label: string }[] = [
  { value: 'toDecide', label: 'Por decidir' },
  { value: 'mine', label: 'Mis solicitudes' },
  { value: 'all', label: 'Todas' },
]

function formatJson(value: unknown): string {
  if (value === null || value === undefined) return 'Sin datos'
  try {
    return JSON.stringify(value, null, 2)
  } catch {
    return String(value)
  }
}

onMounted(async () => {
  await loadApprovals()
  const queryId = typeof route.query.id === 'string' ? route.query.id.trim() : ''
  if (queryId) {
    await openDetail(queryId)
  }
})
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      title="Aprobaciones (Cuatro Ojos)"
      description="Bandeja de solicitudes críticas que requieren validación de un segundo autorizador con MFA TOTP."
    >
      <template #actions>
        <Button variant="outline" size="sm" :disabled="loading" @click="loadApprovals">
          <RefreshCw class="mr-2 size-4" :class="{ 'animate-spin': loading }" />
          Actualizar
        </Button>
      </template>
    </PageHeader>

    <!-- Tabs de scope -->
    <div class="flex flex-wrap gap-2 border-b pb-1">
      <button
        v-for="tab in scopeTabs"
        :key="tab.value"
        type="button"
        class="border-b-2 px-3 py-2 text-sm font-medium transition-colors"
        :class="
          filters.scope === tab.value
            ? 'border-primary text-primary'
            : 'border-transparent text-muted-foreground hover:text-foreground'
        "
        @click="setScope(tab.value)"
      >
        {{ tab.label }}
      </button>
    </div>

    <!-- Filtros -->
    <Card>
      <CardHeader class="pb-3">
        <CardTitle class="flex items-center gap-2 text-base">
          <Filter class="size-4 text-muted-foreground" />
          Filtros
        </CardTitle>
        <CardDescription>Filtra por estado, tipo de operación o ID de objetivo.</CardDescription>
      </CardHeader>
      <CardContent>
        <form
          class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4"
          @submit.prevent="applyFilters"
        >
          <div class="space-y-1.5">
            <label for="approval-status" class="text-xs font-medium text-muted-foreground">
              Estado
            </label>
            <select
              id="approval-status"
              v-model="filters.status"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="">Todos los estados</option>
              <option v-for="opt in APPROVAL_STATUS_OPTIONS" :key="opt.value" :value="opt.value">
                {{ opt.label }}
              </option>
            </select>
          </div>

          <div class="space-y-1.5">
            <label for="approval-op" class="text-xs font-medium text-muted-foreground">
              Tipo de operación
            </label>
            <select
              id="approval-op"
              v-model="filters.operationType"
              class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="">Todas las operaciones</option>
              <option
                v-for="(label, opKey) in APPROVAL_OPERATION_LABELS"
                :key="opKey"
                :value="opKey"
              >
                {{ label }}
              </option>
            </select>
          </div>

          <div class="space-y-1.5">
            <label for="approval-target" class="text-xs font-medium text-muted-foreground">
              ID objetivo
            </label>
            <Input
              id="approval-target"
              v-model="filters.targetId"
              placeholder="UUID o identificador..."
              class="h-9 font-mono text-xs"
            />
          </div>

          <div class="flex items-end justify-end gap-2">
            <Button type="button" variant="ghost" size="sm" @click="resetFilters">
              <X class="mr-1 size-4" />
              Limpiar
            </Button>
            <Button type="submit" size="sm" :disabled="loading">
              <Search class="mr-1 size-4" />
              Filtrar
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>

    <!-- Tabla -->
    <Card>
      <CardContent class="p-0">
        <div v-if="loading" class="space-y-4 p-6">
          <Skeleton class="h-10 w-full" />
          <Skeleton class="h-12 w-full" />
          <Skeleton class="h-12 w-full" />
        </div>

        <div v-else-if="approvalsPage.items.length === 0" class="p-12">
          <EmptyState
            :icon="CheckSquare"
            title="Sin solicitudes de aprobación"
            description="No hay solicitudes que coincidan con la pestaña y los filtros actuales."
          >
            <Button variant="outline" size="sm" @click="resetFilters">Restablecer filtros</Button>
          </EmptyState>
        </div>

        <div v-else>
          <TableGenerator :columns="approvalColumns" :rows="approvalsPage.items" row-key="id">
            <template #cell-operationType="{ row: item }">
              <div class="font-medium">{{ getOperationLabel(item.operationType) }}</div>
              <div class="font-mono text-[11px] text-muted-foreground">
                {{ item.operationType }}
              </div>
            </template>

            <template #cell-target="{ row: item }">
              <div class="text-xs font-medium">{{ item.targetEntity }}</div>
              <div class="font-mono text-xs text-muted-foreground">{{ item.targetId }}</div>
            </template>

            <template #cell-status="{ row: item }">
              <StatusBadge kind="approval" :status="item.status" />
            </template>

            <template #cell-requestedBy="{ row: item }">
              <div class="text-sm font-medium">{{ item.requestedBy.fullName }}</div>
              <div class="text-xs text-muted-foreground">{{ item.requestedBy.email }}</div>
            </template>

            <template #cell-actions="{ row: item }">
              <Button
                variant="ghost"
                size="sm"
                class="h-8 gap-1.5 text-xs"
                @click="openDetail(item.id)"
              >
                <Eye class="size-3.5" />
                <span>Ver detalle</span>
              </Button>
            </template>
          </TableGenerator>
        </div>

        <div
          v-if="approvalsPage.total > 0"
          class="flex flex-col items-center justify-between gap-4 border-t p-4 text-xs text-muted-foreground sm:flex-row"
        >
          <div>
            Mostrando
            <span class="font-medium text-foreground">
              {{ (approvalsPage.page - 1) * approvalsPage.pageSize + 1 }}
            </span>
            a
            <span class="font-medium text-foreground">
              {{ Math.min(approvalsPage.page * approvalsPage.pageSize, approvalsPage.total) }}
            </span>
            de
            <span class="font-medium text-foreground">{{ approvalsPage.total }}</span>
            solicitudes
          </div>

          <div class="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              class="h-8"
              :disabled="approvalsPage.page <= 1 || loading"
              @click="goToPage(approvalsPage.page - 1)"
            >
              <ChevronLeft class="mr-1 size-4" />
              Anterior
            </Button>
            <span class="px-2 font-medium text-foreground">
              Página {{ approvalsPage.page }} de {{ totalPages }}
            </span>
            <Button
              variant="outline"
              size="sm"
              class="h-8"
              :disabled="approvalsPage.page >= totalPages || loading"
              @click="goToPage(approvalsPage.page + 1)"
            >
              Siguiente
              <ChevronRight class="ml-1 size-4" />
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Sheet de Detalle -->
    <Sheet :open="isDetailOpen" @update:open="(val) => (val ? null : closeDetail())">
      <SheetContent side="right" class="w-full overflow-y-auto sm:max-w-2xl">
        <SheetHeader class="border-b pb-4">
          <div class="flex flex-wrap items-center gap-2">
            <StatusBadge
              v-if="selectedDetail"
              kind="approval"
              :status="selectedDetail.status"
            />
            <Badge v-if="selectedDetail" variant="outline" class="font-mono text-xs">
              {{ selectedDetail.operationType }}
            </Badge>
            <SheetTitle class="text-base font-semibold">
              {{ selectedDetail ? getOperationLabel(selectedDetail.operationType) : 'Detalle de solicitud' }}
            </SheetTitle>
          </div>
          <SheetDescription v-if="selectedDetail" class="text-xs">
            Solicitud <span class="font-mono">{{ selectedDetail.id }}</span> sobre
            <span class="font-mono">{{ selectedDetail.targetEntity }} #{{ selectedDetail.targetId }}</span>
          </SheetDescription>
        </SheetHeader>

        <div v-if="detailLoading" class="space-y-4 pt-6">
          <Skeleton class="h-24 w-full" />
          <Skeleton class="h-40 w-full" />
        </div>

        <div v-else-if="selectedDetail" class="space-y-5 pt-4">
          <div class="grid grid-cols-2 gap-3 rounded-lg border bg-muted/30 p-3 text-xs">
            <div>
              <span class="block text-muted-foreground">Solicitante</span>
              <span class="font-medium">{{ selectedDetail.requestedBy.fullName }}</span>
              <span class="block text-muted-foreground">{{ selectedDetail.requestedBy.email }}</span>
            </div>
            <div>
              <span class="block text-muted-foreground">Fecha de solicitud</span>
              <span class="font-medium">{{ formatInstant(selectedDetail.requestedAt) }}</span>
              <span class="mt-1 block text-muted-foreground">
                Expira: {{ formatInstant(selectedDetail.expiresAt) }}
              </span>
            </div>
            <div class="col-span-2">
              <span class="block text-muted-foreground">Justificación</span>
              <p class="mt-0.5 whitespace-pre-wrap text-foreground">{{ selectedDetail.reason }}</p>
            </div>
            <div v-if="selectedDetail.evidenceRef" class="col-span-2">
              <span class="block text-muted-foreground">Referencia de evidencia</span>
              <span class="font-mono">{{ selectedDetail.evidenceRef }}</span>
            </div>
            <div v-if="selectedDetail.decidedBy" class="col-span-2 border-t pt-2">
              <span class="block text-muted-foreground">Decidida por</span>
              <span class="font-medium">
                {{ selectedDetail.decidedBy.fullName }} ({{ selectedDetail.decidedBy.email }})
              </span>
              <span v-if="selectedDetail.decidedAt" class="ml-1 text-muted-foreground">
                · {{ formatInstant(selectedDetail.decidedAt) }}
              </span>
              <p v-if="selectedDetail.decisionComment" class="mt-1 text-foreground">
                Comentario: {{ selectedDetail.decisionComment }}
              </p>
            </div>
          </div>

          <div v-if="canReadAudit" class="text-xs">
            <RouterLink
              :to="{ name: 'audit', query: { approvalRequestId: selectedDetail.id } }"
              class="inline-flex items-center gap-1 font-medium text-primary hover:underline"
              @click="closeDetail"
            >
              Ver eventos vinculados en Auditoría
              <ExternalLink class="size-3.5" aria-hidden="true" />
            </RouterLink>
          </div>

          <!-- Payloads JSON -->
          <div class="space-y-3">
            <div class="rounded-lg border p-3">
              <h4 class="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Parámetros solicitados (Payload)
              </h4>
              <pre
                class="max-h-52 overflow-auto whitespace-pre-wrap rounded bg-muted/60 p-2 font-mono text-[11px]"
                >{{ formatJson(selectedDetail.payload) }}</pre
              >
            </div>

            <div class="grid grid-cols-1 gap-3 md:grid-cols-2">
              <div class="rounded-lg border p-3">
                <h4 class="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  Snapshot al solicitar
                </h4>
                <pre
                  class="max-h-52 overflow-auto whitespace-pre-wrap rounded bg-muted/60 p-2 font-mono text-[11px]"
                  >{{ formatJson(selectedDetail.targetSnapshot) }}</pre
                >
              </div>
              <div class="rounded-lg border p-3">
                <h4 class="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  Estado actual del recurso
                </h4>
                <pre
                  class="max-h-52 overflow-auto whitespace-pre-wrap rounded bg-muted/60 p-2 font-mono text-[11px]"
                  >{{ formatJson(selectedDetail.currentTargetState) }}</pre
                >
              </div>
            </div>
          </div>

          <!-- Acciones sobre la solicitud -->
          <div class="space-y-3 border-t pt-4">
            <p
              v-if="selectedDetail.status === 'PENDING' && getDecideBlockReason(selectedDetail)"
              class="text-xs text-muted-foreground"
            >
              {{ getDecideBlockReason(selectedDetail) }}
            </p>

            <div class="flex flex-wrap justify-between gap-2">
              <div class="flex flex-wrap gap-2">
                <Button
                  v-if="canDecide(selectedDetail)"
                  type="button"
                  size="sm"
                  @click="openApproveModal(selectedDetail)"
                >
                  <CheckCircle2 class="mr-1.5 size-4" />
                  Aprobar con MFA
                </Button>
                <Button
                  v-if="canDecide(selectedDetail)"
                  type="button"
                  variant="destructive"
                  size="sm"
                  @click="openRejectModal(selectedDetail)"
                >
                  <XCircle class="mr-1.5 size-4" />
                  Rechazar
                </Button>
                <Button
                  v-if="canCancel(selectedDetail)"
                  type="button"
                  variant="outline"
                  size="sm"
                  @click="openCancelDialog(selectedDetail)"
                >
                  <Ban class="mr-1.5 size-4" />
                  Cancelar solicitud
                </Button>
              </div>

              <Button type="button" variant="ghost" size="sm" @click="closeDetail">Cerrar</Button>
            </div>
          </div>
        </div>
      </SheetContent>
    </Sheet>

    <!-- Modal: Aprobar con Step-Up MFA -->
    <div
      v-if="approveTarget"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
      role="dialog"
      aria-modal="true"
      aria-label="Aprobar solicitud con MFA"
    >
      <Card class="w-full max-w-md shadow-xl">
        <CardHeader>
          <CardTitle class="flex items-center gap-2 text-base">
            <ShieldCheck class="size-4 text-primary" />
            Aprobar solicitud ({{ getOperationLabel(approveTarget.operationType) }})
          </CardTitle>
          <CardDescription>
            Esta operación crítica requiere confirmación step-up con tu código TOTP vigente.
          </CardDescription>
        </CardHeader>
        <form novalidate @submit.prevent="submitApprove">
          <CardContent>
            <FieldGroup>
              <Field :data-invalid="!!approveError || undefined">
                <FieldLabel for="approve-mfa-code">Código TOTP (6 dígitos)</FieldLabel>
                <Input
                  id="approve-mfa-code"
                  v-model="approveMfaCode"
                  type="text"
                  inputmode="numeric"
                  maxlength="6"
                  autocomplete="one-time-code"
                  placeholder="000000"
                  class="font-mono tracking-widest"
                  :aria-invalid="!!approveError"
                />
                <FieldError :errors="[approveError ?? undefined]" />
              </Field>
              <Field>
                <FieldLabel for="approve-comment">Comentario (opcional)</FieldLabel>
                <textarea
                  id="approve-comment"
                  v-model="approveComment"
                  rows="2"
                  placeholder="Observaciones de verificación..."
                  class="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
                />
              </Field>
            </FieldGroup>
          </CardContent>
          <CardFooter class="justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              :disabled="approveSubmitting"
              @click="closeApproveModal"
            >
              Cancelar
            </Button>
            <Button type="submit" :disabled="approveSubmitting">
              <LoaderCircle v-if="approveSubmitting" class="animate-spin" aria-hidden="true" />
              Confirmar aprobación
            </Button>
          </CardFooter>
        </form>
      </Card>
    </div>

    <!-- Modal: Rechazar solicitud -->
    <div
      v-if="rejectTarget"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
      role="dialog"
      aria-modal="true"
      aria-label="Rechazar solicitud"
    >
      <Card class="w-full max-w-md shadow-xl">
        <CardHeader>
          <CardTitle class="text-base">
            Rechazar solicitud ({{ getOperationLabel(rejectTarget.operationType) }})
          </CardTitle>
          <CardDescription>
            Indica el motivo del rechazo; quedará registrado en auditoría.
          </CardDescription>
        </CardHeader>
        <form novalidate @submit.prevent="submitReject">
          <CardContent>
            <Field :data-invalid="!!rejectError || undefined">
              <FieldLabel for="reject-comment">Motivo del rechazo</FieldLabel>
              <textarea
                id="reject-comment"
                v-model="rejectComment"
                rows="3"
                placeholder="Explica por qué se rechaza esta solicitud..."
                class="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
                :aria-invalid="!!rejectError"
              />
              <FieldError :errors="[rejectError ?? undefined]" />
            </Field>
          </CardContent>
          <CardFooter class="justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              :disabled="rejectSubmitting"
              @click="closeRejectModal"
            >
              Cancelar
            </Button>
            <Button type="submit" variant="destructive" :disabled="rejectSubmitting">
              <LoaderCircle v-if="rejectSubmitting" class="animate-spin" aria-hidden="true" />
              Confirmar rechazo
            </Button>
          </CardFooter>
        </form>
      </Card>
    </div>

    <!-- Modal: Confirmar cancelación propia -->
    <div
      v-if="cancelTarget"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
      role="dialog"
      aria-modal="true"
      aria-label="Cancelar solicitud propia"
    >
      <Card class="w-full max-w-sm shadow-xl">
        <CardHeader>
          <CardTitle class="text-base">Cancelar solicitud</CardTitle>
          <CardDescription>
            ¿Deseas cancelar la solicitud
            <span class="font-mono">{{ cancelTarget.id }}</span>? No podrá reactivarse.
          </CardDescription>
        </CardHeader>
        <CardFooter class="justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            :disabled="cancelSubmitting"
            @click="closeCancelDialog"
          >
            Volver
          </Button>
          <Button
            type="button"
            variant="destructive"
            :disabled="cancelSubmitting"
            @click="confirmCancel"
          >
            <LoaderCircle v-if="cancelSubmitting" class="animate-spin" aria-hidden="true" />
            Sí, cancelar
          </Button>
        </CardFooter>
      </Card>
    </div>
  </div>
</template>
