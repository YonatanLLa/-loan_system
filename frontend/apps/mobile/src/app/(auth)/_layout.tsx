import { Stack, Redirect } from 'expo-router';
import { useSessionStore } from '@/lib/session';

export default function AuthLayout() {
  const isAuthenticated = useSessionStore((state) => state.isAuthenticated);

  // Si ya está autenticado, redirigir al portal principal
  if (isAuthenticated) {
    return <Redirect href="/(app)" />;
  }

  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: '#FFFFFF' },
        headerTintColor: '#111827',
        headerShadowVisible: false,
        headerTitleStyle: { fontWeight: '600' },
      }}>
      <Stack.Screen name="login" options={{ title: 'Iniciar Sesión', headerShown: false }} />
      <Stack.Screen name="register" options={{ title: 'Registro de Cliente' }} />
      <Stack.Screen name="otp" options={{ title: 'Verificación de Seguridad' }} />
    </Stack>
  );
}
