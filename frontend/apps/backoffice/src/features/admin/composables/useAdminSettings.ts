import { computed, onMounted, reactive, ref } from 'vue'
import { toast } from 'vue-sonner'
import { z } from 'zod'

import {
  type ApprovalRequestDto,
  type SecurityPingResponse,
  type SecurityRole,
  toApiError,
} from '@prestamos/api-client'

import { adminService } from '../services/admin.service'

export interface RoleMatrixEntry {
  role: SecurityRole
  label: string
  description: string
  permissions: readonly string[]
}

/**
 * Matriz de referencia de roles y permisos (solo lectura), espejo exacto de
 * `RolePermissions.cs` en el backend.
 */
export const SECURITY_ROLES_MATRIX: readonly RoleMatrixEntry[] = [
  {
    role: 'ROLE_ADMIN',
    label: 'Administrador',
    description: 'Acceso completo operativo, administración de usuarios y aprobaciones de cuatro ojos.',
    permissions: [
      'dashboard.view',
      'customers.read',
      'applications.read',
      'loans.read',
      'payments.read',
      'payments.reverse',
      'treasury.read',
      'integrations.read',
      'collections.read',
      'reports.read',
      'audit.read',
      'products.manage',
      'admin.manage',
      'approvals.read',
      'approvals.payments.reverse',
      'approvals.loans.write_off',
      'approvals.loans.adjust',
      'approvals.products.rates',
      'approvals.loans.restructure',
      'approvals.users.role',
    ],
  },
  {
    role: 'ROLE_ANALYST',
    label: 'Analista de crédito',
    description: 'Consulta de clientes, solicitudes, préstamos y reportes.',
    permissions: ['dashboard.view', 'customers.read', 'applications.read', 'loans.read', 'reports.read'],
  },
  {
    role: 'ROLE_OPERATOR',
    label: 'Operador financiero',
    description: 'Operación diaria de solicitudes, pagos, tesorería e integraciones.',
    permissions: [
      'dashboard.view',
      'customers.read',
      'applications.read',
      'loans.read',
      'payments.read',
      'treasury.read',
      'integrations.read',
      'reports.read',
    ],
  },
  {
    role: 'ROLE_COLLECTOR',
    label: 'Gestor de cobranzas',
    description: 'Seguimiento de cartera vencida y gestión de cobranza.',
    permissions: ['dashboard.view', 'customers.read', 'loans.read', 'collections.read'],
  },
  {
    role: 'ROLE_AUDITOR',
    label: 'Auditor interno',
    description: 'Consulta de solo lectura sobre auditoría, reportes, cartera y aprobaciones.',
    permissions: [
      'dashboard.view',
      'audit.read',
      'reports.read',
      'customers.read',
      'loans.read',
      'payments.read',
      'treasury.read',
      'approvals.read',
    ],
  },
  {
    role: 'ROLE_CLIENT',
    label: 'Cliente (Portal)',
    description: 'Acceso exclusivo al Portal de Clientes; bloqueado para Backoffice.',
    permissions: ['client.portal'],
  },
] as const

export type UserSecurityAction = 'suspend' | 'activate' | 'resetMfa'

const uuidSchema = z
  .string()
  .trim()
  .min(1, 'Ingresa el UUID del usuario')
  .uuid('Debe ser un UUID válido (ej. 550e8400-e29b-41d4-a716-446655440000)')

const ACTION_META: Record<
  UserSecurityAction,
  { title: string; confirmLabel: string; successToast: string }
> = {
  suspend: {
    title: 'Suspender usuario',
    confirmLabel: 'Confirmar suspensión',
    successToast: 'Usuario suspendido correctamente',
  },
  activate: {
    title: 'Activar usuario',
    confirmLabel: 'Confirmar activación',
    successToast: 'Usuario activado correctamente',
  },
  resetMfa: {
    title: 'Reiniciar MFA del usuario',
    confirmLabel: 'Confirmar reinicio MFA',
    successToast: 'MFA reiniciado; el usuario deberá enrolarse en su próximo ingreso',
  },
}

export function useAdminSettings(options: { autoLoad?: boolean } = {}) {
  const { autoLoad = true } = options

  const pingResult = ref<SecurityPingResponse | null>(null)
  const pingLoading = ref(false)
  const pingError = ref<string | null>(null)

  const targetUserId = ref('')
  const targetUserIdError = ref<string | null>(null)

  const pendingAction = ref<UserSecurityAction | null>(null)
  const actionBusy = ref(false)
  const lastActionFeedback = ref<string | null>(null)

  const roleForm = reactive<{ newRole: SecurityRole; reason: string }>({
    newRole: 'ROLE_ANALYST',
    reason: '',
  })
  const roleReasonError = ref<string | null>(null)
  const roleSubmitting = ref(false)
  const createdApproval = ref<ApprovalRequestDto | null>(null)

  const isValidUserId = computed(() => uuidSchema.safeParse(targetUserId.value).success)
  const pendingActionMeta = computed(() =>
    pendingAction.value ? ACTION_META[pendingAction.value] : null,
  )

  function validateUserId(): string | null {
    const parsed = uuidSchema.safeParse(targetUserId.value)
    if (!parsed.success) {
      targetUserIdError.value = parsed.error.issues[0]?.message ?? 'UUID inválido'
      return null
    }
    targetUserIdError.value = null
    return parsed.data
  }

  async function checkSecurityPing() {
    pingLoading.value = true
    pingError.value = null
    try {
      pingResult.value = await adminService.pingSecurity()
    } catch (error) {
      const apiError = toApiError(error)
      pingError.value = apiError.detail || apiError.title
      toast.error(apiError.title, { description: apiError.detail })
    } finally {
      pingLoading.value = false
    }
  }

  function requestActionConfirmation(action: UserSecurityAction) {
    if (!validateUserId()) return
    pendingAction.value = action
  }

  function cancelActionConfirmation() {
    pendingAction.value = null
  }

  async function executePendingAction() {
    const action = pendingAction.value
    const userId = validateUserId()
    if (!action || !userId) return

    actionBusy.value = true
    try {
      if (action === 'suspend') {
        await adminService.suspendUser(userId)
      } else if (action === 'activate') {
        await adminService.activateUser(userId)
      } else {
        await adminService.resetUserMfa(userId)
      }
      const message = `${ACTION_META[action].successToast} (${userId}).`
      lastActionFeedback.value = message
      toast.success(ACTION_META[action].successToast)
      pendingAction.value = null
    } catch (error) {
      const apiError = toApiError(error)
      toast.error(apiError.title, { description: apiError.detail })
    } finally {
      actionBusy.value = false
    }
  }

  async function submitRoleChange() {
    const userId = validateUserId()
    const trimmedReason = roleForm.reason.trim()
    if (trimmedReason.length < 10) {
      roleReasonError.value = 'La justificación debe tener al menos 10 caracteres.'
    } else {
      roleReasonError.value = null
    }

    if (!userId || roleReasonError.value) return

    roleSubmitting.value = true
    try {
      const approval = await adminService.requestUserRoleChange(userId, {
        newRole: roleForm.newRole,
        reason: trimmedReason,
      })
      createdApproval.value = approval
      roleForm.reason = ''
      toast.success('Solicitud de cambio de rol creada', {
        description: `Solicitud ${approval.id} pendiente de segunda aprobación.`,
      })
    } catch (error) {
      const apiError = toApiError(error)
      toast.error(apiError.title, { description: apiError.detail })
    } finally {
      roleSubmitting.value = false
    }
  }

  if (autoLoad) {
    onMounted(() => {
      void checkSecurityPing()
    })
  }

  return {
    SECURITY_ROLES_MATRIX,
    pingResult,
    pingLoading,
    pingError,
    targetUserId,
    targetUserIdError,
    isValidUserId,
    pendingAction,
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
  }
}
