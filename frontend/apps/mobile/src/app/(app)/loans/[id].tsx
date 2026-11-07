import React, { useState } from 'react';
import { StyleSheet, View, Text, ScrollView, RefreshControl, TextInput } from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import * as WebBrowser from 'expo-web-browser';
import {
  useLoanDetail,
  ScheduleTable,
  getLoanStatementPdfUrl,
  type PrepaymentReductionOption,
} from '@/features/loans';
import { useContract, ContractCard } from '@/features/contract';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatMoney, formatBusinessDate } from '@prestamos/utils';

export default function LoanDetailScreen() {
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();
  const loanId = id ?? '';

  const {
    loan,
    schedule,
    paymentsHistory,
    prepaymentQuote,
    isLoading,
    refresh,
    quotePrepayment,
  } = useLoanDetail(loanId);

  const { contract, requestOtp, sign, isLoading: isContractLoading } = useContract(loanId);

  // FE-CP-16 / FE-CP-17: Prepago total o parcial (Ley 29571 art. 85)
  const [prepaymentType, setPrepaymentType] = useState<'TOTAL' | 'PARTIAL'>('TOTAL');
  const [partialAmount, setPartialAmount] = useState('500');
  const [reductionOption, setReductionOption] =
    useState<PrepaymentReductionOption>('REDUCE_INSTALLMENT');

  const handleOpenStatementPdf = async () => {
    await WebBrowser.openBrowserAsync(getLoanStatementPdfUrl(loanId));
  };

  const handleQuotePrepayment = async () => {
    await quotePrepayment(
      prepaymentType,
      prepaymentType === 'PARTIAL' ? partialAmount : undefined,
      prepaymentType === 'PARTIAL' ? reductionOption : undefined
    );
  };

  if (!loan && !isLoading) {
    return (
      <View style={styles.center}>
        <Text style={styles.errorText}>Préstamo no encontrado</Text>
      </View>
    );
  }

  return (
    <ScrollView
      contentContainerStyle={styles.container}
      refreshControl={<RefreshControl refreshing={isLoading} onRefresh={refresh} />}>
      {loan ? (
        <>
          {/* Resumen del Préstamo (FE-CP-10 / FE-CP-15) */}
          <Card style={styles.card}>
            <View style={styles.header}>
              <View>
                <Text style={styles.productName}>{loan.productName}</Text>
                <Text style={styles.loanNumber}>#{loan.loanNumber}</Text>
              </View>
              <StatusBadge kind="loan" status={loan.status} />
            </View>

            <View style={styles.metricsGrid}>
              <View style={styles.metric}>
                <Text style={styles.metricLabel}>Monto Original</Text>
                <Text style={styles.metricValue}>
                  {formatMoney(loan.principalAmount, loan.currency)}
                </Text>
              </View>

              <View style={styles.metric}>
                <Text style={styles.metricLabel}>Saldo Insoluto</Text>
                <Text style={styles.metricValue}>
                  {formatMoney(loan.outstandingBalance, loan.currency)}
                </Text>
              </View>
            </View>

            {loan.nextDueDate && loan.nextDueAmount ? (
              <View style={styles.dueBox}>
                <View>
                  <Text style={styles.dueLabel}>Próximo vencimiento</Text>
                  <Text style={styles.dueDate}>{formatBusinessDate(loan.nextDueDate)}</Text>
                </View>
                <Text style={styles.dueAmount}>
                  {formatMoney(loan.nextDueAmount, loan.currency)}
                </Text>
              </View>
            ) : null}

            <View style={styles.actions}>
              <Button
                title="Pagar Cuota"
                onPress={() =>
                  router.push({ pathname: '/(app)/pay/[loanId]', params: { loanId: loan.id } })
                }
              />
              {/* FE-CP-15: Estado de cuenta descargable */}
              <Button
                title="Descargar Estado de Cuenta (PDF)"
                variant="outline"
                onPress={handleOpenStatementPdf}
              />
            </View>
          </Card>

          {/* FE-CP-09: Revisión y firma de contrato con OTP */}
          {contract ? (
            <ContractCard
              contract={contract}
              onRequestOtp={requestOtp}
              onSign={sign}
              isLoading={isContractLoading}
            />
          ) : null}

          {/* FE-CP-16 / FE-CP-17: Prepago Total y Abono Extraordinario Parcial (Ley 29571 art. 85) */}
          <Card style={styles.card}>
            <View style={styles.header}>
              <Text style={styles.sectionTitle}>Pago Anticipado / Prepago</Text>
              <StatusBadge label="FE-CP-16 · FE-CP-17" tone="info" />
            </View>
            <Text style={styles.subtitle}>
              Sin penalidades conforme al Art. 85 del Código de Protección y Defensa del Consumidor (Ley 29571).
            </Text>

            <View style={styles.toggleRow}>
              <Button
                title="Cancelación Total"
                variant={prepaymentType === 'TOTAL' ? 'primary' : 'outline'}
                onPress={() => setPrepaymentType('TOTAL')}
                style={styles.halfBtn}
              />
              <Button
                title="Prepago Parcial"
                variant={prepaymentType === 'PARTIAL' ? 'primary' : 'outline'}
                onPress={() => setPrepaymentType('PARTIAL')}
                style={styles.halfBtn}
              />
            </View>

            {prepaymentType === 'PARTIAL' ? (
              <View style={styles.partialConfig}>
                <Text style={styles.label}>Monto de abono extraordinario (S/)</Text>
                <TextInput
                  style={styles.input}
                  keyboardType="numeric"
                  value={partialAmount}
                  onChangeText={setPartialAmount}
                />

                <Text style={styles.label}>Efecto en tu cronograma (FE-CP-17):</Text>
                <View style={styles.toggleRow}>
                  <Button
                    title="Reducir Cuota"
                    variant={reductionOption === 'REDUCE_INSTALLMENT' ? 'primary' : 'outline'}
                    onPress={() => setReductionOption('REDUCE_INSTALLMENT')}
                    style={styles.halfBtn}
                  />
                  <Button
                    title="Reducir Plazo"
                    variant={reductionOption === 'REDUCE_TERM' ? 'primary' : 'outline'}
                    onPress={() => setReductionOption('REDUCE_TERM')}
                    style={styles.halfBtn}
                  />
                </View>
              </View>
            ) : null}

            <Button
              title="Cotizar Prepago"
              variant="secondary"
              onPress={handleQuotePrepayment}
              isLoading={isLoading}
            />

            {prepaymentQuote ? (
              <View style={styles.quoteBox}>
                <Text style={styles.quoteTitle}>
                  Liquidación de Prepago ({prepaymentQuote.type === 'TOTAL' ? 'Total' : 'Parcial'})
                </Text>
                <Text style={styles.quoteItem}>
                  Capital: {formatMoney(prepaymentQuote.outstandingPrincipal, loan.currency)}
                </Text>
                <Text style={styles.quoteItem}>
                  Intereses devengados a hoy: {formatMoney(prepaymentQuote.interestToDate, loan.currency)}
                </Text>
                <Text style={styles.quoteTotal}>
                  Total a pagar: {formatMoney(prepaymentQuote.totalPayable, loan.currency)}
                </Text>
                <Button
                  title="Pagar Liquidación Ahora"
                  onPress={() =>
                    router.push({
                      pathname: '/(app)/pay/[loanId]',
                      params: {
                        loanId: loan.id,
                        amount: prepaymentQuote.totalPayable,
                      },
                    })
                  }
                />
              </View>
            ) : null}
          </Card>

          {/* FE-CP-11: Cronograma semaforizado */}
          <Card style={styles.card}>
            <ScheduleTable installments={schedule} currency={loan.currency} />
          </Card>

          {/* FE-CP-15: Historial de pagos */}
          <Card style={styles.card}>
            <Text style={styles.sectionTitle}>Historial de Pagos (FE-CP-15)</Text>
            {paymentsHistory.length === 0 ? (
              <Text style={styles.emptyHistory}>Aún no se registran pagos aplicados.</Text>
            ) : (
              paymentsHistory.map((p) => (
                <View key={p.id} style={styles.historyRow}>
                  <View>
                    <Text style={styles.historyMethod}>{p.paymentMethod}</Text>
                    <Text style={styles.historyDate}>{p.paidAt}</Text>
                  </View>
                  <View style={styles.historyRight}>
                    <Text style={styles.historyAmount}>
                      {formatMoney(p.amount, loan.currency)}
                    </Text>
                    <StatusBadge kind="payment" status={p.status} />
                  </View>
                </View>
              ))
            )}
          </Card>
        </>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: 16,
    gap: 16,
    backgroundColor: '#F9FAFB',
  },
  center: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
  },
  errorText: {
    fontSize: 16,
    color: '#6B7280',
  },
  card: {
    gap: 14,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: 8,
  },
  productName: {
    fontSize: 18,
    fontWeight: '700',
    color: '#111827',
  },
  loanNumber: {
    fontSize: 13,
    color: '#6B7280',
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#111827',
    flex: 1,
  },
  subtitle: {
    fontSize: 13,
    color: '#6B7280',
    lineHeight: 18,
  },
  metricsGrid: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 8,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderColor: '#E5E7EB',
  },
  metric: {
    gap: 2,
  },
  metricLabel: {
    fontSize: 12,
    color: '#6B7280',
  },
  metricValue: {
    fontSize: 16,
    fontWeight: '700',
    color: '#111827',
  },
  dueBox: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: '#EFF6FF',
    padding: 12,
    borderRadius: 8,
  },
  dueLabel: {
    fontSize: 12,
    color: '#1E40AF',
  },
  dueDate: {
    fontSize: 14,
    fontWeight: '600',
    color: '#1E3A8A',
  },
  dueAmount: {
    fontSize: 16,
    fontWeight: '700',
    color: '#1E3A8A',
  },
  actions: {
    gap: 8,
  },
  toggleRow: {
    flexDirection: 'row',
    gap: 10,
  },
  halfBtn: {
    flex: 1,
  },
  partialConfig: {
    gap: 8,
  },
  label: {
    fontSize: 13,
    fontWeight: '500',
    color: '#374151',
  },
  input: {
    height: 44,
    borderWidth: 1,
    borderColor: '#D1D5DB',
    borderRadius: 8,
    paddingHorizontal: 12,
    fontSize: 15,
    backgroundColor: '#F9FAFB',
    color: '#111827',
  },
  quoteBox: {
    backgroundColor: '#F0FDF4',
    borderColor: '#BBF7D0',
    borderWidth: 1,
    borderRadius: 10,
    padding: 12,
    gap: 6,
  },
  quoteTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#166534',
  },
  quoteItem: {
    fontSize: 13,
    color: '#15803D',
  },
  quoteTotal: {
    fontSize: 16,
    fontWeight: '700',
    color: '#14532D',
    marginBottom: 4,
  },
  emptyHistory: {
    fontSize: 13,
    color: '#6B7280',
  },
  historyRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 8,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#E5E7EB',
  },
  historyMethod: {
    fontSize: 14,
    fontWeight: '600',
    color: '#111827',
  },
  historyDate: {
    fontSize: 12,
    color: '#6B7280',
  },
  historyRight: {
    alignItems: 'flex-end',
    gap: 4,
  },
  historyAmount: {
    fontSize: 14,
    fontWeight: '700',
    color: '#111827',
  },
});
