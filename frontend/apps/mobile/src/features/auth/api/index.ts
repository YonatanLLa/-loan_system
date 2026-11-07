import { api, setAccessToken } from '@/lib/api';
import type { Customer, TokenResponse } from '@prestamos/api-client';
import type { LoginCredentials, RegisterPayload, VerifyOtpPayload, AuthSessionData } from '../types';

export async function loginApi(credentials: LoginCredentials): Promise<AuthSessionData> {
  const { data: tokenData } = await api.post<TokenResponse & Partial<AuthSessionData>>(
    '/api/client/login',
    {
      email: credentials.identifier,
      identifier: credentials.identifier,
      password: credentials.password ?? 'Demo1234!',
    },
    { skipAuthRefresh: true }
  );

  if (tokenData.user) {
    return tokenData as AuthSessionData;
  }

  setAccessToken(tokenData.accessToken);
  const { data: customer } = await api.get<Customer>('/api/client/profile');

  return {
    accessToken: tokenData.accessToken,
    refreshToken: tokenData.accessToken,
    user: {
      id: customer.id,
      customerId: customer.id,
      fullName: customer.fullName,
      email: customer.email,
      docType: customer.docType === 'RUC' ? 'DNI' : customer.docType,
      docNumber: customer.docNumber,
      phone: customer.phone,
      kycStatus: customer.kycStatus,
    },
  };
}

export async function registerApi(payload: RegisterPayload): Promise<{ challengeId: string }> {
  const { data } = await api.post<{ challengeId?: string; customerId?: string }>(
    '/api/client/register',
    {
      ...payload,
      password: payload.password ?? 'Demo1234!',
    },
    { skipAuthRefresh: true }
  );
  return { challengeId: data.challengeId ?? data.customerId ?? 'otp-challenge' };
}

export async function verifyOtpApi(payload: VerifyOtpPayload): Promise<AuthSessionData> {
  const { data } = await api.post<AuthSessionData>('/api/client/verify-otp', payload, {
    skipAuthRefresh: true,
  });
  return data;
}

export async function logoutApi(): Promise<void> {
  await api.post('/api/client/logout');
}
