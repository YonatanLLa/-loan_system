import type { CustomerBankAccount, KycStatus } from '@prestamos/api-client';

export interface KycDocumentPayload {
  docType: 'DNI_FRONT' | 'DNI_BACK' | 'SELFIE';
  fileUri: string;
}

export interface ConsentsPayload {
  privacyAccepted: boolean;
  creditBureauAccepted: boolean;
  marketingAccepted: boolean;
}

export interface BankAccountPayload {
  bankName: string;
  accountType: 'CUENTA_CORRIENTE' | 'CUENTA_AHORRO' | 'BILLETERA_DIGITAL';
  cci?: string;
  phoneWallet?: string;
}

export interface OnboardingState {
  kycStatus: KycStatus;
  consentsAccepted: boolean;
  bankAccounts: CustomerBankAccount[];
}
