import { useState, useCallback, useEffect } from 'react';
import { processApiError } from '@/lib/toast';
import type { CreateComplaintPayload, ComplaintResult } from '../types';
import { submitComplaintApi, getComplaintsApi } from '../api';

export function useComplaints() {
  const [complaints, setComplaints] = useState<ComplaintResult[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await getComplaintsApi();
      setComplaints(data);
    } catch (err: unknown) {
      const { apiError } = processApiError(err);
      setError(apiError.detail);
    } finally {
      setIsLoading(false);
    }
  }, []);

  const submit = useCallback(async (payload: CreateComplaintPayload) => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await submitComplaintApi(payload);
      setComplaints((prev) => [result, ...prev]);
      return result;
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
    complaints,
    isLoading,
    error,
    refresh,
    submit,
  };
}
