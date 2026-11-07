import React from 'react';
import { StyleSheet, Text, View, Pressable } from 'react-native';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatMoney, formatBusinessDate } from '@prestamos/utils';
import type { LoanSummary } from '../types';

interface LoanCardProps {
  loan: LoanSummary;
  onPress?: () => void;
}

export function LoanCard({ loan, onPress }: LoanCardProps) {
  return (
    <Pressable onPress={onPress}>
      <Card style={styles.card}>
        <View style={styles.header}>
          <View>
            <Text style={styles.productName}>{loan.productName}</Text>
            <Text style={styles.loanNumber}>#{loan.loanNumber}</Text>
          </View>
          <StatusBadge kind="loan" status={loan.status} />
        </View>

        <View style={styles.balanceRow}>
          <View style={styles.balanceSection}>
            <Text style={styles.balanceLabel}>Saldo insoluto</Text>
            <Text style={styles.balanceValue}>
              {formatMoney(loan.outstandingBalance, loan.currency)}
            </Text>
          </View>

          {loan.lateFeeBalance && Number(loan.lateFeeBalance) > 0 ? (
            <View style={styles.moraSection}>
              <Text style={styles.moraLabel}>Mora acumulada</Text>
              <Text style={styles.moraValue}>
                {formatMoney(loan.lateFeeBalance, loan.currency)}
              </Text>
            </View>
          ) : null}
        </View>

        {loan.nextDueDate && loan.nextDueAmount ? (
          <View style={styles.nextDueBox}>
            <View>
              <Text style={styles.nextDueLabel}>Próximo vencimiento</Text>
              <Text style={styles.nextDueDate}>{formatBusinessDate(loan.nextDueDate)}</Text>
            </View>
            <Text style={styles.nextDueAmount}>
              {formatMoney(loan.nextDueAmount, loan.currency)}
            </Text>
          </View>
        ) : null}

        {loan.daysLate && loan.daysLate > 0 ? (
          <View style={styles.lateBox}>
            <Text style={styles.lateText}>⚠️ Atraso de {loan.daysLate} días</Text>
          </View>
        ) : loan.daysRemaining !== undefined ? (
          <View style={styles.remainingBox}>
            <Text style={styles.remainingText}>
              Vence en {loan.daysRemaining} día{loan.daysRemaining === 1 ? '' : 's'}
            </Text>
          </View>
        ) : null}
      </Card>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    gap: 12,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
  },
  productName: {
    fontSize: 16,
    fontWeight: '700',
    color: '#111827',
  },
  loanNumber: {
    fontSize: 12,
    color: '#6B7280',
  },
  balanceRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-end',
  },
  balanceSection: {
    gap: 2,
  },
  balanceLabel: {
    fontSize: 12,
    color: '#6B7280',
  },
  balanceValue: {
    fontSize: 22,
    fontWeight: '700',
    color: '#111827',
  },
  moraSection: {
    alignItems: 'flex-end',
    gap: 2,
  },
  moraLabel: {
    fontSize: 11,
    color: '#DC2626',
  },
  moraValue: {
    fontSize: 15,
    fontWeight: '700',
    color: '#DC2626',
  },
  nextDueBox: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: '#F3F4F6',
    padding: 10,
    borderRadius: 8,
  },
  nextDueLabel: {
    fontSize: 11,
    color: '#6B7280',
  },
  nextDueDate: {
    fontSize: 13,
    fontWeight: '600',
    color: '#111827',
  },
  nextDueAmount: {
    fontSize: 15,
    fontWeight: '700',
    color: '#1F2937',
  },
  lateBox: {
    backgroundColor: '#FEE2E2',
    paddingVertical: 4,
    paddingHorizontal: 8,
    borderRadius: 6,
    alignSelf: 'flex-start',
  },
  lateText: {
    fontSize: 12,
    color: '#DC2626',
    fontWeight: '600',
  },
  remainingBox: {
    backgroundColor: '#EFF6FF',
    paddingVertical: 4,
    paddingHorizontal: 8,
    borderRadius: 6,
    alignSelf: 'flex-start',
  },
  remainingText: {
    fontSize: 12,
    color: '#1D4ED8',
    fontWeight: '600',
  },
});
