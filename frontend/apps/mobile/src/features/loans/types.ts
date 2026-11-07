import type {
  InstallmentStatus,
  LoanStatus,
  PaymentStatus,
  PaymentMethodType,
  Money,
  BusinessDate,
} from '@prestamos/api-client';

export interface LoanSummary {
  id: string;
  loanNumber: string;
  status: LoanStatus;
  productName: string;
  principalAmount: Money;
  outstandingBalance: Money;
  lateFeeBalance?: Money;
  currency: string;
  nextDueDate?: BusinessDate;
  nextDueAmount?: Money;
  daysRemaining?: number;
  daysLate?: number;
  installmentsCount: number;
  paidInstallmentsCount: number;
}

export interface LoanInstallmentItem {
  id: string;
  installmentNumber: number;
  dueDate: BusinessDate;
  amount: Money;
  principal: Money;
  interest: Money;
  lateFee: Money;
  paidAmount: Money;
  status: InstallmentStatus;
}

export interface LoanPaymentHistoryItem {
  id: string;
  amount: Money;
  status: PaymentStatus;
  paymentMethod: PaymentMethodType;
  paidAt: string;
  receiptUrl?: string;
}

export type PrepaymentReductionOption = 'REDUCE_TERM' | 'REDUCE_INSTALLMENT';

export interface PrepaymentQuote {
  loanId: string;
  type: 'TOTAL' | 'PARTIAL';
  reductionOption?: PrepaymentReductionOption;
  outstandingPrincipal: Money;
  interestToDate: Money;
  penaltyFee: Money;
  totalPayable: Money;
  expiresAt: string;
}
