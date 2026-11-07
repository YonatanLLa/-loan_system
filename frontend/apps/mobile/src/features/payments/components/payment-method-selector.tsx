import React from 'react';
import { StyleSheet, Text, View, Pressable } from 'react-native';
import type { PaymentMethodType } from '@prestamos/api-client';

interface PaymentMethodSelectorProps {
  selectedMethod: PaymentMethodType;
  onSelect: (method: PaymentMethodType) => void;
}

const METHODS: { type: PaymentMethodType; label: string; icon: string }[] = [
  { type: 'YAPE', label: 'Yape (QR Dinámico)', icon: '📱' },
  { type: 'PLIN', label: 'Plin (QR Dinámico)', icon: '⚡' },
  { type: 'TRANSFERENCIA_BANCARIA', label: 'Transferencia Bancaria', icon: '🏦' },
  { type: 'PASARELA_PAGO', label: 'Tarjeta de Débito/Crédito', icon: '💳' },
];

export function PaymentMethodSelector({ selectedMethod, onSelect }: PaymentMethodSelectorProps) {
  return (
    <View style={styles.container}>
      <Text style={styles.title}>Método de Pago</Text>
      <View style={styles.list}>
        {METHODS.map((item) => {
          const isSelected = selectedMethod === item.type;
          return (
            <Pressable
              key={item.type}
              style={[styles.item, isSelected && styles.itemSelected]}
              onPress={() => onSelect(item.type)}>
              <Text style={styles.icon}>{item.icon}</Text>
              <Text style={[styles.label, isSelected && styles.labelSelected]}>{item.label}</Text>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: 8,
  },
  title: {
    fontSize: 14,
    fontWeight: '600',
    color: '#374151',
  },
  list: {
    gap: 8,
  },
  item: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 12,
    borderRadius: 8,
    borderWidth: 1.5,
    borderColor: '#E5E7EB',
    backgroundColor: '#FFFFFF',
    gap: 12,
  },
  itemSelected: {
    borderColor: '#208AEF',
    backgroundColor: '#EFF6FF',
  },
  icon: {
    fontSize: 20,
  },
  label: {
    fontSize: 15,
    color: '#374151',
    fontWeight: '500',
  },
  labelSelected: {
    color: '#1E40AF',
    fontWeight: '600',
  },
});
