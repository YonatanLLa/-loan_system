import React from 'react';
import { StyleSheet, View, Text, FlatList, RefreshControl, Switch } from 'react-native';
import { useNotifications, NotificationCard } from '@/features/notifications';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/components/ui/status-badge';

export default function NotificationsScreen() {
  const {
    notifications,
    preferences,
    pushRegistered,
    isLoading,
    refresh,
    markAsRead,
    registerDevicePush,
    updatePreferences,
  } = useNotifications();

  return (
    <View style={styles.container}>
      <FlatList
        data={notifications}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.list}
        refreshControl={<RefreshControl refreshing={isLoading} onRefresh={refresh} />}
        ListHeaderComponent={
          <Card style={styles.prefsCard}>
            <View style={styles.headerRow}>
              <Text style={styles.prefsTitle}>Preferencias y Push</Text>
              <StatusBadge label="FE-CP-20 · MOV-01" tone="info" />
            </View>

            <View style={styles.prefRow}>
              <Text style={styles.prefLabel}>Recordatorios de próximo vencimiento</Text>
              <Switch
                value={preferences.paymentReminders}
                onValueChange={(val) =>
                  void updatePreferences({ ...preferences, paymentReminders: val })
                }
              />
            </View>

            <View style={styles.prefRow}>
              <Text style={styles.prefLabel}>Confirmación de pagos y desembolsos</Text>
              <Switch
                value={preferences.accountAlerts}
                onValueChange={(val) =>
                  void updatePreferences({ ...preferences, accountAlerts: val })
                }
              />
            </View>

            <View style={styles.prefRow}>
              <Text style={styles.prefLabel}>Promociones y pre-aprobaciones</Text>
              <Switch
                value={preferences.promotions}
                onValueChange={(val) =>
                  void updatePreferences({ ...preferences, promotions: val })
                }
              />
            </View>

            {pushRegistered ? (
              <StatusBadge label="Dispositivo registrado para Push (MOV-01)" tone="success" />
            ) : (
              <Button
                title="Habilitar Notificaciones Push en este Dispositivo"
                variant="outline"
                onPress={registerDevicePush}
              />
            )}
          </Card>
        }
        renderItem={({ item }) => (
          <NotificationCard
            notification={item}
            onPress={() => {
              if (!item.isRead) void markAsRead(item.id);
            }}
          />
        )}
        ListEmptyComponent={
          !isLoading ? (
            <View style={styles.emptyBox}>
              <Text style={styles.emptyTitle}>Bandeja al día</Text>
              <Text style={styles.emptyDesc}>No tienes notificaciones pendientes.</Text>
            </View>
          ) : undefined
        }
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F9FAFB',
  },
  list: {
    padding: 16,
    gap: 12,
  },
  prefsCard: {
    gap: 10,
    marginBottom: 8,
  },
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  prefsTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#111827',
  },
  prefRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 4,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#E5E7EB',
  },
  prefLabel: {
    fontSize: 13,
    color: '#374151',
    flex: 1,
  },
  emptyBox: {
    padding: 40,
    alignItems: 'center',
    gap: 8,
  },
  emptyTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#374151',
  },
  emptyDesc: {
    fontSize: 14,
    color: '#9CA3AF',
  },
});
