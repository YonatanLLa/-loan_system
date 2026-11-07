// Cliente HTTP de la app móvil, reutilizando el contrato del monorepo.
// Importa el polyfill de crypto ANTES de crear el cliente.
import './crypto-polyfill';

import { createApiClient, type SessionPort } from '@prestamos/api-client';
import { mockAxiosAdapter } from '@prestamos/mocks';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

// URL del backend .NET. En Expo las variables EXPO_PUBLIC_* quedan embebidas.
// - iOS simulator:        http://localhost:5000
// - Android emulator:     http://10.0.2.2:5000
// - Dispositivo físico:   http://<IP-de-tu-PC>:5000
const BASE_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5000';
const USE_MOCKS = process.env.EXPO_PUBLIC_USE_MOCKS !== 'false';

const REFRESH_TOKEN_KEY = 'prestamos.refreshToken';

// El access token vive solo en memoria (equivalente móvil a FE-NF-08).
let accessToken: string | null = null;
let webFallbackRefreshToken: string | null = null;
let sessionExpiredListener: (() => void) | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function onSessionExpired(callback: () => void) {
  sessionExpiredListener = callback;
}

export async function saveRefreshToken(token: string) {
  if (Platform.OS === 'web') {
    webFallbackRefreshToken = token;
    try {
      if (typeof window !== 'undefined' && window.sessionStorage) {
        window.sessionStorage.setItem(REFRESH_TOKEN_KEY, token);
      }
    } catch {
      // ignore storage errors on web SSR
    }
    return;
  }
  await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, token);
}

export async function getStoredRefreshToken(): Promise<string | null> {
  if (Platform.OS === 'web') {
    try {
      if (typeof window !== 'undefined' && window.sessionStorage) {
        return window.sessionStorage.getItem(REFRESH_TOKEN_KEY) ?? webFallbackRefreshToken;
      }
    } catch {
      // ignore storage errors on web SSR
    }
    return webFallbackRefreshToken;
  }
  return await SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
}

export async function clearSession() {
  accessToken = null;
  if (Platform.OS === 'web') {
    webFallbackRefreshToken = null;
    try {
      if (typeof window !== 'undefined' && window.sessionStorage) {
        window.sessionStorage.removeItem(REFRESH_TOKEN_KEY);
      }
    } catch {
      // ignore storage errors on web SSR
    }
    return;
  }
  await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
}

// En web el refresco usa una cookie HttpOnly; en móvil no hay navegador, así
// que guardamos el refresh token en el almacén seguro del sistema (Keychain /
// Keystore) y lo enviamos explícitamente al endpoint de refresco.
const session: SessionPort = {
  getAccessToken: () => accessToken,

  async refresh() {
    const refreshToken = await getStoredRefreshToken();
    if (!refreshToken) return false;

    try {
      const { data } = await api.post<{
        accessToken: string;
        refreshToken?: string;
      }>('/api/auth/refresh', { refreshToken }, { skipAuthRefresh: true });
      accessToken = data.accessToken;
      if (data.refreshToken) await saveRefreshToken(data.refreshToken);
      return true;
    } catch {
      return false;
    }
  },

  onSessionExpired() {
    void clearSession();
    if (sessionExpiredListener) {
      sessionExpiredListener();
    }
  },
};

/**
 * Instancia única del cliente HTTP para toda la app.
 * Mientras no exista backend o `EXPO_PUBLIC_USE_MOCKS` no sea `'false'`,
 * enruta las peticiones por `mockAxiosAdapter` de `@prestamos/mocks` (con datos semilla en memoria).
 */
export const api = createApiClient({
  baseURL: BASE_URL,
  session,
  adapter: USE_MOCKS ? mockAxiosAdapter : undefined,
});
