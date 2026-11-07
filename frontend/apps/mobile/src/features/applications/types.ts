import type { ApplicationStatus, Money } from '@prestamos/api-client';

export interface LoanProduct {
  id: string;
  name: string;
  minAmount: Money;
  maxAmount: Money;
  minTermMonths: number;
  maxTermMonths: number;
  tea: string;
}

export interface SimulateLoanRequest {
  productId?: string;
  amount: Money;
  termMonths: number;
}

export interface SimulationResult {
  monthlyInstallment: Money;
  tea: string;
  tcea: string;
  totalCost: Money;
  installmentsCount: number;
}

export interface CreateApplicationPayload {
  productId: string;
  requestedAmount: Money;
  termMonths: number;
  monthlyIncome: Money;
  occupation: string;
  employerName?: string;
  incomeDocumentUri?: string;
}

export interface LoanApplicationItem {
  id: string;
  applicationNumber: string;
  status: ApplicationStatus;
  requestedAmount: Money;
  approvedAmount?: Money;
  termMonths: number;
  createdAt: string;
  updatedAt: string;
}
