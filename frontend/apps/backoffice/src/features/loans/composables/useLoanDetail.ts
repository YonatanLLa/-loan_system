import { ref, computed, toValue, watch, type MaybeRefOrGetter } from 'vue'
import { useRouter } from 'vue-router'
import { toast } from 'vue-sonner'
import type {
  AgingBucket,
  AgingRow,
  CollectionAction,
  Disbursement,
  Loan,
  LoanSummary,
  Payment,
  Schedule,
} from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import type { SummaryItem } from '@prestamos/ui'
import {
  collectionsService,
  type CollectorOption,
} from '@/features/collections'
import { useLoans } from './useLoans'

export function useLoanDetail(loanIdSource: MaybeRefOrGetter<string>) {
  const router = useRouter()
  const loanId = computed(() => toValue(loanIdSource))
  const {
    getLoan,
    getSchedule,
    getLoanPayments,
    getCustomer,
    getDisbursement,
    downloadContractPdf: downloadContractPdfBlob,
  } = useLoans()

  const loading = ref(true)
  const loan = ref<Loan | null>(null)
  const schedule = ref<Schedule | null>(null)
  const payments = ref<Payment[]>([])
  const disbursement = ref<Disbursement | null>(null)
  const collectionActions = ref<CollectionAction[]>([])
  const collectors = ref<CollectorOption[]>([])
  const customerPhone = ref('')
  const customerLoans = ref<LoanSummary[]>([])
  const selectedVersion = ref(1)
  const isDownloadingPdf = ref(false)

  const installmentProgress = computed(() => {
    const list = schedule.value?.installments ?? []
    const totalCount = list.length
    const paidCount = list.filter(
      (i) => i.status === 'PAID' || i.status === 'CANCELLED_BY_PREPAYMENT',
    ).length
    const partialCount = list.filter((i) => i.status === 'PARTIAL').length
    const overdueCount = list.filter((i) => i.status === 'OVERDUE').length
    const pendingCount = list.filter((i) => i.status === 'PENDING').length
    const progressPercent = totalCount > 0 ? Math.round((paidCount / totalCount) * 100) : 0
    const nextInstallment =
      list.find(
        (i) => i.status === 'OVERDUE' || i.status === 'PARTIAL' || i.status === 'PENDING',
      ) ?? null
    return {
      totalCount,
      paidCount,
      partialCount,
      overdueCount,
      pendingCount,
      progressPercent,
      nextInstallment,
    }
  })

  const selectedAgingRow = computed<AgingRow | null>(() => {
    if (!loan.value) return null
    const overdueInsts = (schedule.value?.installments ?? []).filter(
      (i) => i.status === 'OVERDUE' || (i.daysLate != null && i.daysLate > 0),
    )
    const maxLate = Math.max(0, ...overdueInsts.map((i) => i.daysLate ?? 0))
    let bucket: AgingBucket = 'PREVENTIVO'
    if (maxLate > 90) bucket = 'TRAMO_4'
    else if (maxLate > 60) bucket = 'TRAMO_3'
    else if (maxLate > 30) bucket = 'TRAMO_2'
    else if (maxLate > 0) bucket = 'TRAMO_1'

    const overduePrincipal = overdueInsts.reduce(
      (acc, i) => acc + (Number(i.principal) - Number(i.paidPrincipal || 0)),
      0,
    )
    const totalDue = overdueInsts.reduce(
      (acc, i) =>
        acc +
        (Number(i.totalDue) +
          Number(i.lateFee || 0) -
          Number(i.paidPrincipal || 0) -
          Number(i.paidInterest || 0) -
          Number(i.paidFee || 0) -
          Number(i.paidLateFee || 0)),
      0,
    )

    const hasBrokenPromise = collectionActions.value.some(
      (a) => a.status === 'PROMESA_INCUMPLIDA',
    )

    return {
      loanId: loan.value.id,
      customerId: loan.value.customerId,
      customerName: loan.value.customerName,
      phone: customerPhone.value || '987000000',
      productName: loan.value.productName,
      bucket,
      daysLate: maxLate,
      overdueInstallments: overdueInsts.length,
      overduePrincipal: overduePrincipal.toFixed(2),
      totalDue: totalDue.toFixed(2),
      assignedTo: loan.value.assignedTo,
      hasBrokenPromise,
    }
  })

  const contractSummaryItems = computed<SummaryItem[]>(() => {
    if (!loan.value) return []
    return [
      { label: 'Capital Aprobado:', money: loan.value.principal },
      { label: 'Monto Desembolsado:', money: loan.value.disbursedAmount },
      { label: 'Saldo Insoluto:', money: loan.value.outstandingPrincipal, valueClass: 'text-primary' },
      { label: 'Total Amortizado:', money: loan.value.totalPaid, valueClass: 'text-success' },
      {
        label: 'TEA / Frecuencia:',
        value: `${(Number(loan.value.annualRate) * 100).toFixed(1)}% · ${loan.value.frequency}`,
        valueClass: 'font-semibold text-foreground text-xs',
      },
      {
        label: 'Método Amortización:',
        value: loan.value.amortizationMethod,
        valueClass: 'font-semibold text-foreground text-xs',
      },
    ]
  })

  const collectionSummaryItems = computed<SummaryItem[]>(() => {
    if (!loan.value) return []
    return [
      {
        label: 'Gestor Asignado:',
        value: loan.value.assignedTo || 'Sin asignar',
        valueClass: 'font-semibold',
      },
      {
        label: 'Días de Atraso Máximo:',
        value: selectedAgingRow.value?.daysLate
          ? `${selectedAgingRow.value.daysLate} días (${selectedAgingRow.value.bucket})`
          : 'Al día',
        valueClass:
          (selectedAgingRow.value?.daysLate ?? 0) > 0
            ? 'text-destructive font-bold'
            : 'font-semibold text-foreground',
      },
      {
        label: 'Mora / Total Exigible:',
        money: selectedAgingRow.value?.totalDue || '0.00',
      },
    ]
  })

  async function loadLoanData() {
    loading.value = true
    try {
      const loanData = await getLoan(loanId.value)
      loan.value = loanData
      selectedVersion.value = loanData.scheduleVersion

      const [schedData, payData, actionsData] = await Promise.all([
        getSchedule(loanId.value, loanData.scheduleVersion),
        getLoanPayments(loanId.value),
        collectionsService
          .fetchCollectionActions(loanId.value)
          .catch(() => [] as CollectionAction[]),
      ])
      schedule.value = schedData
      payments.value = payData
      collectionActions.value = actionsData

      try {
        const [peopleList, custData] = await Promise.all([
          collectionsService.fetchCollectors(),
          getCustomer(loanData.customerId),
        ])
        collectors.value = peopleList
        customerPhone.value = custData.phone ?? ''
        customerLoans.value = custData.loans ?? []
      } catch {
        // Fallback opcional de catálogos secundarios
      }

      if (loanData.disbursementId) {
        try {
          disbursement.value = await getDisbursement(loanData.disbursementId)
        } catch {
          // Fallback si la ruta de tesorería aún no está montada
        }
      }
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al cargar detalle del préstamo', { description: apiError.detail })
      void router.replace({ name: 'loans' })
    } finally {
      loading.value = false
    }
  }

  async function onVersionChange() {
    if (!loan.value) return
    try {
      schedule.value = await getSchedule(loanId.value, selectedVersion.value)
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al cargar versión del cronograma', { description: apiError.detail })
    }
  }

  async function onCollectionMutation() {
    await loadLoanData()
  }

  async function downloadContractPdf() {
    isDownloadingPdf.value = true
    try {
      const blob = await downloadContractPdfBlob(loanId.value)
      const url = window.URL.createObjectURL(new Blob([blob], { type: 'application/pdf' }))
      const link = document.createElement('a')
      link.href = url
      link.setAttribute('download', `Contrato-${loanId.value}.pdf`)
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
      toast.success('Contrato descargado exitosamente')
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al descargar contrato', { description: apiError.detail })
    } finally {
      isDownloadingPdf.value = false
    }
  }

  watch(loanId, (newId, oldId) => {
    if (newId && oldId && newId !== oldId) {
      void loadLoanData()
    }
  })

  return {
    loanId,
    loading,
    loan,
    schedule,
    payments,
    disbursement,
    collectionActions,
    collectors,
    customerPhone,
    customerLoans,
    selectedVersion,
    isDownloadingPdf,
    installmentProgress,
    selectedAgingRow,
    contractSummaryItems,
    collectionSummaryItems,
    loadLoanData,
    onVersionChange,
    onCollectionMutation,
    downloadContractPdf,
  }
}
