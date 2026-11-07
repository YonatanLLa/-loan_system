import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import {
  getStatusInfo,
  statusToneColors,
  type StatusKind,
  type StatusTone,
} from '@/constants/status';

interface StatusBadgeProps {
  kind?: StatusKind;
  status?: string;
  label?: string;
  tone?: StatusTone;
}

export function StatusBadge({ kind, status, label, tone }: StatusBadgeProps) {
  let displayLabel = label;
  let displayTone: StatusTone = tone ?? 'neutral';

  if (kind && status) {
    const info = getStatusInfo(kind, status);
    displayLabel = label ?? info.label;
    displayTone = tone ?? info.tone;
  }

  const colors = statusToneColors[displayTone];

  return (
    <View
      style={[
        styles.badge,
        {
          backgroundColor: colors.background,
          borderColor: colors.border,
        },
      ]}>
      <Text style={[styles.text, { color: colors.text }]}>{displayLabel ?? status}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  badge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
    borderWidth: 1,
    alignSelf: 'flex-start',
  },
  text: {
    fontSize: 12,
    fontWeight: '600',
  },
});
