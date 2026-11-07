export interface ContractDocument {
  loanId: string;
  contractNumber: string;
  pdfUrl: string;
  summarySheetPdfUrl: string;
  isSigned: boolean;
  signedAt?: string;
}

export interface SignContractPayload {
  loanId: string;
  otpCode: string;
}
