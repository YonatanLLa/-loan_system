import { api } from '@/lib/api';
import {
  newIdempotencyKey,
  IDEMPOTENCY_HEADER,
  type Payment,
  type PaymentIntent,
} from '@prestamos/api-client';
import type { PaymentPayload, PaymentResult } from '../types';

const BASE_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5000';

export function getPaymentReceiptPdfUrl(paymentId: string): string {
  return `${BASE_URL}/api/client/payments/${paymentId}/receipt.pdf`;
}

function mapPaymentResult(
  raw: Partial<PaymentIntent> & Partial<Payment> & Partial<PaymentResult>
): PaymentResult {
  const id = raw.id ?? raw.paymentId ?? 'pay-001';
  return {
    id,
    loanId: raw.loanId ?? '',
    amount: raw.amount ?? '0.00',
    status: raw.status ?? 'PENDING_CONFIRMATION',
    paymentMethod: raw.paymentMethod ?? raw.method ?? 'YAPE',
    qrPayload: raw.qrPayload,
    paymentUrl: raw.paymentUrl ?? raw.checkoutUrl,
    receiptUrl: raw.status === 'APPLIED' ? getPaymentReceiptPdfUrl(id) : raw.receiptUrl,
    createdAt: raw.createdAt ?? raw.paidAt ?? new Date().toISOString(),
  };
}

export async function submitPaymentApi(
  payload: PaymentPayload,
  idempotencyKey?: string
): Promise<PaymentResult> {
  const key = idempotencyKey ?? newIdempotencyKey();

  const { data } = await api.post<PaymentIntent & Partial<PaymentResult>>(
    '/api/client/payments',
    {
      loanId: payload.loanId,
      installmentId: payload.installmentId,
      amount: payload.amount,
      method: payload.paymentMethod,
      paymentMethod: payload.paymentMethod,
      voucherFileName: payload.operationNumber ?? (payload.voucherFileUri ? 'voucher.jpg' : undefined),
    },
    {
      headers: {
        [IDEMPOTENCY_HEADER]: key,
      },
    }
  );
  return mapPaymentResult(data);
}

export async function getPaymentStatusApi(paymentId: string): Promise<PaymentResult> {
  const { data } = await api.get<Payment & Partial<PaymentResult>>(
    `/api/client/payments/${paymentId}`
  );
  return mapPaymentResult(data);
}
