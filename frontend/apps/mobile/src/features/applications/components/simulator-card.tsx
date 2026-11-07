import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, TextInput, View, Pressable } from 'react-native';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { formatMoney } from '@prestamos/utils';
import type { SimulationResult } from '../types';

interface SimulatorCardProps {
  onSimulate: (amount: string, termMonths: number) => void;
  result?: SimulationResult | null;
  isLoading?: boolean;
}

export function SimulatorCard({ onSimulate, result, isLoading = false }: SimulatorCardProps) {
  const [amount, setAmount] = useState('5000');
  const [term, setTerm] = useState(12);

  // FE-CP-06: Recálculo con debounce al ajustar controles interactivos
  useEffect(() => {
    const numAmount = Number(amount);
    if (!Number.isFinite(numAmount) || numAmount <= 0 || term < 1) return;

    const timer = setTimeout(() => {
      onSimulate(amount, term);
    }, 350);

    return () => clearTimeout(timer);
  }, [amount, term, onSimulate]);

  const adjustAmount = (delta: number) => {
    const next = Math.max(500, Math.min(50000, (Number(amount) || 0) + delta));
    setAmount(String(next));
  };

  const adjustTerm = (delta: number) => {
    const next = Math.max(1, Math.min(60, term + delta));
    setTerm(next);
  };

  return (
    <Card style={styles.card}>
      <Text style={styles.title}>Simulador de Préstamos (FE-CP-06)</Text>

      <View style={styles.field}>
        <Text style={styles.label}>Monto solicitado (S/)</Text>
        <View style={styles.stepperRow}>
          <Pressable style={styles.stepBtn} onPress={() => adjustAmount(-500)}>
            <Text style={styles.stepBtnText}>- S/ 500</Text>
          </Pressable>
          <TextInput
            style={styles.input}
            keyboardType="numeric"
            value={amount}
            onChangeText={setAmount}
            placeholder="5000"
          />
          <Pressable style={styles.stepBtn} onPress={() => adjustAmount(500)}>
            <Text style={styles.stepBtnText}>+ S/ 500</Text>
          </Pressable>
        </View>
      </View>

      <View style={styles.field}>
        <Text style={styles.label}>Plazo: {term} meses</Text>
        <View style={styles.stepperRow}>
          <Pressable style={styles.stepBtn} onPress={() => adjustTerm(-3)}>
            <Text style={styles.stepBtnText}>- 3 m</Text>
          </Pressable>
          <View style={styles.termChips}>
            {[6, 12, 24, 36].map((m) => (
              <Pressable
                key={m}
                onPress={() => setTerm(m)}
                style={[styles.chip, term === m && styles.chipActive]}>
                <Text style={[styles.chipText, term === m && styles.chipTextActive]}>{m}m</Text>
              </Pressable>
            ))}
          </View>
          <Pressable style={styles.stepBtn} onPress={() => adjustTerm(3)}>
            <Text style={styles.stepBtnText}>+ 3 m</Text>
          </Pressable>
        </View>
      </View>

      <Button
        title="Recalcular Cuota"
        onPress={() => onSimulate(amount, term)}
        isLoading={isLoading}
      />

      {result ? (
        <View style={styles.resultBox}>
          <Text style={styles.resultLabel}>Cuota mensual estimada:</Text>
          <Text style={styles.resultValue}>{formatMoney(result.monthlyInstallment)}</Text>
          <View style={styles.ratesRow}>
            <Text style={styles.resultDetail}>TEA: {result.tea}%</Text>
            <Text style={styles.resultDetail}>TCEA: {result.tcea}%</Text>
          </View>
          <Text style={styles.resultDetail}>
            Costo total ({result.installmentsCount} cuotas): {formatMoney(result.totalCost)}
          </Text>
        </View>
      ) : null}
    </Card>
  );
}

const styles = StyleSheet.create({
  card: {
    gap: 14,
  },
  title: {
    fontSize: 18,
    fontWeight: '700',
    color: '#111827',
  },
  field: {
    gap: 6,
  },
  label: {
    fontSize: 14,
    color: '#374151',
    fontWeight: '500',
  },
  stepperRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  stepBtn: {
    paddingHorizontal: 10,
    paddingVertical: 10,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#D1D5DB',
    backgroundColor: '#F3F4F6',
  },
  stepBtnText: {
    fontSize: 12,
    fontWeight: '600',
    color: '#1F2937',
  },
  input: {
    flex: 1,
    height: 44,
    borderWidth: 1,
    borderColor: '#D1D5DB',
    borderRadius: 8,
    paddingHorizontal: 12,
    fontSize: 16,
    color: '#111827',
    backgroundColor: '#F9FAFB',
    textAlign: 'center',
    fontWeight: '700',
  },
  termChips: {
    flex: 1,
    flexDirection: 'row',
    justifyContent: 'space-around',
    gap: 4,
  },
  chip: {
    paddingVertical: 8,
    paddingHorizontal: 10,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#E5E7EB',
    backgroundColor: '#FFFFFF',
  },
  chipActive: {
    backgroundColor: '#208AEF',
    borderColor: '#208AEF',
  },
  chipText: {
    fontSize: 12,
    fontWeight: '600',
    color: '#4B5563',
  },
  chipTextActive: {
    color: '#FFFFFF',
  },
  resultBox: {
    backgroundColor: '#F0FDF4',
    padding: 14,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#BBF7D0',
    alignItems: 'center',
    gap: 4,
  },
  resultLabel: {
    fontSize: 13,
    color: '#166534',
  },
  resultValue: {
    fontSize: 22,
    fontWeight: '700',
    color: '#15803D',
  },
  ratesRow: {
    flexDirection: 'row',
    gap: 16,
  },
  resultDetail: {
    fontSize: 12,
    color: '#166534',
    fontWeight: '600',
  },
});
