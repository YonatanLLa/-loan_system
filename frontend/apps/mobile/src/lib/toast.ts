import { create } from 'zustand';
import { toApiError, toFormErrors, type ApiError } from '@prestamos/api-client';

export type ToastTone = 'danger' | 'success' | 'warning' | 'info';

export interface ToastMessage {
  id: string;
  title: string;
  detail: string;
  tone: ToastTone;
  traceId?: string;
}

interface ToastState {
  current: ToastMessage | null;
  isOffline: boolean;
  showToast: (msg: Omit<ToastMessage, 'id'>) => void;
  dismissToast: () => void;
  setOffline: (offline: boolean) => void;
}

export const useToastStore = create<ToastState>((set) => ({
  current: null,
  isOffline: false,

  showToast(msg) {
    const id = `${Date.now()}`;
    set({ current: { ...msg, id } });
  },

  dismissToast() {
    set({ current: null });
  },

  setOffline(offline) {
    set({ isOffline: offline });
  },
}));

/**
 * Normaliza cualquier error con RFC 7807 (`toApiError`), dispara toast nativo en español
 * y devuelve errores por campo (`toFormErrors`) para formularios (FE-NF-05, MOV-06).
 */
export function processApiError(err: unknown): {
  apiError: ApiError;
  fieldErrors: Record<string, string>;
} {
  const apiError = toApiError(err);
  const fieldErrors = toFormErrors(apiError);
  const { showToast, setOffline } = useToastStore.getState();

  if (apiError.isNetworkError) {
    setOffline(true);
  } else {
    setOffline(false);
  }

  showToast({
    title: apiError.title,
    detail: apiError.detail,
    tone: apiError.isNetworkError ? 'warning' : 'danger',
    traceId: apiError.traceId,
  });

  return { apiError, fieldErrors };
}
