import { api } from '@/lib/api';
import type { ClientNotification, PushTokenPayload, NotificationPreferences } from '../types';

export async function getNotificationsApi(): Promise<ClientNotification[]> {
  const { data } = await api.get<ClientNotification[]>('/api/client/notifications');
  return data;
}

export async function markNotificationAsReadApi(id: string): Promise<void> {
  await api.put(`/api/client/notifications/${id}/read`);
}

export async function registerPushTokenApi(payload: PushTokenPayload): Promise<void> {
  await api.post('/api/client/notifications/push-token', payload);
}

export async function updatePreferencesApi(
  payload: NotificationPreferences
): Promise<NotificationPreferences> {
  const { data } = await api.put<NotificationPreferences>(
    '/api/client/notifications/preferences',
    payload
  );
  return data;
}
