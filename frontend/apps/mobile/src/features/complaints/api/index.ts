import { api } from '@/lib/api';
import type { CreateComplaintPayload, ComplaintResult } from '../types';

export async function submitComplaintApi(payload: CreateComplaintPayload): Promise<ComplaintResult> {
  const { data } = await api.post<ComplaintResult>('/api/client/complaints', payload);
  return data;
}

export async function getComplaintsApi(): Promise<ComplaintResult[]> {
  const { data } = await api.get<ComplaintResult[]>('/api/client/complaints');
  return data;
}
