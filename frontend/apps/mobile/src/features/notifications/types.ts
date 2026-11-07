export type NotificationType =
  | 'PAYMENT_DUE'
  | 'PAYMENT_CONFIRMED'
  | 'DISBURSEMENT_READY'
  | 'APPLICATION_UPDATE';

export interface ClientNotification {
  id: string;
  title: string;
  message: string;
  type: NotificationType;
  isRead: boolean;
  createdAt: string;
  actionUrl?: string;
}

export interface PushTokenPayload {
  token: string;
  platform: 'ios' | 'android';
}

export interface NotificationPreferences {
  paymentReminders: boolean;
  promotions: boolean;
  accountAlerts: boolean;
}
