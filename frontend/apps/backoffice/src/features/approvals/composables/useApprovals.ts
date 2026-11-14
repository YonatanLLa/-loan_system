import { computed, reactive, ref, watch } from 'vue'
import { toast } from 'vue-sonner'

import {
  type ApprovalRequestDto,
  type ApprovalRequestSummary,
  type ApprovalScope,
  type ApprovalStatus,
  type Page,
  toApiError,
} from '@prestamos/api-client'

import { PERMISSIONS } from '@/app/permissions'
import { useSessionStore } from '@/app/stores/session'

import { approvalsService } from '../services/approvals.service'

export const APPROVAL_PERMISSION_BY_OPERATION: Record<string, string> = {
  PAYMENT_REVERSAL: PERMISSIONS.approvalsPaymentsReverse,
  LOAN_WRITE_OFF: PERMISSIONS.approvalsLoansWriteOff,
  MANUAL_ADJUSTMENT: PERMISSIONS.approvalsLoansAdjust,
  PRODUCT_RATE_CHANGE: PERMISSIONS.approvalsProductsRates,
  LOAN_RESTRUCTURE: PERMISSIONS.approvalsLoansRestructure,
  USER_ROLE_CHANGE: PERMISSIONS.approvalsUsersRole,
}

export const APPROVAL_STATUS_OPTIONS: readonly { value: ApprovalStatus; label: string }[] = [
  { value: 'PENDING', label: 'Pendiente' },
  { value: 'APPROVED', label: 'Aprobada' },
  { value: 'REJECTED', label: 'Rechazada' },
  { value: 'CANCELLED', label: 'Cancelada' },
  { value: 'EXPIRED', label: 'Expirada' },
]

export function useApprovals() {
  const session = useSessionStore()

  const canDecideAny = computed(() =>
    Object.values(APPROVAL_PERMISSION_BY_OPERATION).some((perm) => session.can(perm)),
  )

  const filters = reactive<{
    scope: ApprovalScope
    status: string
    operationType: string
    targetId: string
    page: number
  }>({
    scope: canDecideAny.value ? 'toDecide' : 'all',
    status: '',
    operationType: '',
    targetId: '',
    page: 1,
  })

  const loading = ref(false)
  const approvalsPage = ref<Page<ApprovalRequestSummary>>({
    items: [],
    total: 0,
    page: 1,
    pageSize: 20,
  })

  const totalPages = ref(1)
  watch(
    () => approvalsPage.value.total,
    (total) => {
      totalPages.value = Math.max(1, Math.ceil(total / approvalsPage.value.pageSize))
    },
    { immediate: true },
  )

  /* ── Detalle (Sheet) ── */
  const isDetailOpen = ref(false)
  const detailLoading = ref(false)
  const selectedDetail = ref<ApprovalRequestDto | null>(null)

  /* ── Modales de decisión ── */
  const approveTarget = ref<ApprovalRequestSummary | null>(null)
  const approveMfaCode = ref('')
  const approveComment = ref('')
  const approveError = ref<string | null>(null)
  const approveSubmitting = ref(false)

  const rejectTarget = ref<ApprovalRequestSummary | null>(null)
  const rejectComment = ref('')
  const rejectError = ref<string | null>(null)
  const rejectSubmitting = ref(false)

  const cancelTarget = ref<ApprovalRequestSummary | null>(null)
  const cancelSubmitting = ref(false)

  function currentUserId(): string | undefined {
    return session.user?.userId ?? session.user?.id
  }

  function getDecideBlockReason(approval: ApprovalRequestSummary): string | null {
    if (approval.status !== 'PENDING') {
      return 'Solo se pueden decidir solicitudes pendientes.'
    }
    const requiredPermission = APPROVAL_PERMISSION_BY_OPERATION[approval.operationType]
    if (!requiredPermission || !session.can(requiredPermission)) {
      return 'No cuentas con el permiso específico para aprobar esta operación.'
    }
    const meId = currentUserId()
    if (meId && approval.requestedBy.id === meId) {
      return 'Regla de cuatro ojos: no puedes aprobar o rechazar tu propia solicitud.'
    }
    if (approval.operationType === 'USER_ROLE_CHANGE' && meId && approval.targetId === meId) {
      return 'No puedes decidir un cambio de rol sobre tu propia cuenta.'
    }
    return null
  }

  function canDecide(approval: ApprovalRequestSummary): boolean {
    return getDecideBlockReason(approval) === null
  }

  function canCancel(approval: ApprovalRequestSummary): boolean {
    const meId = currentUserId()
    return approval.status === 'PENDING' && Boolean(meId && approval.requestedBy.id === meId)
  }

  async function loadApprovals() {
    loading.value = true
    try {
      approvalsPage.value = await approvalsService.getApprovals({
        scope: filters.scope,
        status: filters.status || undefined,
        operationType: filters.operationType || undefined,
        targetId: filters.targetId.trim() || undefined,
        page: filters.page,
        pageSize: approvalsPage.value.pageSize,
      })
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al cargar aprobaciones', { description: apiError.detail })
    } finally {
      loading.value = false
    }
  }

  function setScope(scope: ApprovalScope) {
    filters.scope = scope
    filters.page = 1
    void loadApprovals()
  }

  function applyFilters() {
    filters.page = 1
    void loadApprovals()
  }

  function resetFilters() {
    filters.status = ''
    filters.operationType = ''
    filters.targetId = ''
    filters.page = 1
    void loadApprovals()
  }

  function goToPage(page: number) {
    if (page < 1 || page > totalPages.value) return
    filters.page = page
    void loadApprovals()
  }

  async function openDetail(id: string) {
    isDetailOpen.value = true
    detailLoading.value = true
    try {
      selectedDetail.value = await approvalsService.getApprovalById(id)
    } catch (error) {
      const apiError = toApiError(error)
      toast.error(apiError.title, { description: apiError.detail })
      isDetailOpen.value = false
      selectedDetail.value = null
    } finally {
      detailLoading.value = false
    }
  }

  function closeDetail() {
    isDetailOpen.value = false
    selectedDetail.value = null
  }

  function openApproveModal(approval: ApprovalRequestSummary) {
    approveTarget.value = approval
    approveMfaCode.value = ''
    approveComment.value = ''
    approveError.value = null
  }

  function closeApproveModal() {
    approveTarget.value = null
    approveMfaCode.value = ''
    approveComment.value = ''
    approveError.value = null
  }

  async function submitApprove() {
    if (!approveTarget.value) return
    const cleanedCode = approveMfaCode.value.trim()
    if (!/^\d{6}$/.test(cleanedCode)) {
      approveError.value = 'Ingresa un código TOTP válido de 6 dígitos.'
      return
    }

    approveError.value = null
    approveSubmitting.value = true
    const targetId = approveTarget.value.id

    try {
      await approvalsService.approveApproval(targetId, {
        mfaCode: cleanedCode,
        comment: approveComment.value.trim() || undefined,
      })
      toast.success('Solicitud aprobada y ejecutada')
      closeApproveModal()
      await loadApprovals()
      if (isDetailOpen.value && selectedDetail.value?.id === targetId) {
        await openDetail(targetId)
      }
    } catch (error) {
      const apiError = toApiError(error)
      if (apiError.code === 'Approvals.InvalidMfaCode') {
        approveError.value =
          apiError.detail || 'Código TOTP inválido. Verifica tu aplicación autenticadora.'
        return
      }
      if (apiError.code === 'Approvals.TargetChanged') {
        approveError.value =
          apiError.detail || 'El recurso objetivo cambió desde que se creó la solicitud.'
        toast.error(apiError.title, { description: approveError.value })
        await loadApprovals()
        if (isDetailOpen.value && selectedDetail.value?.id === targetId) {
          await openDetail(targetId)
        }
        return
      }
      approveError.value = apiError.detail || apiError.title
      toast.error(apiError.title, { description: apiError.detail })
    } finally {
      approveSubmitting.value = false
    }
  }

  function openRejectModal(approval: ApprovalRequestSummary) {
    rejectTarget.value = approval
    rejectComment.value = ''
    rejectError.value = null
  }

  function closeRejectModal() {
    rejectTarget.value = null
    rejectComment.value = ''
    rejectError.value = null
  }

  async function submitReject() {
    if (!rejectTarget.value) return
    const cleanedComment = rejectComment.value.trim()
    if (!cleanedComment) {
      rejectError.value = 'Ingresa el motivo del rechazo.'
      return
    }

    rejectError.value = null
    rejectSubmitting.value = true
    const targetId = rejectTarget.value.id

    try {
      await approvalsService.rejectApproval(targetId, { comment: cleanedComment })
      toast.success('Solicitud rechazada')
      closeRejectModal()
      await loadApprovals()
      if (isDetailOpen.value && selectedDetail.value?.id === targetId) {
        await openDetail(targetId)
      }
    } catch (error) {
      const apiError = toApiError(error)
      rejectError.value = apiError.detail || apiError.title
      toast.error(apiError.title, { description: apiError.detail })
    } finally {
      rejectSubmitting.value = false
    }
  }

  function openCancelDialog(approval: ApprovalRequestSummary) {
    cancelTarget.value = approval
  }

  function closeCancelDialog() {
    cancelTarget.value = null
  }

  async function confirmCancel() {
    if (!cancelTarget.value) return
    const targetId = cancelTarget.value.id
    cancelSubmitting.value = true
    try {
      await approvalsService.cancelApproval(targetId)
      toast.success('Solicitud cancelada')
      closeCancelDialog()
      await loadApprovals()
      if (isDetailOpen.value && selectedDetail.value?.id === targetId) {
        await openDetail(targetId)
      }
    } catch (error) {
      const apiError = toApiError(error)
      toast.error(apiError.title, { description: apiError.detail })
    } finally {
      cancelSubmitting.value = false
    }
  }

  return {
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
  }
}
