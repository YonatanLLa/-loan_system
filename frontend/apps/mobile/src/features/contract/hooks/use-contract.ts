import { useState, useCallback, useEffect } from 'react';
import type { ContractDocument } from '../types';
import { getContractApi, signContractApi, requestContractOtpApi } from '../api';

export function useContract(loanId: string) {
  const [contract, setContract] = useState<ContractDocument | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    if (!loanId) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await getContractApi(loanId);
      setContract(data);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Error al cargar contrato');
    } finally {
      setIsLoading(false);
    }
  }, [loanId]);

  const requestOtp = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      await requestContractOtpApi(loanId);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Error al solicitar código OTP');
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, [loanId]);

  const sign = useCallback(
    async (otpCode: string) => {
      setIsLoading(true);
      setError(null);
      try {
        const res = await signContractApi({ loanId, otpCode });
        await refresh();
        return res;
      } catch (err: unknown) {
        setError(err instanceof Error ? err.message : 'Error al firmar contrato');
        throw err;
      } finally {
        setIsLoading(false);
      }
    },
    [loanId, refresh]
  );

  useEffect(() => {
    void refresh();
  }, [refresh]);

  return {
    contract,
    isLoading,
    error,
    refresh,
    requestOtp,
    sign,
  };
}
