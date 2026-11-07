import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/components/ui/status-badge';
import { maskTail } from '@prestamos/utils';
import type { CustomerBankAccount } from '@prestamos/api-client';

interface BankAccountItemProps {
  account: CustomerBankAccount;
  onMakePrimary?: () => void;
}

export function BankAccountItem({ account, onMakePrimary }: BankAccountItemProps) {
  return (
    <Card style={styles.card}>
      <View style={styles.header}>
        <Text style={styles.bankName}>{account.bankName}</Text>
        <View style={styles.badges}>
          {account.isVerified ? (
            <StatusBadge label="Verificada" tone="success" />
          ) : (
            <StatusBadge label="Por verificar" tone="warning" />
          )}
          {account.isPrimary ? (
            <StatusBadge label="Principal" tone="info" />
          ) : (
            <StatusBadge label="Secundaria" tone="neutral" />
          )}
        </View>
      </View>

      <Text style={styles.accountNumber}>{maskTail(account.accountNumberMasked)}</Text>
      {account.cci ? <Text style={styles.cci}>CCI: {maskTail(account.cci)}</Text> : null}

      {!account.isPrimary && onMakePrimary ? (
        <Button
          title="Elegir como Principal (requiere OTP)"
          variant="outline"
          onPress={onMakePrimary}
        />
      ) : null}
    </Card>
  );
}

const styles = StyleSheet.create({
  card: {
    gap: 8,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: 8,
  },
  badges: {
    flexDirection: 'row',
    gap: 6,
  },
  bankName: {
    fontSize: 15,
    fontWeight: '700',
    color: '#111827',
    flex: 1,
  },
  accountNumber: {
    fontSize: 14,
    color: '#4B5563',
  },
  cci: {
    fontSize: 12,
    color: '#6B7280',
  },
});
