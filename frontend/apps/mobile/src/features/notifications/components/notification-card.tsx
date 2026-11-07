import React from 'react';
import { StyleSheet, Text, View, Pressable } from 'react-native';
import { Card } from '@/components/ui/card';
import type { ClientNotification } from '../types';

interface NotificationCardProps {
  notification: ClientNotification;
  onPress?: () => void;
}

export function NotificationCard({ notification, onPress }: NotificationCardProps) {
  return (
    <Pressable onPress={onPress}>
      <Card style={[styles.card, !notification.isRead && styles.unreadCard]}>
        <View style={styles.header}>
          <Text style={[styles.title, !notification.isRead && styles.unreadTitle]}>
            {notification.title}
          </Text>
          {!notification.isRead ? <View style={styles.unreadDot} /> : null}
        </View>

        <Text style={styles.message}>{notification.message}</Text>
        <Text style={styles.time}>{notification.createdAt}</Text>
      </Card>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    gap: 6,
  },
  unreadCard: {
    borderColor: '#93C5FD',
    backgroundColor: '#F0F9FF',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  title: {
    fontSize: 15,
    fontWeight: '600',
    color: '#1F2937',
  },
  unreadTitle: {
    color: '#1D4ED8',
    fontWeight: '700',
  },
  unreadDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: '#2563EB',
  },
  message: {
    fontSize: 13,
    color: '#4B5563',
    lineHeight: 18,
  },
  time: {
    fontSize: 11,
    color: '#9CA3AF',
    alignSelf: 'flex-end',
  },
});
