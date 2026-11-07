import type { PaymentMethodType, PaymentStatus, Money } from '@prestamos/api-client';

export interface PaymentPayload {
  loanId: string;
  installmentId?: string;
  amount: Money;
  paymentMethod: PaymentMethodType;
  voucherFileUri?: string;
  operationNumber?: string;
}

export interface PaymentResult {
  id: string;
  loanId: string;
  amount: Money;
  status: PaymentStatus;
  paymentMethod: PaymentMethodType;
  qrPayload?: string;
  paymentUrl?: string;
  receiptUrl?: string;
  createdAt: string;
}
