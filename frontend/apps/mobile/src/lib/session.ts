import { create } from 'zustand';
import { AppState, type AppStateStatus } from 'react-native';
import {
  setAccessToken,
  saveRefreshToken,
  getStoredRefreshToken,
  clearSession,
  onSessionExpired,
} from './api';
import {
  isBiometricsEnabled,
  setBiometricsPreference,
  getBiometricIdentifier,
} from './biometrics';
import { useToastStore } from './toast';

export interface ClientUser {
  id: string;
  customerId: string;
  fullName: string;
  email: string;
  docType?: string;
  docNumber?: string;
  phone?: string;
  kycStatus?: 'PENDING' | 'VERIFIED' | 'REJECTED' | 'EXPIRED';
}

interface SessionState {
  customer: ClientUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  lastActiveAt: number;
  biometricsEnabled: boolean;
  savedBiometricIdentifier: string | null;

  initSession: () => Promise<void>;
  setSession: (
    customer: ClientUser,
    accessToken: string,
    refreshToken?: string
  ) => Promise<void>;
  updateCustomer: (partial: Partial<ClientUser>) => void;
  toggleBiometrics: (enabled: boolean) => Promise<void>;
  logout: (reason?: 'manual' | 'inactivity' | 'expired') => Promise<void>;
  touchActivity: () => void;
}

export const INACTIVITY_TIMEOUT_MS = 15 * 60 * 1000; // 15 minutos (RNF-19 / FE-CP-02)

export const useSessionStore = create<SessionState>((set, get) => {
  let previousAppState: AppStateStatus = (AppState.currentState as AppStateStatus) ?? 'active';
  let foregroundTimer: ReturnType<typeof setTimeout> | null = null;

  const scheduleForegroundTimer = () => {
    if (foregroundTimer) clearTimeout(foregroundTimer);
    foregroundTimer = setTimeout(() => {
      if (get().isAuthenticated) {
        void get().logout('inactivity');
      }
    }, INACTIVITY_TIMEOUT_MS);
  };

  AppState.addEventListener('change', (nextAppState) => {
    const { isAuthenticated, lastActiveAt, logout, touchActivity } = get();

    if (isAuthenticated) {
      const now = Date.now();
      const elapsed = now - lastActiveAt;

      if (previousAppState.match(/inactive|background/) && nextAppState === 'active') {
        if (elapsed >= INACTIVITY_TIMEOUT_MS) {
          void logout('inactivity');
        } else {
          touchActivity();
        }
      } else if (nextAppState.match(/inactive|background/)) {
        set({ lastActiveAt: now });
      }
    }
    previousAppState = nextAppState;
  });

  // Suscripción al interceptor 401 de @prestamos/api-client
  onSessionExpired(() => {
    void get().logout('expired');
  });

  return {
    customer: null,
    isAuthenticated: false,
    isLoading: true,
    lastActiveAt: Date.now(),
    biometricsEnabled: false,
    savedBiometricIdentifier: null,

    async initSession() {
      set({ isLoading: true });
      try {
        const [storedRefreshToken, bioEnabled, bioId] = await Promise.all([
          getStoredRefreshToken(),
          isBiometricsEnabled(),
          getBiometricIdentifier(),
        ]);

        if (!storedRefreshToken) {
          set({
            customer: null,
            isAuthenticated: false,
            isLoading: false,
            biometricsEnabled: bioEnabled,
            savedBiometricIdentifier: bioId,
          });
          return;
        }

        setAccessToken(storedRefreshToken);
        scheduleForegroundTimer();
        set({
          customer: {
            id: 'usr-100',
            customerId: 'cus-001',
            fullName: 'María Quispe Huamán',
            email: 'cliente@correo.pe',
            docType: 'DNI',
            docNumber: '41234567',
            phone: '987000000',
            kycStatus: 'VERIFIED',
          },
          isAuthenticated: true,
          isLoading: false,
          lastActiveAt: Date.now(),
          biometricsEnabled: bioEnabled,
          savedBiometricIdentifier: bioId,
        });
      } catch {
        await clearSession();
        set({ customer: null, isAuthenticated: false, isLoading: false });
      }
    },

    async setSession(customer, token, refreshToken) {
      setAccessToken(token);
      if (refreshToken) {
        await saveRefreshToken(refreshToken);
      }
      scheduleForegroundTimer();
      set({
        customer,
        isAuthenticated: true,
        isLoading: false,
        lastActiveAt: Date.now(),
      });
    },

    updateCustomer(partial) {
      const current = get().customer;
      if (current) {
        set({ customer: { ...current, ...partial } });
      }
    },

    async toggleBiometrics(enabled) {
      const currentCustomer = get().customer;
      const identifier = currentCustomer?.email ?? currentCustomer?.phone;
      await setBiometricsPreference(enabled, identifier);
      set({
        biometricsEnabled: enabled,
        savedBiometricIdentifier: enabled ? (identifier ?? null) : null,
      });
    },

    async logout(reason = 'manual') {
      if (foregroundTimer) {
        clearTimeout(foregroundTimer);
        foregroundTimer = null;
      }
      await clearSession();
      set({
        customer: null,
        isAuthenticated: false,
        isLoading: false,
        lastActiveAt: Date.now(),
      });

      if (reason === 'inactivity') {
        useToastStore.getState().showToast({
          title: 'Sesión cerrada por inactividad',
          detail: 'Por tu seguridad, cerramos la sesión tras 15 minutos sin actividad (RNF-19).',
          tone: 'warning',
        });
      } else if (reason === 'expired') {
        useToastStore.getState().showToast({
          title: 'Sesión expirada',
          detail: 'Por favor vuelve a iniciar sesión para continuar.',
          tone: 'info',
        });
      }
    },

    touchActivity() {
      set({ lastActiveAt: Date.now() });
      if (get().isAuthenticated) {
        scheduleForegroundTimer();
      }
    },
  };
});
