import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import type { ApplicationStatus } from '@prestamos/api-client';

interface ApplicationTimelineProps {
  currentStatus: ApplicationStatus;
}

const STEPS: { code: ApplicationStatus; label: string; desc: string }[] = [
  {
    code: 'SOLICITADO',
    label: '1. Solicitado',
    desc: 'Solicitud recibida con tus datos laborales y sustento de ingresos.',
  },
  {
    code: 'EN_EVALUACION',
    label: '2. En Evaluación',
    desc: 'Validación crediticia y revisión de documentos.',
  },
  {
    code: 'APROBADO',
    label: '3. Aprobado',
    desc: 'Listo para revisar y firmar contrato con OTP.',
  },
  {
    code: 'DESEMBOLSADO',
    label: '4. Desembolsado',
    desc: 'Fondos transferidos a tu cuenta verificada.',
  },
];

export function ApplicationTimeline({ currentStatus }: ApplicationTimelineProps) {
  const normalizedStatus: ApplicationStatus =
    currentStatus === 'APROBADO_CON_CONDICIONES' ? 'APROBADO' : currentStatus;
  const activeIndex = STEPS.findIndex((s) => s.code === normalizedStatus);

  return (
    <View style={styles.container}>
      {STEPS.map((step, index) => {
        const isDone = activeIndex >= index && activeIndex !== -1;
        const isCurrent = activeIndex === index;

        return (
          <View key={step.code} style={styles.stepRow}>
            <View style={styles.indicatorCol}>
              <View
                style={[
                  styles.circle,
                  isDone && styles.circleDone,
                  isCurrent && styles.circleCurrent,
                ]}>
                <Text style={[styles.circleText, isDone && styles.circleTextDone]}>
                  {isDone ? '✓' : index + 1}
                </Text>
              </View>
              {index < STEPS.length - 1 ? (
                <View style={[styles.line, activeIndex > index && styles.lineDone]} />
              ) : null}
            </View>

            <View style={styles.contentCol}>
              <Text style={[styles.stepLabel, isCurrent && styles.stepLabelCurrent]}>
                {step.label}
              </Text>
              <Text style={styles.stepDesc}>{step.desc}</Text>
            </View>
          </View>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    marginTop: 8,
  },
  stepRow: {
    flexDirection: 'row',
    gap: 12,
  },
  indicatorCol: {
    alignItems: 'center',
  },
  circle: {
    width: 26,
    height: 26,
    borderRadius: 13,
    borderWidth: 2,
    borderColor: '#D1D5DB',
    backgroundColor: '#FFFFFF',
    justifyContent: 'center',
    alignItems: 'center',
  },
  circleDone: {
    borderColor: '#10B981',
    backgroundColor: '#10B981',
  },
  circleCurrent: {
    borderColor: '#208AEF',
    backgroundColor: '#208AEF',
  },
  circleText: {
    fontSize: 12,
    fontWeight: '700',
    color: '#6B7280',
  },
  circleTextDone: {
    color: '#FFFFFF',
  },
  line: {
    width: 2,
    flex: 1,
    minHeight: 24,
    backgroundColor: '#E5E7EB',
  },
  lineDone: {
    backgroundColor: '#10B981',
  },
  contentCol: {
    flex: 1,
    paddingBottom: 16,
  },
  stepLabel: {
    fontSize: 14,
    fontWeight: '600',
    color: '#374151',
  },
  stepLabelCurrent: {
    color: '#1D4ED8',
    fontWeight: '700',
  },
  stepDesc: {
    fontSize: 12,
    color: '#6B7280',
    marginTop: 2,
  },
});
