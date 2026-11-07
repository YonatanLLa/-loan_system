import { useState, useCallback, useEffect, useRef } from 'react';
import { createIdempotentAction } from '@prestamos/api-client';
import type { Customer, CustomerBankAccount } from '@prestamos/api-client';
import { processApiError, useToastStore } from '@/lib/toast';
import {
  getProfileApi,
  getProfileBankAccountsApi,
  requestPrimaryAccountChangeOtpApi,
  setPrimaryBankAccountApi,
  requestCreditBalanceRefundApi,
} from '../api';

export function useProfile() {
  const [profile, setProfile] = useState<Customer | null>(null);
  const [bankAccounts, setBankAccounts] = useState<CustomerBankAccount[]>([]);
  const [coolingPeriodNotice, setCoolingPeriodNotice] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const idempotentPrimaryRef = useRef(
    createIdempotentAction((key: string, accountId: string, otpCode: string) =>
      setPrimaryBankAccountApi({ accountId, otpCode }, key)
    )
  );

  const idempotentRefundRef = useRef(
    createIdempotentAction((key: string, bankAccountId: string) =>
      requestCreditBalanceRefundApi(bankAccountId, key)
    )
  );

  const refresh = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const [profileData, accountsData] = await Promise.all([
        getProfileApi(),
        getProfileBankAccountsApi(),
      ]);
      setProfile(profileData);
      setBankAccounts(accountsData);
    } catch (err: unknown) {
      const { apiError } = processApiError(err);
      setError(apiError.detail);
    } finally {
      setIsLoading(false);
    }
  }, []);

  const requestAccountChangeOtp = useCallback(async (accountId: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const res = await requestPrimaryAccountChangeOtpApi(accountId);
      setCoolingPeriodNotice(
        `Por seguridad anti SIM-swap, el cambio tendrá un periodo de espera de ${res.coolingPeriodHours}h (vigente desde ${res.effectiveAt}). Mientras tanto, cualquier desembolso irá a la cuenta anterior.`
      );
      return res;
    } catch (err: unknown) {
      const { apiError } = processApiError(err);
      setError(apiError.detail);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const setPrimaryAccount = useCallback(
    async (accountId: string, otpCode: string) => {
      setIsLoading(true);
      setError(null);
      try {
        const updated = await idempotentPrimaryRef.current.execute(accountId, otpCode);
        await refresh();
        useToastStore.getState().showToast({
          title: 'Cambio de cuenta registrado',
          detail: 'En espera de 24–48 h por seguridad anti SIM-swap (FE-CP-18).',
          tone: 'success',
        });
        return updated;
      } catch (err: unknown) {
        const { apiError } = processApiError(err);
        setError(apiError.detail);
        throw err;
      } finally {
        setIsLoading(false);
      }
    },
    [refresh]
  );

  const requestCreditBalanceRefund = useCallback(
    async (bankAccountId: string) => {
      setIsLoading(true);
      setError(null);
      try {
        const res = await idempotentRefundRef.current.execute(bankAccountId);
        await refresh();
        useToastStore.getState().showToast({
          title: 'Devolución solicitada',
          detail: 'Tu saldo a favor será transferido a tu cuenta verificada (FE-CP-19).',
          tone: 'success',
        });
        return res;
      } catch (err: unknown) {
        const { apiError } = processApiError(err);
        setError(apiError.detail);
        throw err;
      } finally {
        setIsLoading(false);
      }
    },
    [refresh]
  );

  useEffect(() => {
    void refresh();
  }, [refresh]);

  return {
    profile,
    bankAccounts,
    coolingPeriodNotice,
    isLoading,
    error,
    refresh,
    requestAccountChangeOtp,
    setPrimaryAccount,
    requestCreditBalanceRefund,
  };
}
