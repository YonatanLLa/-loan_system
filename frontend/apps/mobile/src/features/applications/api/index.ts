import { api } from '@/lib/api';
import type {
  LoanApplication as ApiLoanApplication,
  LoanProduct as ApiLoanProduct,
} from '@prestamos/api-client';
import type {
  LoanProduct,
  SimulateLoanRequest,
  SimulationResult,
  CreateApplicationPayload,
  LoanApplicationItem,
} from '../types';

function mapApplication(raw: ApiLoanApplication & Partial<LoanApplicationItem>): LoanApplicationItem {
  return {
    id: raw.id,
    applicationNumber: raw.applicationNumber ?? raw.id.toUpperCase(),
    status: raw.status,
    requestedAmount: raw.requestedAmount,
    approvedAmount: raw.approvedAmount,
    termMonths: raw.termMonths ?? raw.term,
    createdAt: raw.createdAt,
    updatedAt: raw.updatedAt ?? raw.decidedAt ?? raw.createdAt,
  };
}

export async function getLoanProductsApi(): Promise<LoanProduct[]> {
  const { data } = await api.get<(ApiLoanProduct & Partial<LoanProduct>)[]>('/api/loan-products', {
    params: { status: 'ACTIVO' },
  });
  return data.map((p) => ({
    id: p.id,
    name: p.name,
    minAmount: p.minAmount,
    maxAmount: p.maxAmount,
    minTermMonths: p.minTermMonths ?? p.minTerm,
    maxTermMonths: p.maxTermMonths ?? p.maxTerm,
    tea: p.tea ?? `${(Number(p.annualRate) * 100).toFixed(2)}%`,
  }));
}

export async function simulateLoanApi(payload: SimulateLoanRequest): Promise<SimulationResult> {
  const { data } = await api.post<SimulationResult>('/api/client/loan-applications/simulate', {
    productId: payload.productId ?? 'prod-001',
    amount: payload.amount,
    term: payload.termMonths,
    termMonths: payload.termMonths,
  });
  return data;
}

export async function createApplicationApi(
  payload: CreateApplicationPayload
): Promise<LoanApplicationItem> {
  const { data } = await api.post<ApiLoanApplication & Partial<LoanApplicationItem>>(
    '/api/client/loan-applications',
    {
      ...payload,
      amount: payload.requestedAmount,
      term: payload.termMonths,
      frequency: 'MENSUAL',
      destination: 'CONSUMO',
    }
  );
  return mapApplication(data);
}

export async function getApplicationsApi(): Promise<LoanApplicationItem[]> {
  const { data } = await api.get<(ApiLoanApplication & Partial<LoanApplicationItem>)[]>(
    '/api/client/loan-applications'
  );
  return data.map(mapApplication);
}

export async function getApplicationDetailApi(id: string): Promise<LoanApplicationItem> {
  const { data } = await api.get<ApiLoanApplication & Partial<LoanApplicationItem>>(
    `/api/client/loan-applications/${id}`
  );
  return mapApplication(data);
}
