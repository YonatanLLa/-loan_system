import { useState, useCallback, useEffect } from 'react';
import { processApiError } from '@/lib/toast';
import type {
  LoanSummary,
  LoanInstallmentItem,
  LoanPaymentHistoryItem,
  PrepaymentQuote,
  PrepaymentReductionOption,
} from '../types';
import {
  getLoanDetailApi,
  getLoanScheduleApi,
  getLoanPaymentsHistoryApi,
  quotePrepaymentApi,
} from '../api';

export function useLoanDetail(id: string) {
  const [loan, setLoan] = useState<LoanSummary | null>(null);
  const [schedule, setSchedule] = useState<LoanInstallmentItem[]>([]);
  const [paymentsHistory, setPaymentsHistory] = useState<LoanPaymentHistoryItem[]>([]);
  const [prepaymentQuote, setPrepaymentQuote] = useState<PrepaymentQuote | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    if (!id) return;
    setIsLoading(true);
    setError(null);
    try {
      const [loanData, scheduleData, historyData] = await Promise.all([
        getLoanDetailApi(id),
        getLoanScheduleApi(id),
        getLoanPaymentsHistoryApi(id).catch(() => [] as LoanPaymentHistoryItem[]),
      ]);
      setLoan(loanData);
      setSchedule(scheduleData);
      setPaymentsHistory(historyData);
    } catch (err: unknown) {
      const { apiError } = processApiError(err);
      setError(apiError.detail);
    } finally {
      setIsLoading(false);
    }
  }, [id]);

  const quotePrepayment = useCallback(
    async (
      type: 'TOTAL' | 'PARTIAL',
      amount?: string,
      reductionOption?: PrepaymentReductionOption
    ) => {
      if (!id) return null;
      setIsLoading(true);
      setError(null);
      try {
        const quote = await quotePrepaymentApi(id, type, amount, reductionOption);
        setPrepaymentQuote(quote);
        return quote;
      } catch (err: unknown) {
        const { apiError } = processApiError(err);
        setError(apiError.detail);
        throw err;
      } finally {
        setIsLoading(false);
      }
    },
    [id]
  );

  useEffect(() => {
    void refresh();
  }, [refresh]);

  return {
    loan,
    schedule,
    paymentsHistory,
    prepaymentQuote,
    isLoading,
    error,
    refresh,
    quotePrepayment,
  };
}
