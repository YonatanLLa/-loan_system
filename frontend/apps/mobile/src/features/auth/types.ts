import type { KycStatus } from '@prestamos/api-client';

export interface LoginCredentials {
  identifier: string; // correo o celular
  password?: string;
}

export interface RegisterPayload {
  docType: 'DNI' | 'CE';
  docNumber: string;
  fullName: string;
  email: string;
  phone: string;
  password?: string;
}

export interface VerifyOtpPayload {
  identifier: string;
  code: string;
  purpose: 'LOGIN' | 'REGISTER' | 'CONTRACT' | 'BANK_ACCOUNT';
}

export interface AuthSessionData {
  accessToken: string;
  refreshToken?: string;
  user: {
    id: string;
    customerId: string;
    fullName: string;
    email: string;
    docType?: string;
    docNumber?: string;
    phone?: string;
    kycStatus?: KycStatus;
  };
}
