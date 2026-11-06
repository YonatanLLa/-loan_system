import { http, HttpResponse } from 'msw'

import { db } from '../db'
import { toInstallmentDto, toLoanDto, activeInstallments } from '../domain'
import { notFound, secured } from '../http-utils'
import { statement } from './collections-reports'

/** Préstamos del cliente autenticado; un préstamo ajeno responde 404 (RF-26.5). */
const ownLoan = (loanId: string, customerId?: string) =>
  db.loans.find((l) => l.id === loanId && l.customerId === customerId)

export const clientHandlers = [
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
    '*/api/client/loans/:id/statement',
    secured<{ id: string }>('client.portal', ({ params, user }) => {
      const loan = ownLoan(params.id, user.customerId)
      return loan ? HttpResponse.json(statement(loan.id)) : notFound('El préstamo')
    }),
  ),

  http.get(
    '*/api/client/bank-accounts',
    secured('client.portal', ({ user }) =>
      HttpResponse.json(db.customerBankAccounts.filter((a) => a.customerId === user.customerId)),
    ),
  ),
]
