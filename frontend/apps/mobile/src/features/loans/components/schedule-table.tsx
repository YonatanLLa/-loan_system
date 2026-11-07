import React from 'react';
import { StyleSheet, Text, View, FlatList } from 'react-native';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatMoney, formatBusinessDate } from '@prestamos/utils';
import type { LoanInstallmentItem } from '../types';

interface ScheduleTableProps {
  installments: LoanInstallmentItem[];
  currency?: string;
}

export function ScheduleTable({ installments, currency = 'PEN' }: ScheduleTableProps) {
  return (
    <View style={styles.container}>
      <Text style={styles.sectionTitle}>Cronograma de Pagos</Text>
      <FlatList
        data={installments}
        keyExtractor={(item) => item.id}
        scrollEnabled={false}
        renderItem={({ item }) => (
          <View style={styles.row}>
            <View style={styles.colNumber}>
              <Text style={styles.numText}>Cuota {item.installmentNumber}</Text>
              <Text style={styles.dateText}>{formatBusinessDate(item.dueDate)}</Text>
            </View>

            <View style={styles.colAmount}>
              <Text style={styles.amountText}>{formatMoney(item.amount, currency)}</Text>
              {Number(item.lateFee) > 0 ? (
                <Text style={styles.lateFeeText}>+ Mora {formatMoney(item.lateFee, currency)}</Text>
              ) : null}
            </View>

            <StatusBadge kind="installment" status={item.status} />
          </View>
        )}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: 8,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#111827',
    marginBottom: 4,
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 12,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#E5E7EB',
  },
  colNumber: {
    gap: 2,
    flex: 1,
  },
  numText: {
    fontSize: 14,
    fontWeight: '600',
    color: '#111827',
  },
  dateText: {
    fontSize: 12,
    color: '#6B7280',
  },
  colAmount: {
    alignItems: 'flex-end',
    marginRight: 12,
  },
  amountText: {
    fontSize: 14,
    fontWeight: '700',
    color: '#111827',
  },
  lateFeeText: {
    fontSize: 11,
    color: '#DC2626',
  },
});
