import React, { useState } from 'react';
import { StyleSheet, Text, TextInput, View } from 'react-native';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import type { ComplaintType, CreateComplaintPayload } from '../types';

interface ComplaintFormProps {
  onSubmit: (payload: CreateComplaintPayload) => void;
  isLoading?: boolean;
}

export function ComplaintForm({ onSubmit, isLoading = false }: ComplaintFormProps) {
  const [type, setType] = useState<ComplaintType>('RECLAMO');
  const [fullName, setFullName] = useState('');
  const [docNumber, setDocNumber] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [detail, setDetail] = useState('');
  const [requestedSolution, setRequestedSolution] = useState('');

  const handleSubmit = () => {
    onSubmit({
      type,
      fullName,
      docType: 'DNI',
      docNumber,
      email,
      phone,
      detail,
      requestedSolution,
    });
  };

  return (
    <Card style={styles.card}>
      <Text style={styles.title}>Libro de Reclamaciones Virtual</Text>
      <Text style={styles.desc}>Conforme a lo establecido en el Código de Protección al Consumidor.</Text>

      <View style={styles.typeSelector}>
        <Button
          title="Reclamo"
          variant={type === 'RECLAMO' ? 'primary' : 'outline'}
          onPress={() => setType('RECLAMO')}
          style={styles.halfBtn}
        />
        <Button
          title="Queja"
          variant={type === 'QUEJA' ? 'primary' : 'outline'}
          onPress={() => setType('QUEJA')}
          style={styles.halfBtn}
        />
      </View>

      <View style={styles.field}>
        <Text style={styles.label}>Nombres y Apellidos</Text>
        <TextInput
          style={styles.input}
          value={fullName}
          onChangeText={setFullName}
          placeholder="Juan Pérez"
        />
      </View>

      <View style={styles.field}>
        <Text style={styles.label}>Número de DNI</Text>
        <TextInput
          style={styles.input}
          keyboardType="numeric"
          maxLength={8}
          value={docNumber}
          onChangeText={setDocNumber}
          placeholder="12345678"
        />
      </View>

      <View style={styles.field}>
        <Text style={styles.label}>Correo Electrónico</Text>
        <TextInput
          style={styles.input}
          keyboardType="email-address"
          autoCapitalize="none"
          value={email}
          onChangeText={setEmail}
          placeholder="juan@correo.com"
        />
      </View>

      <View style={styles.field}>
        <Text style={styles.label}>Celular</Text>
        <TextInput
          style={styles.input}
          keyboardType="numeric"
          maxLength={9}
          value={phone}
          onChangeText={setPhone}
          placeholder="987654321"
        />
      </View>

      <View style={styles.field}>
        <Text style={styles.label}>Detalle del reclamo o queja</Text>
        <TextInput
          style={[styles.input, styles.textArea]}
          multiline
          numberOfLines={4}
          value={detail}
          onChangeText={setDetail}
          placeholder="Describe los hechos..."
        />
      </View>

      <View style={styles.field}>
        <Text style={styles.label}>Pedido o solución solicitada</Text>
        <TextInput
          style={[styles.input, styles.textArea]}
          multiline
          numberOfLines={3}
          value={requestedSolution}
          onChangeText={setRequestedSolution}
          placeholder="Qué acción esperas..."
        />
      </View>

      <Button
        title="Enviar Hoja de Reclamación"
        onPress={handleSubmit}
        isLoading={isLoading}
        disabled={detail.length < 10 || fullName.length === 0}
      />
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
  desc: {
    fontSize: 13,
    color: '#6B7280',
    lineHeight: 18,
  },
  typeSelector: {
    flexDirection: 'row',
    gap: 12,
    marginVertical: 4,
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
    height: 44,
    borderWidth: 1,
    borderColor: '#D1D5DB',
    borderRadius: 8,
    paddingHorizontal: 12,
    fontSize: 15,
    backgroundColor: '#F9FAFB',
    color: '#111827',
  },
  textArea: {
    height: 80,
    textAlignVertical: 'top',
    paddingVertical: 8,
  },
});
