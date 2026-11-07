import React, { useState } from 'react';
import { StyleSheet, Text, TextInput, View } from 'react-native';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';

interface LoginCardProps {
  onSubmit: (identifier: string) => void;
  isLoading?: boolean;
  errorMessage?: string | null;
}

export function LoginCard({ onSubmit, isLoading = false, errorMessage }: LoginCardProps) {
  const [identifier, setIdentifier] = useState('cliente@correo.pe');

  const handleSubmit = () => {
    if (identifier.trim().length > 0) {
      onSubmit(identifier.trim());
    }
  };

  return (
    <Card style={styles.card}>
      <Text style={styles.title}>Portal del Cliente</Text>
      <Text style={styles.subtitle}>Ingresa con tu DNI, celular o correo electrónico</Text>

      {errorMessage ? (
        <View style={styles.errorBox}>
          <Text style={styles.errorText}>{errorMessage}</Text>
        </View>
      ) : null}

      <View style={styles.field}>
        <Text style={styles.label}>Correo, Celular o DNI</Text>
        <TextInput
          style={styles.input}
          placeholder="ej. cliente@correo.pe, 987000000 o 41234567"
          placeholderTextColor="#9CA3AF"
          value={identifier}
          onChangeText={setIdentifier}
          autoCapitalize="none"
          keyboardType="email-address"
          editable={!isLoading}
        />
        <Text style={styles.hint}>
          Usuario semilla (@prestamos/mocks): cliente@correo.pe · DNI 41234567 · Cel. 987000000
        </Text>
      </View>

      <Button
        title="Continuar"
        onPress={handleSubmit}
        isLoading={isLoading}
        disabled={identifier.trim().length === 0}
      />
    </Card>
  );
}

const styles = StyleSheet.create({
  card: {
    width: '100%',
    gap: 16,
  },
  title: {
    fontSize: 22,
    fontWeight: '700',
    color: '#111827',
  },
  subtitle: {
    fontSize: 14,
    color: '#6B7280',
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
    color: '#111827',
    backgroundColor: '#F9FAFB',
  },
  hint: {
    fontSize: 11,
    color: '#6B7280',
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
