import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { StatusBadge } from './ui/status-badge';

interface FeaturePlaceholderProps {
  title: string;
  description: string;
  requirements?: string;
  phase?: 'MVP' | 'F2' | 'F3';
}

export function FeaturePlaceholder({
  title,
  description,
  requirements,
  phase = 'MVP',
}: FeaturePlaceholderProps) {
  return (
    <View style={styles.container}>
      <View style={styles.card}>
        <View style={styles.badgeRow}>
          <StatusBadge label={phase} tone="info" />
          {requirements ? (
            <View style={styles.reqBadge}>
              <Text style={styles.reqText}>{requirements}</Text>
            </View>
          ) : null}
        </View>

        <Text style={styles.title}>{title}</Text>
        <Text style={styles.description}>{description}</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 16,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#F9FAFB',
  },
  card: {
    width: '100%',
    maxWidth: 420,
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    padding: 24,
    borderWidth: 1,
    borderColor: '#E5E7EB',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 8,
    elevation: 2,
    gap: 12,
  },
  badgeRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  reqBadge: {
    backgroundColor: '#F3F4F6',
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  reqText: {
    fontSize: 12,
    color: '#4B5563',
    fontWeight: '500',
  },
  title: {
    fontSize: 20,
    fontWeight: '700',
    color: '#111827',
  },
  description: {
    fontSize: 14,
    color: '#6B7280',
    lineHeight: 20,
  },
});
