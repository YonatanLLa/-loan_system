import { authHandlers } from './auth'
import { catalogHandlers } from './catalog'
import { clientHandlers } from './client'
import { collectionsReportHandlers } from './collections-reports'
import { customerHandlers } from './customers'
import { loanHandlers } from './loans'
import { originationHandlers } from './origination'
import { paymentHandlers } from './payments'
import { treasuryHandlers } from './treasury'

export const handlers = [
  ...authHandlers,
  ...customerHandlers,
  ...catalogHandlers,
  ...originationHandlers,
  ...loanHandlers,
  ...paymentHandlers,
  ...treasuryHandlers,
  ...collectionsReportHandlers,
  ...clientHandlers,
]
