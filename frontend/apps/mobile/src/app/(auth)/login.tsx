import React from 'react';
import { StyleSheet, View, Text, Pressable } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useRouter } from 'expo-router';
import { LoginCard, useAuth } from '@/features/auth';
import { useSessionStore } from '@/lib/session';
import { Button } from '@/components/ui/button';

export default function LoginScreen() {
  const router = useRouter();
  const { login, isLoading, error } = useAuth();
  const setSession = useSessionStore((state) => state.setSession);
  const biometricsEnabled = useSessionStore((state) => state.biometricsEnabled);
  const savedIdentifier = useSessionStore((state) => state.savedBiometricIdentifier);

  const handleLogin = async (identifier: string) => {
    try {
      await login({ identifier });
      router.replace('/(app)');
    } catch {
      router.push({
        pathname: '/(auth)/otp',
        params: { identifier, purpose: 'LOGIN' },
      });
    }
  };

  const handleBiometricLogin = async () => {
    if (!savedIdentifier) return;
    await handleLogin(savedIdentifier);
  };

  const handleDemoLogin = async () => {
    try {
      await login({ identifier: 'cliente@correo.pe' });
      router.replace('/(app)');
    } catch {
      await setSession(
        {
          id: 'usr-100',
          customerId: 'cus-001',
          fullName: 'María Quispe Huamán',
          email: 'cliente@correo.pe',
          docType: 'DNI',
          docNumber: '41234567',
          phone: '987000000',
          kycStatus: 'VERIFIED',
        },
        'demo-access-token'
      );
      router.replace('/(app)');
    }
  };

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.content}>
        <LoginCard onSubmit={handleLogin} isLoading={isLoading} errorMessage={error} />

        {/* MOV-03: Reingreso biométrico opcional */}
        {biometricsEnabled && savedIdentifier ? (
          <Button
            title={`Reingresar con Biometría (${savedIdentifier})`}
            variant="outline"
            onPress={handleBiometricLogin}
            isLoading={isLoading}
          />
        ) : null}

        <Button
          title="Ingresar con Datos Semilla (María Quispe)"
          variant="secondary"
          onPress={handleDemoLogin}
          isLoading={isLoading}
        />

        <View style={styles.footer}>
          <Text style={styles.footerText}>¿Aún no tienes cuenta?</Text>
          <Pressable onPress={() => router.push('/(auth)/register')}>
            <Text style={styles.linkText}>Solicita tu préstamo aquí</Text>
          </Pressable>
        </View>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F9FAFB',
  },
  content: {
    flex: 1,
    justifyContent: 'center',
    paddingHorizontal: 20,
    gap: 16,
    maxWidth: 480,
    width: '100%',
    alignSelf: 'center',
  },
  footer: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    gap: 6,
  },
  footerText: {
    fontSize: 14,
    color: '#6B7280',
  },
  linkText: {
    fontSize: 14,
    fontWeight: '600',
    color: '#208AEF',
  },
});
