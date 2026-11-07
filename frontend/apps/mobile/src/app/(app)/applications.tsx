import React, { useState } from 'react';
import { StyleSheet, View, Text, TextInput, FlatList, RefreshControl } from 'react-native';
import {
  useApplications,
  SimulatorCard,
  ApplicationTimeline,
  applicationSchema,
} from '@/features/applications';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatMoney } from '@prestamos/utils';

export default function ApplicationsScreen() {
  const {
    products,
    applications,
    simulation,
    isLoading,
    fieldErrors,
    simulate,
    create,
    refresh,
  } = useApplications();

  // FE-CP-07: Formulario de solicitud con datos laborales y adjunto de ingresos
  const [requestedAmount, setRequestedAmount] = useState('5000');
  const [termMonths, setTermMonths] = useState(12);
  const [monthlyIncome, setMonthlyIncome] = useState('3500');
  const [occupation, setOccupation] = useState('');
  const [employerName, setEmployerName] = useState('');
  const [incomeDocumentUri, setIncomeDocumentUri] = useState<string | undefined>(undefined);
  const [localError, setLocalError] = useState<string | null>(null);

  const handleSimulate = (amount: string, term: number) => {
    setRequestedAmount(amount);
    setTermMonths(term);
    void simulate({ amount, termMonths: term, productId: products[0]?.id });
  };

  const handleCreateApplication = async () => {
    const productId = products[0]?.id ?? 'default-personal';
    const parsed = applicationSchema.safeParse({
      productId,
      requestedAmount,
      termMonths,
      monthlyIncome,
      occupation,
      employerName: employerName || undefined,
    });

    if (!parsed.success) {
      setLocalError(parsed.error.issues[0]?.message ?? 'Completa tus datos laborales');
      return;
    }

    setLocalError(null);
    await create({
      ...parsed.data,
      incomeDocumentUri,
    });
    setOccupation('');
    setEmployerName('');
    setIncomeDocumentUri(undefined);
  };

  return (
    <View style={styles.container}>
      <FlatList
        data={applications}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.list}
        refreshControl={<RefreshControl refreshing={isLoading} onRefresh={refresh} />}
        ListHeaderComponent={
          <View style={styles.header}>
            {/* FE-CP-06: Simulador interactivo */}
            <SimulatorCard
              onSimulate={handleSimulate}
              result={simulation}
              isLoading={isLoading}
            />

            {/* FE-CP-07: Solicitud con datos laborales y adjunto de ingresos */}
            <Card style={styles.formCard}>
              <View style={styles.titleRow}>
                <Text style={styles.sectionTitle}>Solicitar Préstamo</Text>
                <StatusBadge label="FE-CP-07" tone="info" />
              </View>
              <Text style={styles.subtitle}>
                Monto: {formatMoney(requestedAmount)} · Plazo: {termMonths} meses
              </Text>

              <View style={styles.field}>
                <Text style={styles.label}>Ingreso Mensual Neto (S/)</Text>
                <TextInput
                  style={styles.input}
                  keyboardType="numeric"
                  value={monthlyIncome}
                  onChangeText={setMonthlyIncome}
                  placeholder="3500"
                />
              </View>

              <View style={styles.field}>
                <Text style={styles.label}>Ocupación / Cargo</Text>
                <TextInput
                  style={styles.input}
                  value={occupation}
                  onChangeText={setOccupation}
                  placeholder="Ej. Analista, Independiente..."
                />
              </View>

              <View style={styles.field}>
                <Text style={styles.label}>Empresa / Empleador (Opcional)</Text>
                <TextInput
                  style={styles.input}
                  value={employerName}
                  onChangeText={setEmployerName}
                  placeholder="Nombre de empresa"
                />
              </View>

              <View style={styles.attachRow}>
                <Text style={styles.label}>Sustento de ingresos (Boleta / Recibo):</Text>
                {incomeDocumentUri ? (
                  <StatusBadge label="Adjunto listo" tone="success" />
                ) : (
                  <Button
                    title="Adjuntar Boleta / Recibo"
                    variant="outline"
                    onPress={() => setIncomeDocumentUri('file:///tmp/boleta-ingresos.pdf')}
                  />
                )}
              </View>

              {localError || fieldErrors.occupation ? (
                <Text style={styles.errorText}>{localError ?? fieldErrors.occupation}</Text>
              ) : null}

              <Button
                title="Enviar Solicitud"
                onPress={handleCreateApplication}
                isLoading={isLoading}
              />
            </Card>

            <Text style={styles.sectionTitle}>Seguimiento de Solicitudes (FE-CP-08)</Text>
          </View>
        }
        renderItem={({ item }) => (
          <Card style={styles.itemCard}>
            <View style={styles.itemHeader}>
              <Text style={styles.itemNumber}>Solicitud #{item.applicationNumber}</Text>
              <StatusBadge kind="application" status={item.status} />
            </View>
            <Text style={styles.itemAmount}>
              Monto solicitado: {formatMoney(item.requestedAmount)} · {item.termMonths} meses
            </Text>
            {item.approvedAmount ? (
              <Text style={styles.approvedAmount}>
                Monto aprobado: {formatMoney(item.approvedAmount)}
              </Text>
            ) : null}

            {/* FE-CP-08: Línea de tiempo (Stepper vertical) */}
            <ApplicationTimeline currentStatus={item.status} />
          </Card>
        )}
        ListEmptyComponent={
          !isLoading ? (
            <Text style={styles.emptyText}>No tienes solicitudes registradas aún.</Text>
          ) : undefined
        }
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F9FAFB',
  },
  list: {
    padding: 16,
    gap: 12,
  },
  header: {
    gap: 16,
    marginBottom: 8,
  },
  formCard: {
    gap: 12,
  },
  titleRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '700',
    color: '#111827',
  },
  subtitle: {
    fontSize: 13,
    color: '#4B5563',
    fontWeight: '600',
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
  attachRow: {
    gap: 6,
  },
  errorText: {
    fontSize: 12,
    color: '#DC2626',
  },
  itemCard: {
    gap: 8,
  },
  itemHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  itemNumber: {
    fontSize: 15,
    fontWeight: '700',
    color: '#111827',
  },
  itemAmount: {
    fontSize: 14,
    color: '#4B5563',
  },
  approvedAmount: {
    fontSize: 14,
    fontWeight: '600',
    color: '#059669',
  },
  emptyText: {
    textAlign: 'center',
    color: '#6B7280',
    marginTop: 12,
    fontSize: 14,
  },
});
