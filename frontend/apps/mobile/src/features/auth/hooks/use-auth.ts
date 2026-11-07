import { useState, useCallback } from 'react';
import { useSessionStore } from '@/lib/session';
import { processApiError } from '@/lib/toast';
import { loginApi, registerApi, verifyOtpApi } from '../api';
import type { LoginCredentials, RegisterPayload, VerifyOtpPayload } from '../types';

export function useAuth() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const setSession = useSessionStore((state) => state.setSession);
  const logoutStore = useSessionStore((state) => state.logout);

  const login = useCallback(
    async (credentials: LoginCredentials) => {
      setIsLoading(true);
      setError(null);
      setFieldErrors({});
      try {
        const response = await loginApi(credentials);
        await setSession(
          {
            id: response.user.id,
            customerId: response.user.customerId,
            fullName: response.user.fullName,
            email: response.user.email,
            kycStatus: response.user.kycStatus,
          },
          response.accessToken,
          response.refreshToken
        );
        return response;
      } catch (err: unknown) {
        const { apiError, fieldErrors: fields } = processApiError(err);
        setError(apiError.detail);
        setFieldErrors(fields);
        throw err;
      } finally {
        setIsLoading(false);
      }
    },
    [setSession]
  );

  const register = useCallback(async (payload: RegisterPayload) => {
    setIsLoading(true);
    setError(null);
    setFieldErrors({});
    try {
      return await registerApi(payload);
    } catch (err: unknown) {
      const { apiError, fieldErrors: fields } = processApiError(err);
      setError(apiError.detail);
      setFieldErrors(fields);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const verifyOtp = useCallback(
    async (payload: VerifyOtpPayload) => {
      setIsLoading(true);
      setError(null);
      setFieldErrors({});
      try {
        const response = await verifyOtpApi(payload);
        await setSession(
          {
            id: response.user.id,
            customerId: response.user.customerId,
            fullName: response.user.fullName,
            email: response.user.email,
            kycStatus: response.user.kycStatus,
          },
          response.accessToken,
          response.refreshToken
        );
        return response;
      } catch (err: unknown) {
        const { apiError, fieldErrors: fields } = processApiError(err);
        setError(apiError.detail);
        setFieldErrors(fields);
        throw err;
      } finally {
        setIsLoading(false);
      }
    },
    [setSession]
  );

  return {
    login,
    register,
    verifyOtp,
    logout: logoutStore,
    isLoading,
    error,
    fieldErrors,
  };
}
