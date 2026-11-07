import React, { useState } from 'react';
import {
  StyleSheet,
  Text,
  View,
  ScrollView,
  TextInput,
  type NativeScrollEvent,
  type NativeSyntheticEvent,
} from 'react-native';
import * as WebBrowser from 'expo-web-browser';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/components/ui/status-badge';
import { getContractPdfUrl } from '../api';
import type { ContractDocument } from '../types';

interface ContractCardProps {
  contract: ContractDocument;
  onRequestOtp: () => Promise<void>;
  onSign: (otpCode: string) => Promise<unknown>;
  isLoading?: boolean;
}

export function ContractCard({
  contract,
  onRequestOtp,
  onSign,
  isLoading = false,
}: ContractCardProps) {
  const [hasScrolledToEnd, setHasScrolledToEnd] = useState(false);
  const [otpRequested, setOtpRequested] = useState(false);
  const [otpCode, setOtpCode] = useState('');

  const handleScroll = (e: NativeSyntheticEvent<NativeScrollEvent>) => {
    const { layoutMeasurement, contentOffset, contentSize } = e.nativeEvent;
    const isEnd = layoutMeasurement.height + contentOffset.y >= contentSize.height - 20;
    if (isEnd && !hasScrolledToEnd) {
      setHasScrolledToEnd(true);
    }
  };

  const handleOpenPdf = async () => {
    const url = contract.pdfUrl || getContractPdfUrl(contract.loanId);
    await WebBrowser.openBrowserAsync(url);
  };

  const handleAcceptPress = async () => {
    await onRequestOtp();
    setOtpRequested(true);
  };

  return (
    <Card style={styles.card}>
      <View style={styles.header}>
        <Text style={styles.title}>Contrato y Hoja Resumen #{contract.contractNumber}</Text>
        <StatusBadge
          label={contract.isSigned ? 'Firmado' : 'Pendiente de firma'}
          tone={contract.isSigned ? 'success' : 'warning'}
        />
      </View>

      {!contract.isSigned ? (
        <>
          <Text style={styles.desc}>
            Desplázate hasta el final del contrato y la Hoja Resumen informativa para habilitar la aceptación con OTP (FE-CP-09).
          </Text>

          <ScrollView
            style={styles.contractReader}
            nestedScrollEnabled
            onScroll={handleScroll}
            scrollEventThrottle={16}>
            <Text style={styles.contractText}>
              HOJA RESUMEN Y CONTRATO DE PRÉSTAMO DINERARIO{'\n\n'}
              1. PARTES: El Prestatario acepta las condiciones financieras detalladas en la Hoja Resumen (TEA, TCEA, cronograma de pagos y penalidades por mora).{'\n\n'}
              2. DESEMBOLSO: El monto aprobado será transferido únicamente a la cuenta bancaria verificada de titularidad del cliente tras la firma electrónica con código OTP.{'\n\n'}
              3. PREPAGO (Ley N° 29571 Art. 85): El cliente tiene derecho a efectuar pagos anticipados totales o parciales en cualquier momento, con reducción de intereses al día de pago y sin penalidad alguna.{'\n\n'}
              4. CONFORMIDAD: Al llegar al final de este documento y validar su clave OTP de 6 dígitos, el cliente suscribe el presente contrato con plena validez legal.
            </Text>
          </ScrollView>

          {!hasScrolledToEnd ? (
            <Button
              title="Marcar lectura completa hasta el final"
              variant="secondary"
              onPress={() => setHasScrolledToEnd(true)}
            />
          ) : null}

          {!otpRequested ? (
            <Button
              title="Aceptar y Solicitar OTP de Firma"
              onPress={handleAcceptPress}
              disabled={!hasScrolledToEnd}
              isLoading={isLoading}
            />
          ) : (
            <View style={styles.otpSection}>
              <Text style={styles.otpLabel}>
                Ingresa el OTP de 6 dígitos enviado para firmar el contrato:
              </Text>
              <TextInput
                style={styles.otpInput}
                keyboardType="number-pad"
                textContentType="oneTimeCode"
                autoComplete="sms-otp"
                maxLength={6}
                placeholder="000000"
                value={otpCode}
                onChangeText={(t) => setOtpCode(t.replace(/\D/g, ''))}
              />
              <Button
                title="Firmar Contrato Digitalmente"
                onPress={() => onSign(otpCode)}
                disabled={otpCode.length !== 6}
                isLoading={isLoading}
              />
            </View>
          )}
        </>
      ) : (
        <Text style={styles.signedInfo}>Firmado digitalmente el {contract.signedAt}</Text>
      )}

      <Button
        title="Descargar / Ver Contrato PDF"
        variant="outline"
        onPress={handleOpenPdf}
      />
    </Card>
  );
}

const styles = StyleSheet.create({
  card: {
    gap: 12,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: 8,
  },
  title: {
    fontSize: 16,
    fontWeight: '700',
    color: '#111827',
    flex: 1,
  },
  desc: {
    fontSize: 13,
    color: '#4B5563',
    lineHeight: 18,
  },
  contractReader: {
    maxHeight: 150,
    backgroundColor: '#F9FAFB',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#E5E7EB',
    padding: 12,
  },
  contractText: {
    fontSize: 12,
    color: '#374151',
    lineHeight: 18,
  },
  otpSection: {
    gap: 10,
    backgroundColor: '#F0F9FF',
    padding: 12,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#BAE6FD',
  },
  otpLabel: {
    fontSize: 13,
    fontWeight: '600',
    color: '#0369A1',
  },
  otpInput: {
    height: 46,
    borderWidth: 1.5,
    borderColor: '#0284C7',
    borderRadius: 8,
    backgroundColor: '#FFFFFF',
    fontSize: 20,
    fontWeight: '700',
    textAlign: 'center',
    letterSpacing: 6,
    color: '#111827',
  },
  signedInfo: {
    fontSize: 13,
    color: '#059669',
    fontWeight: '600',
  },
});
