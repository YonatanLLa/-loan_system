import React, { useEffect, useState } from 'react';
import { AppState, type AppStateStatus, StyleSheet, Text, View } from 'react-native';

/**
 * MOV-08: Protección de pantallas sensibles.
 * Cuando la app pasa a `inactive` o `background` (selector de apps / multitarea),
 * cubre la pantalla para ocultar montos, saldos y cuentas bancarias.
 */
export function PrivacyOverlay() {
  const [hidden, setHidden] = useState(false);

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (nextState: AppStateStatus) => {
      setHidden(nextState !== 'active');
    });
    return () => subscription.remove();
  }, []);

  if (!hidden) return null;

  return (
    <View style={styles.overlay} pointerEvents="auto">
      <View style={styles.shield}>
        <Text style={styles.logo}>🔒</Text>
        <Text style={styles.title}>Portal Préstamos</Text>
        <Text style={styles.subtitle}>Contenido protegido por seguridad (MOV-08)</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  overlay: {
    position: 'absolute',
    top: 0,
    right: 0,
    bottom: 0,
    left: 0,
    backgroundColor: '#0F172A',
    zIndex: 9999,
    justifyContent: 'center',
    alignItems: 'center',
  },
  shield: {
    alignItems: 'center',
    gap: 8,
    padding: 24,
  },
  logo: {
    fontSize: 44,
  },
  title: {
    fontSize: 20,
    fontWeight: '700',
    color: '#FFFFFF',
  },
  subtitle: {
    fontSize: 13,
    color: '#94A3B8',
  },
});
