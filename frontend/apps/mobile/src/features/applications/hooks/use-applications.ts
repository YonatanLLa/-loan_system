import { useState, useCallback, useEffect } from 'react';
import { processApiError } from '@/lib/toast';
import type {
  LoanProduct,
  LoanApplicationItem,
  SimulateLoanRequest,
  SimulationResult,
} from '../types';
import {
  getLoanProductsApi,
  simulateLoanApi,
  createApplicationApi,
  getApplicationsApi,
  getApplicationDetailApi,
} from '../api';

export function useApplications() {
  const [products, setProducts] = useState<LoanProduct[]>([]);
  const [applications, setApplications] = useState<LoanApplicationItem[]>([]);
  const [selectedApplication, setSelectedApplication] = useState<LoanApplicationItem | null>(null);
  const [simulation, setSimulation] = useState<SimulationResult | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const refresh = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const [appsData, prodsData] = await Promise.all([
        getApplicationsApi(),
        getLoanProductsApi().catch(() => [] as LoanProduct[]),
      ]);
      setApplications(appsData);
      setProducts(prodsData);
    } catch (err: unknown) {
      const { apiError } = processApiError(err);
      setError(apiError.detail);
    } finally {
      setIsLoading(false);
    }
  }, []);

  const simulate = useCallback(async (params: SimulateLoanRequest) => {
    setIsLoading(true);
    setError(null);
    try {
      const res = await simulateLoanApi(params);
      setSimulation(res);
      return res;
    } catch (err: unknown) {
      const { apiError } = processApiError(err);
      setError(apiError.detail);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const create = useCallback(
    async (payload: Parameters<typeof createApplicationApi>[0]) => {
      setIsLoading(true);
      setError(null);
      setFieldErrors({});
      try {
        const item = await createApplicationApi(payload);
        await refresh();
        return item;
      } catch (err: unknown) {
        const { apiError, fieldErrors: fields } = processApiError(err);
        setError(apiError.detail);
        setFieldErrors(fields);
        throw err;
      } finally {
        setIsLoading(false);
      }
    },
    [refresh]
  );

  const loadDetail = useCallback(async (id: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const detail = await getApplicationDetailApi(id);
      setSelectedApplication(detail);
      return detail;
    } catch (err: unknown) {
      const { apiError } = processApiError(err);
      setError(apiError.detail);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  return {
    products,
    applications,
    selectedApplication,
    simulation,
    isLoading,
    error,
    fieldErrors,
    simulate,
    create,
    loadDetail,
    refresh,
  };
}
