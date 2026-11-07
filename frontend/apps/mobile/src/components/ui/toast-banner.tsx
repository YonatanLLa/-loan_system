import React, { useEffect } from 'react';
import { StyleSheet, Text, View, Pressable } from 'react-native';
import { useToastStore } from '@/lib/toast';
import { statusToneColors } from '@/constants/status';

export function ToastBanner() {
  const current = useToastStore((state) => state.current);
  const isOffline = useToastStore((state) => state.isOffline);
  const dismissToast = useToastStore((state) => state.dismissToast);

  useEffect(() => {
    if (!current) return;
    const timer = setTimeout(() => {
      dismissToast();
    }, 4500);
    return () => clearTimeout(timer);
  }, [current, dismissToast]);

  return (
    <View style={styles.wrapper} pointerEvents="box-none">
      {isOffline ? (
        <View style={styles.offlineBanner}>
          <Text style={styles.offlineText}>
            Sin conexión a internet. Tus operaciones de pago conservarán su clave de idempotencia al reintentar (MOV-06).
          </Text>
        </View>
      ) : null}

      {current ? (
        <Pressable
          onPress={dismissToast}
          style={[
            styles.toast,
            {
              backgroundColor: statusToneColors[current.tone].background,
              borderColor: statusToneColors[current.tone].border,
            },
          ]}>
          <View style={styles.toastHeader}>
            <Text style={[styles.toastTitle, { color: statusToneColors[current.tone].text }]}>
              {current.title}
            </Text>
            <Text style={[styles.closeHint, { color: statusToneColors[current.tone].text }]}>✕</Text>
          </View>
          <Text style={[styles.toastDetail, { color: statusToneColors[current.tone].text }]}>
            {current.detail}
          </Text>
          {current.traceId ? (
            <Text style={styles.traceText}>Código de rastreo: {current.traceId}</Text>
          ) : null}
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrapper: {
    position: 'absolute',
    top: 48,
    left: 16,
    right: 16,
    zIndex: 999,
    gap: 8,
  },
  offlineBanner: {
    backgroundColor: '#78350F',
    paddingVertical: 8,
    paddingHorizontal: 12,
    borderRadius: 8,
  },
  offlineText: {
    color: '#FEF3C7',
    fontSize: 12,
    fontWeight: '600',
    textAlign: 'center',
  },
  toast: {
    padding: 14,
    borderRadius: 12,
    borderWidth: 1.5,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 6,
    elevation: 4,
    gap: 4,
  },
  toastHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  toastTitle: {
    fontSize: 14,
    fontWeight: '700',
  },
  closeHint: {
    fontSize: 13,
    fontWeight: '700',
  },
  toastDetail: {
    fontSize: 13,
    lineHeight: 18,
  },
  traceText: {
    fontSize: 11,
    color: '#6B7280',
    marginTop: 2,
  },
});
