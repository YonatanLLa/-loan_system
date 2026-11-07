export type ComplaintType = 'QUEJA' | 'RECLAMO';

export interface CreateComplaintPayload {
  type: ComplaintType;
  fullName: string;
  docType: 'DNI' | 'CE' | 'RUC';
  docNumber: string;
  email: string;
  phone: string;
  detail: string;
  requestedSolution: string;
  loanId?: string;
}

export interface ComplaintResult {
  id: string;
  filingNumber: string; // ej. QUE-2026-0001
  status: 'RECEIVED' | 'IN_REVIEW' | 'RESOLVED';
  createdAt: string;
}
