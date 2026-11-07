import { useState, useCallback, useEffect } from 'react';
import { Platform } from 'react-native';
import { processApiError, useToastStore } from '@/lib/toast';
import type { ClientNotification, NotificationPreferences } from '../types';
import {
  getNotificationsApi,
  markNotificationAsReadApi,
  registerPushTokenApi,
  updatePreferencesApi,
} from '../api';

export function useNotifications() {
  const [notifications, setNotifications] = useState<ClientNotification[]>([]);
  const [preferences, setPreferences] = useState<NotificationPreferences>({
    paymentReminders: true,
    accountAlerts: true,
    promotions: false,
  });
  const [pushRegistered, setPushRegistered] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await getNotificationsApi();
      setNotifications(data);
    } catch (err: unknown) {
      const { apiError } = processApiError(err);
      setError(apiError.detail);
    } finally {
      setIsLoading(false);
    }
  }, []);

  const markAsRead = useCallback(async (id: string) => {
    try {
      await markNotificationAsReadApi(id);
      setNotifications((prev) =>
        prev.map((n) => (n.id === id ? { ...n, isRead: true } : n))
      );
    } catch (err: unknown) {
      processApiError(err);
    }
  }, []);

  const registerDevicePush = useCallback(async () => {
    const platform: 'ios' | 'android' = Platform.OS === 'ios' ? 'ios' : 'android';
    const token = `ExponentPushToken[${platform}-device-token]`;
    try {
      await registerPushTokenApi({ token, platform });
      setPushRegistered(true);
      useToastStore.getState().showToast({
        title: 'Notificaciones Push activas',
        detail: 'Recibirás alertas de vencimientos, pagos y desembolsos (MOV-01).',
        tone: 'success',
      });
    } catch (err: unknown) {
      processApiError(err);
    }
  }, []);

  const updatePreferences = useCallback(async (next: NotificationPreferences) => {
    setPreferences(next);
    try {
      const saved = await updatePreferencesApi(next);
      setPreferences(saved);
    } catch (err: unknown) {
      processApiError(err);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  return {
    notifications,
    preferences,
    pushRegistered,
    isLoading,
    error,
    refresh,
    markAsRead,
    registerDevicePush,
    updatePreferences,
  };
}
