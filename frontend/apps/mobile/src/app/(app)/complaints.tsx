import React, { useState } from 'react';
import { StyleSheet, ScrollView, Alert, View, Text } from 'react-native';
import { useRouter } from 'expo-router';
import { ComplaintForm, useComplaints, complaintSchema } from '@/features/complaints';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';

export default function ComplaintsScreen() {
  const router = useRouter();
  const { complaints, submit, isLoading } = useComplaints();
  const [validationError, setValidationError] = useState<string | null>(null);

  const handleSubmit = async (payload: Parameters<typeof submit>[0]) => {
    const parsed = complaintSchema.safeParse(payload);
    if (!parsed.success) {
      setValidationError(parsed.error.issues[0]?.message ?? 'Revisa los datos del formulario');
      return;
    }

    setValidationError(null);
    try {
      const result = await submit(parsed.data);
      Alert.alert(
        'Reclamación Registrada',
        `Tu código de seguimiento es: ${result.filingNumber}. Te responderemos en un plazo máximo de 15 días hábiles.`,
        [{ text: 'Entendido', onPress: () => router.back() }]
      );
    } catch {
      // error en el hook
    }
  };

  return (
    <ScrollView contentContainerStyle={styles.container}>
      {validationError ? (
        <View style={styles.errorBox}>
          <Text style={styles.errorText}>{validationError}</Text>
        </View>
      ) : null}

      <ComplaintForm onSubmit={handleSubmit} isLoading={isLoading} />

      {complaints.length > 0 ? (
        <Card style={styles.historyCard}>
          <Text style={styles.historyTitle}>Mis Hojas de Reclamación (FE-CP-21)</Text>
          {complaints.map((c) => (
            <View key={c.id} style={styles.historyRow}>
              <View>
                <Text style={styles.filingNum}>{c.filingNumber}</Text>
                <Text style={styles.createdAt}>{c.createdAt}</Text>
              </View>
              <StatusBadge
                label={c.status === 'RESOLVED' ? 'Resuelto' : 'En revisión'}
                tone={c.status === 'RESOLVED' ? 'success' : 'info'}
              />
            </View>
          ))}
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
  historyCard: {
    gap: 10,
  },
  historyTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#111827',
  },
  historyRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 8,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#E5E7EB',
  },
  filingNum: {
    fontSize: 14,
    fontWeight: '700',
    color: '#111827',
  },
  createdAt: {
    fontSize: 12,
    color: '#6B7280',
  },
});
