import React, { useState } from 'react';
import { StyleSheet, View, Text, TextInput, ScrollView } from 'react-native';
import { useRouter } from 'expo-router';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { useAuth, registerSchema } from '@/features/auth';

export default function RegisterScreen() {
  const router = useRouter();
  const { register, isLoading, error, fieldErrors } = useAuth();

  const [docType, setDocType] = useState<'DNI' | 'CE'>('DNI');
  const [docNumber, setDocNumber] = useState('');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [localErrors, setLocalErrors] = useState<Record<string, string>>({});

  const handleRegister = async () => {
    const parsed = registerSchema.safeParse({
      docType,
      docNumber,
      fullName,
      email,
      phone,
    });

    if (!parsed.success) {
      const errs: Record<string, string> = {};
      for (const issue of parsed.error.issues) {
        const key = String(issue.path[0] ?? 'general');
        errs[key] = issue.message;
      }
      setLocalErrors(errs);
      return;
    }

    setLocalErrors({});
    try {
      await register(parsed.data);
      router.push({
        pathname: '/(auth)/otp',
        params: { identifier: phone, purpose: 'REGISTER' },
      });
    } catch {
      // error gestionado en el hook
    }
  };

  const mergedErrors = { ...localErrors, ...fieldErrors };

  return (
    <ScrollView contentContainerStyle={styles.container}>
      <Card style={styles.card}>
        <Text style={styles.title}>Crea tu cuenta de cliente</Text>
        <Text style={styles.desc}>
          Ingresa tus datos personales para acceder a préstamos rápidos y seguros.
        </Text>

        {error ? (
          <View style={styles.errorBox}>
            <Text style={styles.errorText}>{error}</Text>
          </View>
        ) : null}

        <View style={styles.docTypeRow}>
          <Button
            title="DNI (8 díg.)"
            variant={docType === 'DNI' ? 'primary' : 'outline'}
            onPress={() => {
              setDocType('DNI');
              setDocNumber('');
            }}
            style={styles.halfBtn}
          />
          <Button
            title="Carné Ext. (9 díg.)"
            variant={docType === 'CE' ? 'primary' : 'outline'}
            onPress={() => {
              setDocType('CE');
              setDocNumber('');
            }}
            style={styles.halfBtn}
          />
        </View>

        <View style={styles.field}>
          <Text style={styles.label}>
            Número de {docType} ({docType === 'DNI' ? '8' : '9'} dígitos)
          </Text>
          <TextInput
            style={styles.input}
            keyboardType="number-pad"
            maxLength={docType === 'DNI' ? 8 : 9}
            placeholder={docType === 'DNI' ? '12345678' : '001234567'}
            value={docNumber}
            onChangeText={setDocNumber}
          />
          {mergedErrors.docNumber ? (
            <Text style={styles.fieldError}>{mergedErrors.docNumber}</Text>
          ) : null}
        </View>

        <View style={styles.field}>
          <Text style={styles.label}>Nombres y Apellidos</Text>
          <TextInput
            style={styles.input}
            placeholder="Juan Pérez"
            value={fullName}
            onChangeText={setFullName}
          />
          {mergedErrors.fullName ? (
            <Text style={styles.fieldError}>{mergedErrors.fullName}</Text>
          ) : null}
        </View>

        <View style={styles.field}>
          <Text style={styles.label}>Correo Electrónico</Text>
          <TextInput
            style={styles.input}
            keyboardType="email-address"
            autoCapitalize="none"
            placeholder="juan@correo.com"
            value={email}
            onChangeText={setEmail}
          />
          {mergedErrors.email ? (
            <Text style={styles.fieldError}>{mergedErrors.email}</Text>
          ) : null}
        </View>

        <View style={styles.field}>
          <Text style={styles.label}>Celular (9 dígitos)</Text>
          <TextInput
            style={styles.input}
            keyboardType="number-pad"
            maxLength={9}
            placeholder="987654321"
            value={phone}
            onChangeText={setPhone}
          />
          {mergedErrors.phone ? (
            <Text style={styles.fieldError}>{mergedErrors.phone}</Text>
          ) : null}
        </View>

        <Button
          title="Continuar con Verificación OTP"
          onPress={handleRegister}
          isLoading={isLoading}
        />
      </Card>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: 20,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#F9FAFB',
    flexGrow: 1,
  },
  card: {
    width: '100%',
    maxWidth: 480,
    gap: 14,
  },
  title: {
    fontSize: 20,
    fontWeight: '700',
    color: '#111827',
  },
  desc: {
    fontSize: 14,
    color: '#6B7280',
    lineHeight: 20,
  },
  docTypeRow: {
    flexDirection: 'row',
    gap: 10,
  },
  halfBtn: {
    flex: 1,
  },
  field: {
    gap: 4,
  },
  label: {
    fontSize: 13,
    fontWeight: '500',
    color: '#374151',
  },
  input: {
    height: 46,
    borderWidth: 1,
    borderColor: '#D1D5DB',
    borderRadius: 8,
    paddingHorizontal: 12,
    fontSize: 15,
    backgroundColor: '#F9FAFB',
    color: '#111827',
  },
  fieldError: {
    fontSize: 12,
    color: '#DC2626',
  },
  errorBox: {
    backgroundColor: '#FEE2E2',
    padding: 10,
    borderRadius: 8,
  },
  errorText: {
    color: '#B91C1C',
    fontSize: 13,
  },
});
