import { http, HttpResponse } from 'msw'
import type {
  ChangePrimaryBankAccountRequest,
  ComplaintClaimRecord,
  ComplaintClaimRequest,
  CustomerBankAccount,
  NotificationPreferences,
  PartialPrepaymentSimulation,
  RefundRequest,
  SimulateRequest,
} from '@prestamos/api-client'

import { addDays, today } from '../dates'
import { db, nextId, nowIso, recordAudit } from '../db'
import {
  activeInstallments,
  holidaySet,
  isSettledOrCancelled,
  outstandingPrincipal,
  quotePrepayment,
  refreshLoan,
  simulateLoan,
  toInstallmentDto,
  toLoanDto,
  toPaymentDto,
} from '../domain'
import { idempotent, notFound, pdfResponse, problem, readJson, secured } from '../http-utils'
import { fromCents, sum, toCents } from '../money'
import { buildSchedule, periodRate } from '../schedule'
import { createLoanRecord } from '../seed'
import { statement } from './collections-reports'

/** Préstamos del cliente autenticado; un préstamo ajeno responde 404 (RF-26.5). */
const ownLoan = (loanId: string, customerId?: string) =>
  db.loans.find((l) => l.id === loanId && l.customerId === customerId)

interface MockNotification {
  id: string
  customerId: string
  title: string
  message: string
  type: 'PAYMENT_DUE' | 'PAYMENT_CONFIRMED' | 'DISBURSEMENT_READY' | 'APPLICATION_UPDATE'
  isRead: boolean
  createdAt: string
  actionUrl?: string
}

interface MockComplaint {
  id: string
  customerId: string
  filingNumber: string
  type: 'QUEJA' | 'RECLAMO'
  detail: string
  requestedSolution: string
  status: 'RECEIVED' | 'IN_REVIEW' | 'RESOLVED'
  createdAt: string
}

const clientNotifications: MockNotification[] = [
  {
    id: 'not-001',
    customerId: 'cus-001',
    title: 'Próximo vencimiento de cuota',
    message:
      'Tu próxima cuota del Crédito Personal Digital vence pronto. Paga a tiempo con Yape o Plin.',
    type: 'PAYMENT_DUE',
    isRead: false,
    createdAt: `${today()}T08:00:00Z`,
    actionUrl: '/(app)/loans/loan-001',
  },
  {
    id: 'not-002',
    customerId: 'cus-001',
    title: 'Solicitud en evaluación',
    message: 'Tu solicitud APP-008 por S/ 2,500.00 se encuentra en evaluación crediticia.',
    type: 'APPLICATION_UPDATE',
    isRead: false,
    createdAt: `${today()}T08:20:00Z`,
    actionUrl: '/(app)/applications',
  },
  {
    id: 'not-003',
    customerId: 'cus-001',
    title: 'Pago confirmado',
    message: 'Hemos aplicado exitosamente el pago de tu cuota #2. Tu recibo PDF está disponible.',
    type: 'PAYMENT_CONFIRMED',
    isRead: true,
    createdAt: `${addDays(today(), -15)}T15:05:00Z`,
  },
]

const clientComplaints: MockComplaint[] = []

export const clientHandlers = [
  // ── Contrato y Firma Digital (FE-CP-09) ──────────────────────────────────────
  http.get(
    '*/api/client/contract',
    secured('client.portal', ({ url, user }) => {
      const appId = url.searchParams.get('applicationId')
      const application = appId
        ? db.applications.find((a) => a.id === appId && a.customerId === user.customerId)
        : db.applications.find(
            (a) =>
              a.customerId === user.customerId &&
              (a.status === 'APROBADO' ||
                a.status === 'APROBADO_CON_CONDICIONES' ||
                a.status === 'SOLICITADO'),
          )

      if (!application) {
        return problem(
          404,
          'No hay contrato pendiente',
          'No tienes ninguna solicitud aprobada pendiente de firma.',
        )
      }

      const product = db.products.find((p) => p.id === application.productId) || db.products[0]!
      const customer = db.customers.find((c) => c.id === user.customerId)
      const primaryBank = db.customerBankAccounts.find(
        (a) => a.customerId === user.customerId && a.isPrimary,
      ) ||
        db.customerBankAccounts.find((a) => a.customerId === user.customerId) || {
          bankName: 'Banco de Crédito BCP',
          accountNumberMasked: '**** 1234',
          cci: '00219000000000010000',
        }

      const amountCents = toCents(application.approvedAmount ?? application.requestedAmount)
      const sim = simulateLoan(product, amountCents, application.term, application.frequency)

      const clauses = [
        {
          title: 'PRIMERA: OBJETO DEL CONTRATO DE CRÉDITO DIGITAL',
          body: `FinCredit Perú otorga al Prestatario un crédito dinerario por la suma aprobada de S/ ${fromCents(amountCents)}, transferible a la cuenta bancaria de titularidad del Prestatario en ${primaryBank.bankName}.`,
        },
        {
          title: 'SEGUNDA: CONDICIONES FINANCIERAS Y COSTO TOTAL (TCEA)',
          body: `El crédito devengará una Tasa Efectiva Anual (TEA) de ${(Number(product.annualRate) * 100).toFixed(2)}% y una Tasa de Costo Efectivo Anual (TCEA) de ${(Number(sim.summary.tcea) * 100).toFixed(2)}%, la cual incluye intereses compensatorios, comisiones y cargos reglamentados por la SBS.`,
        },
        {
          title: 'TERCERA: CRONOGRAMA DE PAGOS Y MORA',
          body: `El crédito será amortizado en ${application.term} cuotas periódicas de vencimiento mensual. En caso de incumplimiento, se aplicará una penalidad diaria por mora de ${product.moraRatePerDay}% sobre el saldo de capital vencido.`,
        },
        {
          title: 'CUARTA: DERECHO DE PREPAGO Y CANCELACIÓN ANTICIPADA',
          body: 'Conforme al artículo 85 de la Ley N° 29571 (Código de Protección y Defensa del Consumidor), el Prestatario tiene derecho irrevocable a realizar pagos anticipados totales o parciales sin penalidad ni comisión alguna, con liquidación de intereses al día efectivo de pago.',
        },
        {
          title: 'QUINTA: FIRMA ELECTRÓNICA Y VALIDEZ LEGAL',
          body: 'Las partes convienen expresamente que la aceptación electrónica mediante clave de un solo uso (OTP) enviada al número celular registrado tiene plena validez jurídica, equivalencia funcional y eficacia legal con arreglo a la Ley N° 27269 de Firmas y Certificados Digitales.',
        },
      ]

      return HttpResponse.json({
        applicationId: application.id,
        loanId: db.loans.find((l) => l.applicationId === application.id)?.id,
        customerName: customer?.fullName || user.fullName,
        docNumber: customer?.docNumber || '72345678',
        productName: product.name,
        principal: fromCents(amountCents),
        term: application.term,
        frequency: application.frequency,
        tea: sim.summary.tea,
        tcea: sim.summary.tcea,
        monthlyInstallment: sim.summary.installmentAmount,
        totalInterest: sim.summary.totalInterest,
        totalFees: sim.summary.totalFees,
        totalCost: sim.summary.totalCost,
        disbursementAccount: primaryBank.cci || primaryBank.accountNumberMasked,
        disbursementBank: primaryBank.bankName,
        clauses,
        schedule: sim.installments,
        isSigned: application.status === 'DESEMBOLSADO',
        signatureEvidence:
          application.status === 'DESEMBOLSADO'
            ? {
                signedAt: nowIso(),
                signedIp: '190.235.12.84',
                otpCode: '123456',
                verified: true,
              }
            : undefined,
      })
    }),
  ),

  http.post(
    '*/api/client/contract/sign',
    secured('client.portal', async ({ request, user }) => {
      const body = await readJson<{ applicationId: string; otpCode: string }>(request)
      if (!body?.otpCode?.trim()) {
        return problem(400, 'Código OTP requerido', 'Ingresa el código OTP de 6 dígitos.')
      }
      if (body.otpCode.trim() !== '123456') {
        return problem(
          400,
          'Código OTP incorrecto',
          'El código ingresado es inválido o ha expirado. (Usa 123456 en demo).',
        )
      }

      const application = db.applications.find(
        (a) => a.id === body.applicationId && a.customerId === user.customerId,
      )
      if (!application) return notFound('La solicitud')

      // Crea el préstamo y desembolso si aún no existe
      let loan = db.loans.find((l) => l.applicationId === application.id)
      if (!loan) {
        const primaryAccount =
          db.customerBankAccounts.find((a) => a.customerId === user.customerId && a.isPrimary) ||
          db.customerBankAccounts.find((a) => a.customerId === user.customerId) ||
          db.customerBankAccounts[0]

        loan = createLoanRecord(application, primaryAccount ? primaryAccount.id : 'cba-001')
        application.status = 'DESEMBOLSADO'

        const disbursementId = nextId('dis')
        loan.disbursementId = disbursementId
        db.disbursements.unshift({
          id: disbursementId,
          loanId: loan.id,
          customerName: user.fullName,
          amount: fromCents(loan.disbursed),
          channel: 'TRANSFERENCIA_BANCARIA',
          status: 'COMPLETED',
          companyBankAccountId: db.companyAccounts[0]?.id || 'bac-001',
          customerBankAccountId: primaryAccount ? primaryAccount.id : 'cba-001',
          initiatedAt: nowIso(),
          createdMs: Date.now(),
        })

        recordAudit({
          entityName: 'loan_contracts',
          entityId: application.id,
          action: 'ELECTRONIC_SIGNATURE',
          after: { loanId: loan.id, otp: '123456', ip: '190.235.12.84' },
          userName: user.fullName,
        })
      }

      return HttpResponse.json({
        success: true,
        loanId: loan.id,
        signedAt: nowIso(),
        message:
          'Contrato y pagaré electrónico firmados exitosamente. Tu desembolso fue procesado.',
      })
    }),
  ),

  http.get(
    '*/api/client/contract/document.pdf',
    secured('client.portal', () => pdfResponse('Contrato y Pagaré Digital Aceptado')),
  ),
  http.get(
    '*/api/client/loans',
    secured('client.portal', ({ user }) =>
      HttpResponse.json(db.loans.filter((l) => l.customerId === user.customerId).map(toLoanDto)),
    ),
  ),

  http.get(
    '*/api/client/loans/:id',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      return loan ? HttpResponse.json(toLoanDto(loan)) : notFound('El préstamo')
    }),
  ),

  http.get(
    '*/api/client/loans/:id/schedule',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      if (!loan) return notFound('El préstamo')
      return HttpResponse.json({
        loanId: loan.id,
        version: loan.scheduleVersion,
        installments: activeInstallments(loan).map(toInstallmentDto),
      })
    }),
  ),

  http.get(
    '*/api/client/loans/:id/payments',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      if (!loan) return notFound('El préstamo')
      return HttpResponse.json(db.payments.filter((p) => p.loanId === loan.id).map(toPaymentDto))
    }),
  ),

  http.get(
    '*/api/client/loans/:id/statement',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      return loan ? HttpResponse.json(statement(loan.id)) : notFound('El préstamo')
    }),
  ),

  http.get(
    '*/api/client/loans/:id/statement.pdf',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      return loan ? pdfResponse(`Estado de cuenta ${loan.id}`) : notFound('El préstamo')
    }),
  ),

  http.post(
    '*/api/client/loans/:id/prepayment/quote',
    secured<{ id: string }>('client.portal', async ({ request, params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      if (!loan) return notFound('El préstamo')
      const body = await readJson<{
        type?: 'TOTAL' | 'PARTIAL'
        amount?: string
        reductionOption?: 'REDUCE_TERM' | 'REDUCE_INSTALLMENT'
        asOfDate?: string
      }>(request)
      const quote = quotePrepayment(loan, body?.asOfDate ?? today())
      return HttpResponse.json({
        ...quote,
        type: body?.type ?? 'TOTAL',
        reductionOption: body?.reductionOption,
        interestToDate: quote.accruedInterest,
        penaltyFee: quote.prepaymentPenalty,
        totalPayable: quote.totalToPay,
        expiresAt: `${quote.validUntil}T23:59:59Z`,
      })
    }),
  ),

  http.get(
    '*/api/client/loans/:id/contract',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      if (!loan) return notFound('El préstamo')
      return HttpResponse.json({
        loanId: loan.id,
        contractNumber: `CTR-${loan.id.toUpperCase()}`,
        pdfUrl: `/api/client/loans/${loan.id}/documents/contract.pdf`,
        summarySheetPdfUrl: `/api/client/loans/${loan.id}/documents/summary.pdf`,
        isSigned: loan.status !== 'PENDIENTE_DESEMBOLSO',
        signedAt: loan.status !== 'PENDIENTE_DESEMBOLSO' ? loan.createdAt : undefined,
      })
    }),
  ),

  http.get(
    '*/api/client/loans/:id/documents/contract.pdf',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      return loan ? pdfResponse(`Contrato ${loan.id}`) : notFound('El préstamo')
    }),
  ),

  http.post(
    '*/api/client/loans/:id/request-sign-otp',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      if (!loan) return notFound('El préstamo')
      return HttpResponse.json({ sent: true })
    }),
  ),

  http.post(
    '*/api/client/loans/:id/sign',
    secured<{ id: string }>('client.portal', async ({ request, params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      if (!loan) return notFound('El préstamo')
      const body = await readJson<{ otpCode?: string }>(request)
      if (!body?.otpCode || !/^\d{6}$/.test(body.otpCode)) {
        return problem(400, 'Código OTP inválido', 'Ingresa el código de 6 dígitos.')
      }
      const signedAt = nowIso()
      recordAudit({
        entityName: 'loans',
        entityId: loan.id,
        action: 'SIGN_CONTRACT',
        after: { signedAt },
        userName: user.fullName,
      })
      return HttpResponse.json({ success: true, signedAt })
    }),
  ),

  http.post(
    '*/api/client/loan-applications/simulate',
    secured('client.portal', async ({ request }) => {
      const body = await readJson<SimulateRequest & { termMonths?: number }>(request)
      const product =
        db.products.find((p) => p.id === body?.productId) ??
        db.products.find((p) => p.status === 'ACTIVO') ??
        db.products[0]!
      const term = Number(body?.term ?? body?.termMonths ?? 12)
      const amount = body?.amount ?? '1000.00'
      const result = simulateLoan(
        product,
        toCents(amount),
        term,
        body?.frequency ?? product.allowedFrequencies[0] ?? 'MENSUAL',
        body?.firstDueDate,
      )
      return HttpResponse.json({
        ...result,
        monthlyInstallment: result.summary.installmentAmount,
        tea: `${(Number(result.summary.tea) * 100).toFixed(2)}%`,
        tcea: `${(Number(result.summary.tcea) * 100).toFixed(2)}%`,
        totalCost: result.summary.totalCost,
        installmentsCount: result.installments.length,
      })
    }),
  ),

  http.get(
    '*/api/client/loan-applications/:id',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const app = db.applications.find(
        (a) => a.id === params.id && a.customerId === user.customerId,
      )
      return app ? HttpResponse.json(app) : notFound('La solicitud')
    }),
  ),

  http.get(
    '*/api/client/profile',
    secured('client.portal', ({ user }) => {
      const customer = db.customers.find((c) => c.id === user.customerId) ?? db.customers[0]
      return customer ? HttpResponse.json(customer) : notFound('El perfil del cliente')
    }),
  ),

  http.put(
    '*/api/client/profile',
    secured('client.portal', async ({ request, user }) => {
      const customer = db.customers.find((c) => c.id === user.customerId)
      if (!customer) return notFound('El perfil del cliente')
      const body = await readJson<{
        phone?: string
        address?: string
        district?: string
        email?: string
      }>(request)
      if (body?.phone) customer.phone = body.phone
      if (body?.address) customer.address = body.address
      if (body?.district) customer.district = body.district
      if (body?.email) customer.email = body.email
      return HttpResponse.json(customer)
    }),
  ),

  http.post(
    '*/api/client/profile/refund-credit-balance',
    secured('client.portal', async ({ request, user }) =>
      idempotent(request, () => {
        const customer = db.customers.find((c) => c.id === user.customerId)
        if (!customer) return notFound('El cliente')
        customer.creditBalance = '0.00'
        return {
          status: 200,
          body: { refundId: nextId('ref'), status: 'PROCESSING' },
        }
      }),
    ),
  ),

  http.post(
    '*/api/client/documents',
    secured('client.portal', () =>
      HttpResponse.json({ documentId: nextId('doc') }, { status: 201 }),
    ),
  ),

  http.post(
    '*/api/client/consents',
    secured('client.portal', () => HttpResponse.json({ accepted: true, recordedAt: nowIso() })),
  ),

  http.get(
    '*/api/client/bank-accounts',
    secured('client.portal', ({ user }) =>
      HttpResponse.json(db.customerBankAccounts.filter((a) => a.customerId === user.customerId)),
    ),
  ),

  // Cotización de prepago total para el portal del cliente (FE-CP-16 / US-30)
  http.post(
    '*/api/client/loans/:id/quote-prepayment',
    secured<{ id: string }>('client.portal', async ({ request, params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      if (!loan) return notFound('El préstamo')
      if (loan.status !== 'VIGENTE' && loan.status !== 'EN_MORA') {
        return problem(
          422,
          'Préstamo no cancelable',
          'Solo se cotiza la cancelación de préstamos vigentes o en mora.',
        )
      }
      const body = await readJson<{ asOfDate?: string }>(request)
      return HttpResponse.json(quotePrepayment(loan, body?.asOfDate ?? today()))
    }),
  ),

  // Simulación de abono extraordinario a capital (FE-CP-17)
  http.post(
    '*/api/client/loans/:id/simulate-partial-prepayment',
    secured<{ id: string }>('client.portal', async ({ request, params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      if (!loan) return notFound('El préstamo')
      if (loan.status !== 'VIGENTE' && loan.status !== 'EN_MORA') {
        return problem(
          422,
          'Préstamo no amortizable',
          'Solo se realizan abonos a préstamos vigentes o en mora.',
        )
      }
      const body = await readJson<{ amount: string; option: 'REDUCE_TERM' | 'REDUCE_FEE' }>(request)
      if (!body?.amount || toCents(body.amount) <= 0) {
        return problem(400, 'Monto inválido', 'Ingresa un monto de abono mayor a cero.')
      }

      refreshLoan(loan)
      const currentPrincipal = outstandingPrincipal(loan)
      const prepAmount = toCents(body.amount)
      if (prepAmount >= currentPrincipal) {
        return problem(
          422,
          'Monto excede saldo',
          'El abono parcial no puede igualar o exceder el saldo insoluto; para cancelación total usa prepago total.',
        )
      }

      const unpaid = activeInstallments(loan).filter((i) => !isSettledOrCancelled(i))
      const currentTermRemaining = unpaid.length
      const currentInstallmentAmount = unpaid[0] ? unpaid[0].totalDue : 0
      const newPrincipal = currentPrincipal - prepAmount
      const interestBefore = sum(unpaid.map((i) => i.interest))

      let newTermRemaining = currentTermRemaining
      let newInstallmentAmount = currentInstallmentAmount
      let interestAfter = 0

      const i = periodRate(loan.annualRate, loan.frequency)

      if (body.option === 'REDUCE_TERM') {
        if (i > 0) {
          const ratio = (newPrincipal * i) / currentInstallmentAmount
          if (ratio < 1) {
            const calculatedTerm = Math.ceil(-Math.log(1 - ratio) / Math.log(1 + i))
            newTermRemaining = Math.max(1, Math.min(calculatedTerm, currentTermRemaining - 1))
          } else {
            newTermRemaining = 1
          }
        } else {
          newTermRemaining = Math.max(1, Math.ceil(newPrincipal / currentInstallmentAmount))
        }

        const simSchedule = buildSchedule({
          principal: newPrincipal,
          annualRate: loan.annualRate,
          term: newTermRemaining,
          frequency: loan.frequency,
          method: loan.amortizationMethod,
          startDate: today(),
          periodicFee: loan.periodicFee,
          holidays: holidaySet(),
        })
        newInstallmentAmount = simSchedule[0]?.totalDue ?? 0
        interestAfter = sum(simSchedule.map((s) => s.interest))
      } else {
        newTermRemaining = currentTermRemaining
        const simSchedule = buildSchedule({
          principal: newPrincipal,
          annualRate: loan.annualRate,
          term: newTermRemaining,
          frequency: loan.frequency,
          method: loan.amortizationMethod,
          startDate: today(),
          periodicFee: loan.periodicFee,
          holidays: holidaySet(),
        })
        newInstallmentAmount = simSchedule[0]?.totalDue ?? 0
        interestAfter = sum(simSchedule.map((s) => s.interest))
      }

      const interestSaved = Math.max(0, interestBefore - interestAfter)

      const simulation: PartialPrepaymentSimulation = {
        loanId: loan.id,
        currentOutstandingPrincipal: fromCents(currentPrincipal),
        prepaymentAmount: body.amount,
        newOutstandingPrincipal: fromCents(newPrincipal),
        option: body.option,
        currentTermRemaining,
        newTermRemaining,
        currentInstallmentAmount: fromCents(currentInstallmentAmount),
        newInstallmentAmount: fromCents(newInstallmentAmount),
        totalInterestSaved: fromCents(interestSaved),
      }

      return HttpResponse.json(simulation)
    }),
  ),

  // ── Cambio seguro de cuenta bancaria con OTP y período de espera (FE-CP-18 / ARQUITECTURA_GENERAL §9) ──
  http.post(
    '*/api/client/bank-accounts/change-primary',
    secured('client.portal', async ({ request, user }) => {
      const body = await readJson<ChangePrimaryBankAccountRequest>(request)
      if (!body?.otpCode || body.otpCode.length !== 6) {
        return problem(
          400,
          'OTP requerido',
          'Ingresa el código OTP de 6 dígitos enviado a tu teléfono.',
        )
      }

      const holdHours = db.parameters.bankAccountChangeHoldHours ?? 24
      const coolingOffUntil = new Date(Date.now() + holdHours * 3600 * 1000).toISOString()

      let primaryAccount: CustomerBankAccount

      if (body.bankAccountId) {
        const match = db.customerBankAccounts.find(
          (a) => a.id === body.bankAccountId && a.customerId === user.customerId,
        )
        if (!match) return notFound('La cuenta bancaria')
        primaryAccount = match
      } else if (body.newAccount) {
        const { bankName, accountType, cci, phoneWallet } = body.newAccount
        if (!bankName)
          return problem(400, 'Faltan datos', 'Indica el nombre de la entidad bancaria.')
        if (cci && !/^\d{20}$/.test(cci)) {
          return problem(400, 'CCI inválido', 'El CCI debe contener 20 dígitos numéricos.')
        }

        primaryAccount = {
          id: nextId('cba'),
          customerId: user.customerId!,
          bankName,
          accountType: accountType || (phoneWallet ? 'BILLETERA_DIGITAL' : 'CUENTA_AHORRO'),
          accountNumberMasked: phoneWallet
            ? `Billetera: ${phoneWallet}`
            : `**** ${(cci || '0000').slice(-4)}`,
          cci,
          phoneWallet,
          isVerified: true,
          isPrimary: true,
          status: 'ACTIVE',
        }
        db.customerBankAccounts.push(primaryAccount)
      } else {
        return problem(
          400,
          'Datos incompletos',
          'Indica la cuenta existente o los datos de la nueva cuenta.',
        )
      }

      // Marca las demás como no primarias
      for (const acc of db.customerBankAccounts) {
        if (acc.customerId === user.customerId) {
          acc.isPrimary = acc.id === primaryAccount.id
          if (acc.id === primaryAccount.id) {
            acc.coolingOffUntil = coolingOffUntil
          }
        }
      }

      recordAudit({
        entityName: 'customer_bank_accounts',
        entityId: primaryAccount.id,
        action: 'UPDATE',
        userName: user.fullName,
        after: { coolingOffUntil, holdHours },
      })

      return HttpResponse.json({
        account: primaryAccount,
        message: `Cuenta principal actualizada. Por seguridad, estará activa para desembolsos a partir de ${new Date(coolingOffUntil).toLocaleString('es-PE')}.`,
        coolingOffUntil,
      })
    }),
  ),

  // ── Solicitud de devolución del saldo a favor a cuenta verificada (FE-CP-19 / RF-17.3) ──
  http.get(
    '*/api/client/refunds',
    secured('client.portal', ({ user }) => {
      return HttpResponse.json(db.refundRequests.filter((r) => r.customerId === user.customerId))
    }),
  ),

  http.post(
    '*/api/client/refunds',
    secured('client.portal', async ({ request, user }) => {
      const body = await readJson<{ amount: string; bankAccountId: string }>(request)
      if (!body?.amount || !body?.bankAccountId) {
        return problem(400, 'Datos incompletos', 'Indica el monto y la cuenta de destino.')
      }
      const customer = db.customers.find((c) => c.id === user.customerId)
      if (!customer) return notFound('El cliente')

      const creditCents = toCents(customer.creditBalance)
      const requestCents = toCents(body.amount)

      if (requestCents <= 0) {
        return problem(400, 'Monto inválido', 'Ingresa un monto de devolución mayor a cero.')
      }
      if (requestCents > creditCents) {
        return problem(
          422,
          'Saldo insuficiente',
          `El monto solicitado supera tu saldo a favor disponible (S/ ${customer.creditBalance}).`,
        )
      }

      const bankAccount = db.customerBankAccounts.find(
        (a) => a.id === body.bankAccountId && a.customerId === user.customerId,
      )
      if (!bankAccount) return notFound('La cuenta bancaria')
      if (!bankAccount.isVerified) {
        return problem(
          422,
          'Cuenta no verificada',
          'Solo se transfieren devoluciones a cuentas bancarias verificadas.',
        )
      }

      customer.creditBalance = fromCents(creditCents - requestCents)

      const refund: RefundRequest = {
        id: nextId('ref'),
        customerId: customer.id,
        amount: body.amount,
        customerBankAccountId: bankAccount.id,
        status: 'PENDING_APPROVAL',
        requestedAt: nowIso(),
        bankName: bankAccount.bankName,
        accountNumberMasked: bankAccount.accountNumberMasked,
      }

      db.refundRequests.unshift(refund)
      recordAudit({
        entityName: 'refund_requests',
        entityId: refund.id,
        action: 'CREATE',
        userName: user.fullName,
        after: { amount: refund.amount },
      })

      return HttpResponse.json(refund, { status: 201 })
    }),
  ),

  // ── Centro de notificaciones y preferencias (FE-CP-20) ────────────────────────
  http.get(
    '*/api/client/notifications',
    secured('client.portal', ({ user }) => {
      const notifs = db.notifications
        .filter((n) => n.customerId === user.customerId)
        .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
      return HttpResponse.json(notifs)
    }),
  ),

  http.patch(
    '*/api/client/notifications/:id/read',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const notif = db.notifications.find(
        (n) => n.id === params.id && n.customerId === user.customerId,
      )
      if (!notif) return notFound('La notificación')
      notif.read = true
      return HttpResponse.json(notif)
    }),
  ),

  http.post(
    '*/api/client/notifications/mark-all-read',
    secured('client.portal', ({ user }) => {
      for (const notif of db.notifications) {
        if (notif.customerId === user.customerId) {
          notif.read = true
        }
      }
      return HttpResponse.json({ success: true })
    }),
  ),

  http.get(
    '*/api/client/notification-preferences',
    secured('client.portal', ({ user }) => {
      const prefs = db.notificationPreferences.get(user.customerId ?? '') || {
        emailEnabled: true,
        whatsappEnabled: true,
        smsEnabled: false,
        notifyDaysBeforeDue: 3,
      }
      return HttpResponse.json(prefs)
    }),
  ),

  http.put(
    '*/api/client/notification-preferences',
    secured('client.portal', async ({ request, user }) => {
      const body = await readJson<NotificationPreferences>(request)
      if (!body) return problem(400, 'Datos inválidos', 'Envía las preferencias de notificación.')
      db.notificationPreferences.set(user.customerId ?? '', body)
      return HttpResponse.json(body)
    }),
  ),

  // ── Libro de Reclamaciones Virtual (FE-CP-21 / Ley 29571) ─────────────────────
  http.post('*/api/complaints', async ({ request }) => {
    const body = await readJson<ComplaintClaimRequest>(request)
    if (
      !body?.consumerFullName?.trim() ||
      !body.docNumber?.trim() ||
      !body.email?.trim() ||
      !body.description?.trim()
    ) {
      return problem(
        400,
        'Datos obligatorios incompletos',
        'Completa tus datos personales y el detalle del reclamo o queja.',
      )
    }

    const year = new Date().getFullYear()
    const correlative = `LR-${year}-${String(db.complaints.length + 1).padStart(5, '0')}`

    const deadlineDate = new Date()
    deadlineDate.setDate(deadlineDate.getDate() + 21)

    const record: ComplaintClaimRecord = {
      id: nextId('clm'),
      ...body,
      correlativeNumber: correlative,
      status: 'REGISTRADO',
      createdAt: nowIso(),
      responseDeadline: deadlineDate.toISOString(),
    }

    db.complaints.unshift(record)
    return HttpResponse.json(record, { status: 201 })
  }),

  http.get('*/api/complaints/:correlative', ({ params }) => {
    const match = db.complaints.find(
      (c) => c.correlativeNumber === params.correlative || c.id === params.correlative,
    )
    return match ? HttpResponse.json(match) : notFound('La hoja de reclamación')
  }),
]
