import { useState, useCallback, useRef } from 'react';
import { createIdempotentAction } from '@prestamos/api-client';
import { processApiError } from '@/lib/toast';
import type { PaymentPayload, PaymentResult } from '../types';
import { submitPaymentApi, getPaymentStatusApi } from '../api';

export function usePayments() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastPayment, setLastPayment] = useState<PaymentResult | null>(null);
  const [pendingIdempotencyKey, setPendingIdempotencyKey] = useState<string | null>(null);

  // FE-NF-04 / MOV-06: Acción idempotente que conserva la misma Idempotency-Key en reintentos
  const idempotentPaymentRef = useRef(
    createIdempotentAction((key: string, payload: PaymentPayload) =>
      submitPaymentApi(payload, key)
    )
  );

  const makePayment = useCallback(async (payload: PaymentPayload) => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await idempotentPaymentRef.current.execute(payload);
      setPendingIdempotencyKey(null);
      setLastPayment(result);
      return result;
    } catch (err: unknown) {
      setPendingIdempotencyKey(idempotentPaymentRef.current.pendingKey);
      const { apiError } = processApiError(err);
      setError(apiError.detail);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const resetIdempotency = useCallback(() => {
    idempotentPaymentRef.current.reset();
    setPendingIdempotencyKey(null);
  }, []);

  const checkStatus = useCallback(async (paymentId: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await getPaymentStatusApi(paymentId);
      setLastPayment(result);
      return result;
    } catch (err: unknown) {
      const { apiError } = processApiError(err);
      setError(apiError.detail);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  return {
    isLoading,
    error,
    lastPayment,
    pendingIdempotencyKey,
    makePayment,
    resetIdempotency,
    checkStatus,
  };
}
