import { api } from '@/lib/api';
import type { CustomerBankAccount } from '@prestamos/api-client';
import type { KycDocumentPayload, ConsentsPayload, BankAccountPayload } from '../types';

export async function uploadKycDocumentApi(
  payload: KycDocumentPayload
): Promise<{ documentId: string }> {
  // FE-CP-03: Subida de imagen comprimida desde cámara nativa
  const formData = new FormData();
  formData.append('docType', payload.docType);
  formData.append('compressed', 'true');
  formData.append('file', {
    uri: payload.fileUri,
    name: `${payload.docType.toLowerCase()}.jpg`,
    type: 'image/jpeg',
  } as unknown as Blob);

  const { data } = await api.post<{ documentId: string }>('/api/client/documents', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return data;
}

export async function registerConsentsApi(payload: ConsentsPayload): Promise<void> {
  await api.post('/api/client/consents', payload);
}

export async function registerBankAccountApi(
  payload: BankAccountPayload
): Promise<CustomerBankAccount> {
  const { data } = await api.post<CustomerBankAccount>('/api/client/bank-accounts', payload);
  return data;
}

export async function verifyBankAccountApi(
  accountId: string,
  verificationCode?: string
): Promise<CustomerBankAccount> {
  const { data } = await api.put<CustomerBankAccount>(
    `/api/client/bank-accounts/${accountId}/verify`,
    { verificationCode }
  );
  return data;
}

export async function getBankAccountsApi(): Promise<CustomerBankAccount[]> {
  const { data } = await api.get<CustomerBankAccount[]>('/api/client/bank-accounts');
  return data;
}
