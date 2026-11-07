import type { Customer } from '@prestamos/api-client';

export type ProfileData = Customer;

export interface SetPrimaryAccountPayload {
  accountId: string;
  otpCode: string;
}

export interface RequestAccountChangeOtpResponse {
  coolingPeriodHours: number; // 24-48h anti SIM-swap
  effectiveAt: string;
}
