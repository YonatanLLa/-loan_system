import { z } from 'zod';

export const pushTokenSchema = z.object({
  token: z.string().min(10),
  platform: z.enum(['ios', 'android']),
});

export const preferencesSchema = z.object({
  paymentReminders: z.boolean(),
  promotions: z.boolean(),
  accountAlerts: z.boolean(),
});

export type PushTokenFormData = z.infer<typeof pushTokenSchema>;
export type PreferencesFormData = z.infer<typeof preferencesSchema>;
