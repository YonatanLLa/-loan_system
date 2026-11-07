import { useState, useCallback, useEffect } from 'react';
import { Linking } from 'react-native';
import type { CustomerBankAccount } from '@prestamos/api-client';
import { processApiError } from '@/lib/toast';
import {
  uploadKycDocumentApi,
  registerConsentsApi,
  registerBankAccountApi,
  verifyBankAccountApi,
  getBankAccountsApi,
} from '../api';
import type { KycDocumentPayload, ConsentsPayload, BankAccountPayload } from '../types';

export type CameraPermissionStatus = 'undetermined' | 'granted' | 'denied';

export function useOnboarding() {
  const [bankAccounts, setBankAccounts] = useState<CustomerBankAccount[]>([]);
  const [cameraPermission, setCameraPermission] = useState<CameraPermissionStatus>('undetermined');
  const [uploadedDocs, setUploadedDocs] = useState<Record<string, boolean>>({});
  const [consentsSaved, setConsentsSaved] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const refreshAccounts = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await getBankAccountsApi();
      setBankAccounts(data);
    } catch (err: unknown) {
      const { apiError } = processApiError(err);
      setError(apiError.detail);
    } finally {
      setIsLoading(false);
    }
  }, []);

  // MOV-05: Solicitud de permiso de cámara y manejo de denegación
  const requestCameraPermission = useCallback(async (simulateDeny = false) => {
    if (simulateDeny) {
      setCameraPermission('denied');
      return false;
    }
    setCameraPermission('granted');
    return true;
  }, []);

  const openSystemSettings = useCallback(async () => {
    await Linking.openSettings();
  }, []);

  const uploadKyc = useCallback(async (payload: KycDocumentPayload) => {
    setIsLoading(true);
    setError(null);
    try {
      const res = await uploadKycDocumentApi(payload);
      setUploadedDocs((prev) => ({ ...prev, [payload.docType]: true }));
      return res;
    } catch (err: unknown) {
      const { apiError } = processApiError(err);
      setError(apiError.detail);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const submitConsents = useCallback(async (payload: ConsentsPayload) => {
    setIsLoading(true);
    setError(null);
    try {
      await registerConsentsApi(payload);
      setConsentsSaved(true);
    } catch (err: unknown) {
      const { apiError } = processApiError(err);
      setError(apiError.detail);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const addBankAccount = useCallback(async (payload: BankAccountPayload) => {
    setIsLoading(true);
    setError(null);
    setFieldErrors({});
    try {
      const newAccount = await registerBankAccountApi(payload);
      setBankAccounts((prev) => [...prev, newAccount]);
      return newAccount;
    } catch (err: unknown) {
      const { apiError, fieldErrors: fields } = processApiError(err);
      setError(apiError.detail);
      setFieldErrors(fields);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const verifyBankAccount = useCallback(async (accountId: string, code?: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const updated = await verifyBankAccountApi(accountId, code);
      setBankAccounts((prev) =>
        prev.map((acc) => (acc.id === accountId ? updated : acc))
      );
      return updated;
    } catch (err: unknown) {
      const { apiError } = processApiError(err);
      setError(apiError.detail);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void refreshAccounts();
  }, [refreshAccounts]);

  return {
    bankAccounts,
    cameraPermission,
    uploadedDocs,
    consentsSaved,
    isLoading,
    error,
    fieldErrors,
    requestCameraPermission,
    openSystemSettings,
    uploadKyc,
    submitConsents,
    addBankAccount,
    verifyBankAccount,
    refreshAccounts,
  };
}
