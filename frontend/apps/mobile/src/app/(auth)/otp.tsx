import React, { useState } from 'react';
import { StyleSheet, View, Text, TextInput } from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { useAuth } from '@/features/auth';

export default function OtpScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ identifier?: string; purpose?: string }>();
  const identifier = params.identifier ?? '';
  const purpose = (params.purpose as 'LOGIN' | 'REGISTER') ?? 'LOGIN';

  const { verifyOtp, isLoading, error } = useAuth();
  const [code, setCode] = useState('');

  const handleVerify = async () => {
    try {
      await verifyOtp({
        identifier,
        code,
        purpose,
      });
      router.replace('/(app)');
    } catch {
      // error en el hook
    }
  };

  return (
    <View style={styles.container}>
      <Card style={styles.card}>
        <Text style={styles.title}>Código de Verificación</Text>
        <Text style={styles.desc}>
          Ingresa el código OTP de 6 dígitos enviado por SMS o WhatsApp al número/correo{' '}
          <Text style={styles.highlight}>{identifier}</Text>.
        </Text>

        {error ? (
          <View style={styles.errorBox}>
            <Text style={styles.errorText}>{error}</Text>
          </View>
        ) : null}

        {/* FE-CP-01: Teclado numérico y autollenado de OTP desde SMS */}
        <TextInput
          style={styles.otpInput}
          keyboardType="number-pad"
          textContentType="oneTimeCode"
          autoComplete="sms-otp"
          maxLength={6}
          placeholder="000000"
          value={code}
          onChangeText={(txt) => setCode(txt.replace(/\D/g, ''))}
          textAlign="center"
          autoFocus
        />

        <Button
          title="Verificar y Entrar"
          onPress={handleVerify}
          isLoading={isLoading}
          disabled={code.length !== 6}
        />
      </Card>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 20,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#F9FAFB',
  },
  card: {
    width: '100%',
    maxWidth: 420,
    gap: 16,
    alignItems: 'center',
  },
  title: {
    fontSize: 20,
    fontWeight: '700',
    color: '#111827',
  },
  desc: {
    fontSize: 14,
    color: '#6B7280',
    textAlign: 'center',
    lineHeight: 20,
  },
  highlight: {
    fontWeight: '600',
    color: '#111827',
  },
  otpInput: {
    width: 200,
    height: 56,
    borderWidth: 2,
    borderColor: '#208AEF',
    borderRadius: 12,
    fontSize: 28,
    fontWeight: '700',
    letterSpacing: 8,
    color: '#111827',
    backgroundColor: '#F0F9FF',
    textAlign: 'center',
  },
  errorBox: {
    width: '100%',
    backgroundColor: '#FEE2E2',
    padding: 10,
    borderRadius: 8,
  },
  errorText: {
    color: '#B91C1C',
    fontSize: 13,
    textAlign: 'center',
  },
});
