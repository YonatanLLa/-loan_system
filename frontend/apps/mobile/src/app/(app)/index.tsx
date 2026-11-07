import React from 'react';
import { StyleSheet, View, Text, FlatList, RefreshControl } from 'react-native';
import { useRouter } from 'expo-router';
import { useLoans, LoanCard } from '@/features/loans';
import { Button } from '@/components/ui/button';

export default function HomeScreen() {
  const router = useRouter();
  const { loans, isLoading, refresh } = useLoans();

  return (
    <View style={styles.container}>
      <FlatList
        data={loans}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.list}
        refreshControl={<RefreshControl refreshing={isLoading} onRefresh={refresh} />}
        ListHeaderComponent={
          <View style={styles.header}>
            <Text style={styles.welcomeText}>Resumen Financiero</Text>
            <Text style={styles.subtitle}>
              Gestiona tus préstamos, consulta cuotas y realiza pagos en línea.
            </Text>
          </View>
        }
        renderItem={({ item }) => (
          <LoanCard
            loan={item}
            onPress={() => router.push({ pathname: '/(app)/loans/[id]', params: { id: item.id } })}
          />
        )}
        ListEmptyComponent={
          !isLoading ? (
            <View style={styles.emptyBox}>
              <Text style={styles.emptyTitle}>No tienes préstamos vigentes</Text>
              <Text style={styles.emptyDesc}>
                Simula y solicita un préstamo al instante con tasas preferenciales.
              </Text>
              <Button
                title="Simular Préstamo"
                onPress={() => router.push('/(app)/applications')}
                style={styles.emptyBtn}
              />
            </View>
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
    gap: 16,
  },
  header: {
    gap: 4,
    marginBottom: 8,
  },
  welcomeText: {
    fontSize: 22,
    fontWeight: '700',
    color: '#111827',
  },
  subtitle: {
    fontSize: 14,
    color: '#6B7280',
  },
  emptyBox: {
    padding: 24,
    alignItems: 'center',
    gap: 12,
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    borderWidth: 1,
    borderColor: '#E5E7EB',
    marginTop: 20,
  },
  emptyTitle: {
    fontSize: 17,
    fontWeight: '600',
    color: '#1F2937',
  },
  emptyDesc: {
    fontSize: 14,
    color: '#6B7280',
    textAlign: 'center',
    lineHeight: 20,
  },
  emptyBtn: {
    marginTop: 8,
    width: '100%',
  },
});
