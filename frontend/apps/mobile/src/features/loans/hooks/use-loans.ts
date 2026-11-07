import { useState, useCallback, useEffect } from 'react';
import type { LoanSummary } from '../types';
import { getLoansApi } from '../api';

export function useLoans() {
  const [loans, setLoans] = useState<LoanSummary[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await getLoansApi();
      setLoans(data);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Error al obtener préstamos');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  return {
    loans,
    isLoading,
    error,
    refresh,
  };
}
