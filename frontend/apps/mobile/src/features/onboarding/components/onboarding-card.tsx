import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import type { KycStatus } from '@prestamos/api-client';

interface OnboardingCardProps {
  kycStatus?: KycStatus;
  accountsCount: number;
}

export function OnboardingCard({ kycStatus = 'PENDING', accountsCount }: OnboardingCardProps) {
  return (
    <Card style={styles.card}>
      <Text style={styles.title}>Estado del Onboarding</Text>

      <View style={styles.row}>
        <Text style={styles.label}>Verificación de Identidad (KYC)</Text>
        <StatusBadge kind="kyc" status={kycStatus} />
      </View>

      <View style={styles.row}>
        <Text style={styles.label}>Cuentas de desembolso registradas</Text>
        <Text style={styles.value}>{accountsCount}</Text>
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  card: {
    gap: 12,
  },
  title: {
    fontSize: 18,
    fontWeight: '700',
    color: '#111827',
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 6,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#E5E7EB',
  },
  label: {
    fontSize: 14,
    color: '#4B5563',
  },
  value: {
    fontSize: 14,
    fontWeight: '600',
    color: '#111827',
  },
});
