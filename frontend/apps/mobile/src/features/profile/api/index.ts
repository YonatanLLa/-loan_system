import { api } from '@/lib/api';
import { IDEMPOTENCY_HEADER, newIdempotencyKey } from '@prestamos/api-client';
import type { Customer, CustomerBankAccount } from '@prestamos/api-client';
import type { SetPrimaryAccountPayload, RequestAccountChangeOtpResponse } from '../types';

export async function getProfileApi(): Promise<Customer> {
  const { data } = await api.get<Customer>('/api/client/profile');
  return data;
}

export async function getProfileBankAccountsApi(): Promise<CustomerBankAccount[]> {
  const { data } = await api.get<CustomerBankAccount[]>('/api/client/bank-accounts');
  return data;
}

export async function requestPrimaryAccountChangeOtpApi(
  accountId: string
): Promise<RequestAccountChangeOtpResponse> {
  const { data } = await api.post<RequestAccountChangeOtpResponse>(
    `/api/client/bank-accounts/${accountId}/request-change-otp`
  );
  return data;
}

export async function setPrimaryBankAccountApi(
  payload: SetPrimaryAccountPayload,
  idempotencyKey?: string
): Promise<CustomerBankAccount> {
  const { data } = await api.put<CustomerBankAccount>(
    `/api/client/bank-accounts/${payload.accountId}/set-primary`,
    { otpCode: payload.otpCode },
    {
      headers: {
        [IDEMPOTENCY_HEADER]: idempotencyKey ?? newIdempotencyKey(),
      },
    }
  );
  return data;
}

export async function requestCreditBalanceRefundApi(
  bankAccountId: string,
  idempotencyKey?: string
): Promise<{ refundId: string; status: string }> {
  const { data } = await api.post<{ refundId: string; status: string }>(
    '/api/client/profile/refund-credit-balance',
    { bankAccountId },
    {
      headers: {
        [IDEMPOTENCY_HEADER]: idempotencyKey ?? newIdempotencyKey(),
      },
    }
  );
  return data;
}
