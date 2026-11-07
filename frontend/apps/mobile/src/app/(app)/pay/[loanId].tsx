import React, { useState } from 'react';
import { StyleSheet, View, Text, TextInput, ScrollView } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import * as WebBrowser from 'expo-web-browser';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import {
  usePayments,
  PaymentMethodSelector,
  getPaymentReceiptPdfUrl,
} from '@/features/payments';
import { formatMoney } from '@prestamos/utils';
import type { PaymentMethodType } from '@prestamos/api-client';

export default function PayScreen() {
  const params = useLocalSearchParams<{ loanId: string; amount?: string }>();
  const loanId = params.loanId ?? '';

  const {
    makePayment,
    resetIdempotency,
    checkStatus,
    lastPayment,
    pendingIdempotencyKey,
    isLoading,
    error,
  } = usePayments();

  const [amount, setAmount] = useState(params.amount ?? '350.00');
  const [method, setMethod] = useState<PaymentMethodType>('YAPE');
  const [operationNumber, setOperationNumber] = useState('');
  const [voucherUri, setVoucherUri] = useState<string | undefined>(undefined);

  const handleAmountChange = (val: string) => {
    setAmount(val);
    resetIdempotency();
  };

  const handleMethodChange = (nextMethod: PaymentMethodType) => {
    setMethod(nextMethod);
    resetIdempotency();
  };

  const handlePay = async () => {
    try {
      const result = await makePayment({
        loanId,
        amount,
        paymentMethod: method,
        operationNumber: operationNumber || undefined,
        voucherFileUri: method === 'TRANSFERENCIA_BANCARIA' ? voucherUri : undefined,
      });

      // FE-CP-12 / MOV-04: Si es pasarela alojada, abrir navegador seguro con deep link de retorno
      if (method === 'PASARELA_PAGO' && result.paymentUrl) {
        await WebBrowser.openAuthSessionAsync(
          result.paymentUrl,
          `prestamos://pay/return?loanId=${loanId}&paymentId=${result.id}`
        );
      }
    } catch {
      // error gestionado en el hook; conserva pendingIdempotencyKey para reintentar
    }
  };

  const handleOpenReceiptPdf = async (paymentId: string) => {
    await WebBrowser.openBrowserAsync(getPaymentReceiptPdfUrl(paymentId));
  };

  return (
    <ScrollView contentContainerStyle={styles.container}>
      <Card style={styles.card}>
        <Text style={styles.title}>Pagar Cuota (FE-CP-12)</Text>
        <Text style={styles.desc}>Préstamo #{loanId}</Text>

        {error ? (
          <View style={styles.errorBox}>
            <Text style={styles.errorText}>{error}</Text>
            {pendingIdempotencyKey ? (
              <Text style={styles.idempNote}>
                Clave de idempotencia activa ({pendingIdempotencyKey.slice(0, 8)}...): puedes reintentar sin riesgo de doble cobro (MOV-06).
              </Text>
            ) : null}
          </View>
        ) : null}

        <View style={styles.field}>
          <Text style={styles.label}>Monto a pagar (S/)</Text>
          <TextInput
            style={styles.input}
            keyboardType="numeric"
            value={amount}
            onChangeText={handleAmountChange}
          />
        </View>

        <PaymentMethodSelector selectedMethod={method} onSelect={handleMethodChange} />

        {/* FE-CP-12: QR Dinámico para Yape / Plin */}
        {method === 'YAPE' || method === 'PLIN' ? (
          <View style={styles.qrBox}>
            <Text style={styles.qrTitle}>QR Dinámico {method}</Text>
            <View style={styles.qrPlaceholder}>
              <Text style={styles.qrCodeText}>[ QR {method} · {formatMoney(amount)} ]</Text>
            </View>
            <Text style={styles.qrHint}>
              Escanea desde tu app {method}. La confirmación llega en tiempo real por webhook (FE-CP-14).
            </Text>
          </View>
        ) : null}

        {/* FE-CP-12: Pasarela alojada con tarjeta tokenizada */}
        {method === 'PASARELA_PAGO' ? (
          <View style={styles.gatewayBox}>
            <Text style={styles.gatewayText}>
              Los datos de tu tarjeta se ingresan únicamente en el checkout alojado certificado PCI-DSS. Al finalizar volverás automáticamente a la app (`prestamos://pay/return`, MOV-04).
            </Text>
          </View>
        ) : null}

        {/* FE-CP-12 / FE-CP-13: Reporte de transferencia con voucher */}
        {method === 'TRANSFERENCIA_BANCARIA' ? (
          <View style={styles.voucherSection}>
            <View style={styles.field}>
              <Text style={styles.label}>Número de Operación del Voucher</Text>
              <TextInput
                style={styles.input}
                placeholder="ej. 8921382"
                value={operationNumber}
                onChangeText={setOperationNumber}
              />
            </View>

            <View style={styles.field}>
              <Text style={styles.label}>Comprobante / Voucher de transferencia</Text>
              {voucherUri ? (
                <StatusBadge label="Voucher adjuntado" tone="success" />
              ) : (
                <Button
                  title="Adjuntar Foto del Voucher"
                  variant="outline"
                  onPress={() => setVoucherUri('file:///tmp/voucher-transferencia.jpg')}
                />
              )}
            </View>

            <View style={styles.validationWarning}>
              <Text style={styles.validationWarningText}>
                Nota (FE-CP-13): Los pagos con voucher nacen en estado &quot;En validación&quot; y nunca se registran como pagados hasta su conciliación bancaria.
              </Text>
            </View>
          </View>
        ) : null}

        <Button
          title={
            pendingIdempotencyKey
              ? `Reintentar Pago Seguro (${formatMoney(amount)})`
              : `Confirmar Pago (${formatMoney(amount)})`
          }
          onPress={handlePay}
          isLoading={isLoading}
          disabled={!amount || Number(amount) <= 0}
        />
      </Card>

      {/* FE-CP-13 / FE-CP-14: Estado del pago y Recibo PDF con QR de verificación */}
      {lastPayment ? (
        <Card style={styles.card}>
          <View style={styles.receiptHeader}>
            <Text style={styles.receiptTitle}>Comprobante #{lastPayment.id}</Text>
            <StatusBadge kind="payment" status={lastPayment.status} />
          </View>

          {lastPayment.status === 'PENDING_CONFIRMATION' ? (
            <View style={styles.pendingBox}>
              <Text style={styles.pendingTitle}>Pago en validación (FE-CP-13)</Text>
              <Text style={styles.pendingDesc}>
                Tu voucher fue recibido y está en proceso de conciliación. La cuota se marcará como pagada únicamente cuando el banco confirme la operación.
              </Text>
              <Button
                title="Consultar estado de confirmación"
                variant="outline"
                onPress={() => checkStatus(lastPayment.id)}
                isLoading={isLoading}
              />
            </View>
          ) : (
            <View style={styles.confirmedBox}>
              <Text style={styles.confirmedTitle}>Pago Confirmado (FE-CP-14)</Text>
              <Text style={styles.confirmedDesc}>
                Monto aplicado: {formatMoney(lastPayment.amount)}
              </Text>
              <View style={styles.verifyQrBox}>
                <Text style={styles.verifyQrText}>
                  [ QR DE VERIFICACIÓN · RECIBO #{lastPayment.id} ]
                </Text>
              </View>
              <Button
                title="Descargar Recibo PDF con QR"
                variant="outline"
                onPress={() => handleOpenReceiptPdf(lastPayment.id)}
              />
            </View>
          )}
        </Card>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: 16,
    gap: 16,
    backgroundColor: '#F9FAFB',
    flexGrow: 1,
  },
  card: {
    gap: 16,
  },
  title: {
    fontSize: 20,
    fontWeight: '700',
    color: '#111827',
  },
  desc: {
    fontSize: 13,
    color: '#6B7280',
    marginTop: -8,
  },
  field: {
    gap: 6,
  },
  label: {
    fontSize: 14,
    fontWeight: '500',
    color: '#374151',
  },
  input: {
    height: 48,
    borderWidth: 1,
    borderColor: '#D1D5DB',
    borderRadius: 8,
    paddingHorizontal: 12,
    fontSize: 16,
    backgroundColor: '#F9FAFB',
    color: '#111827',
  },
  qrBox: {
    backgroundColor: '#F0F9FF',
    padding: 14,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: '#BAE6FD',
    alignItems: 'center',
    gap: 8,
  },
  qrTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#0369A1',
  },
  qrPlaceholder: {
    paddingVertical: 24,
    paddingHorizontal: 16,
    backgroundColor: '#FFFFFF',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#E0F2FE',
  },
  qrCodeText: {
    fontSize: 13,
    fontWeight: '700',
    color: '#0284C7',
  },
  qrHint: {
    fontSize: 12,
    color: '#0369A1',
    textAlign: 'center',
  },
  gatewayBox: {
    backgroundColor: '#F3F4F6',
    padding: 12,
    borderRadius: 8,
  },
  gatewayText: {
    fontSize: 12,
    color: '#374151',
    lineHeight: 18,
  },
  voucherSection: {
    gap: 12,
  },
  validationWarning: {
    backgroundColor: '#FFFBEB',
    borderColor: '#FDE68A',
    borderWidth: 1,
    borderRadius: 8,
    padding: 10,
  },
  validationWarningText: {
    fontSize: 12,
    color: '#92400E',
    lineHeight: 17,
  },
  errorBox: {
    backgroundColor: '#FEE2E2',
    padding: 10,
    borderRadius: 8,
    gap: 4,
  },
  errorText: {
    color: '#B91C1C',
    fontSize: 13,
    fontWeight: '600',
  },
  idempNote: {
    color: '#7F1D1D',
    fontSize: 11,
  },
  receiptHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  receiptTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#111827',
  },
  pendingBox: {
    gap: 10,
    backgroundColor: '#F9FAFB',
    padding: 12,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#E5E7EB',
  },
  pendingTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#374151',
  },
  pendingDesc: {
    fontSize: 13,
    color: '#4B5563',
    lineHeight: 18,
  },
  confirmedBox: {
    gap: 10,
    backgroundColor: '#ECFDF5',
    padding: 12,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#A7F3D0',
  },
  confirmedTitle: {
    fontSize: 15,
    fontWeight: '700',
    color: '#065F46',
  },
  confirmedDesc: {
    fontSize: 13,
    color: '#047857',
  },
  verifyQrBox: {
    padding: 12,
    backgroundColor: '#FFFFFF',
    borderRadius: 6,
    alignItems: 'center',
  },
  verifyQrText: {
    fontSize: 12,
    fontWeight: '700',
    color: '#065F46',
  },
});
