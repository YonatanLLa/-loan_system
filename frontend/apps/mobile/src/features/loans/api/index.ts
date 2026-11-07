import { api } from '@/lib/api';
import type { Installment, Loan, Payment, Schedule } from '@prestamos/api-client';
import type {
  LoanSummary,
  LoanInstallmentItem,
  LoanPaymentHistoryItem,
  PrepaymentQuote,
  PrepaymentReductionOption,
} from '../types';

const BASE_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5000';

export function getLoanStatementPdfUrl(id: string): string {
  return `${BASE_URL}/api/client/loans/${id}/statement.pdf`;
}

function mapInstallment(loanId: string, inst: Installment & Partial<LoanInstallmentItem>): LoanInstallmentItem {
  return {
    id: inst.id ?? `${loanId}-${inst.number ?? inst.installmentNumber}`,
    installmentNumber: inst.installmentNumber ?? inst.number,
    dueDate: inst.dueDate,
    amount: inst.amount ?? inst.totalDue,
    principal: inst.principal,
    interest: inst.interest,
    lateFee: inst.lateFee ?? '0.00',
    paidAmount: inst.paidAmount ?? inst.paidPrincipal ?? '0.00',
    status: inst.status,
  };
}

function mapLoan(raw: Loan & Partial<LoanSummary>, installments?: LoanInstallmentItem[]): LoanSummary {
  const nextPending = installments?.find((i) => i.status !== 'PAID' && i.status !== 'CANCELLED_BY_PREPAYMENT');
  const paidCount = installments
    ? installments.filter((i) => i.status === 'PAID').length
    : (raw.paidInstallmentsCount ?? 0);

  return {
    id: raw.id,
    loanNumber: raw.loanNumber ?? raw.id.toUpperCase(),
    status: raw.status,
    productName: raw.productName,
    principalAmount: raw.principalAmount ?? raw.principal,
    outstandingBalance: raw.outstandingBalance ?? raw.outstandingPrincipal,
    lateFeeBalance: raw.lateFeeBalance,
    currency: raw.currency ?? 'PEN',
    nextDueDate: raw.nextDueDate ?? nextPending?.dueDate ?? raw.firstDueDate,
    nextDueAmount: raw.nextDueAmount ?? nextPending?.amount,
    daysRemaining: raw.daysRemaining,
    daysLate: raw.daysLate,
    installmentsCount: raw.installmentsCount ?? raw.term ?? installments?.length ?? 0,
    paidInstallmentsCount: paidCount,
  };
}

export async function getLoansApi(): Promise<LoanSummary[]> {
  const { data } = await api.get<(Loan & Partial<LoanSummary>)[]>('/api/client/loans');
  const enriched = await Promise.all(
    data.map(async (loan) => {
      try {
        const schedule = await getLoanScheduleApi(loan.id);
        return mapLoan(loan, schedule);
      } catch {
        return mapLoan(loan);
      }
    })
  );
  return enriched;
}

export async function getLoanDetailApi(id: string): Promise<LoanSummary> {
  const [{ data }, schedule] = await Promise.all([
    api.get<Loan & Partial<LoanSummary>>(`/api/client/loans/${id}`),
    getLoanScheduleApi(id).catch(() => undefined),
  ]);
  return mapLoan(data, schedule);
}

export async function getLoanScheduleApi(id: string): Promise<LoanInstallmentItem[]> {
  const { data } = await api.get<Schedule | (Installment & Partial<LoanInstallmentItem>)[]>(
    `/api/client/loans/${id}/schedule`
  );
  const rawList = Array.isArray(data) ? data : data.installments;
  return rawList.map((inst) => mapInstallment(id, inst));
}

export async function getLoanPaymentsHistoryApi(id: string): Promise<LoanPaymentHistoryItem[]> {
  const { data } = await api.get<(Payment & Partial<LoanPaymentHistoryItem>)[]>(
    `/api/client/loans/${id}/payments`
  );
  return data.map((p) => ({
    id: p.id,
    amount: p.amount,
    status: p.status,
    paymentMethod: p.paymentMethod ?? p.method,
    paidAt: p.paidAt,
    receiptUrl: p.status === 'APPLIED' ? `${BASE_URL}/api/client/payments/${p.id}/receipt.pdf` : undefined,
  }));
}

export async function quotePrepaymentApi(
  loanId: string,
  type: 'TOTAL' | 'PARTIAL',
  amount?: string,
  reductionOption?: PrepaymentReductionOption
): Promise<PrepaymentQuote> {
  const { data } = await api.post<PrepaymentQuote>(
    `/api/client/loans/${loanId}/prepayment/quote`,
    { type, amount, reductionOption }
  );
  return data;
}
