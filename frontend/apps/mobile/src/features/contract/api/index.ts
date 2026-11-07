import { api } from '@/lib/api';
import type { ContractDocument, SignContractPayload } from '../types';

const BASE_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5000';

export function getContractPdfUrl(loanId: string): string {
  return `${BASE_URL}/api/client/loans/${loanId}/documents/contract.pdf`;
}

export async function getContractApi(loanId: string): Promise<ContractDocument> {
  const { data } = await api.get<ContractDocument>(`/api/client/loans/${loanId}/contract`);
  return data;
}

export async function signContractApi(
  payload: SignContractPayload
): Promise<{ success: boolean; signedAt: string }> {
  const { data } = await api.post<{ success: boolean; signedAt: string }>(
    `/api/client/loans/${payload.loanId}/sign`,
    { otpCode: payload.otpCode }
  );
  return data;
}

export async function requestContractOtpApi(loanId: string): Promise<void> {
  await api.post(`/api/client/loans/${loanId}/request-sign-otp`);
}
