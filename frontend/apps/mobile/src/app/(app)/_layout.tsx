import { Tabs, Redirect } from 'expo-router';
import { useSessionStore } from '@/lib/session';
import { useColorScheme } from 'react-native';
import { Colors } from '@/constants/theme';

export default function AppLayout() {
  const isAuthenticated = useSessionStore((state) => state.isAuthenticated);
  const colorScheme = useColorScheme();
  const colors = Colors[colorScheme === 'dark' ? 'dark' : 'light'];

  // Guarda de autenticación (FE-CP-02 / FE-NF-08)
  if (!isAuthenticated) {
    return <Redirect href="/(auth)/login" />;
  }

  return (
    <Tabs
      screenOptions={{
        headerStyle: { backgroundColor: colors.background },
        headerTintColor: colors.text,
        headerShadowVisible: false,
        tabBarStyle: {
          backgroundColor: colors.background,
          borderTopColor: colors.backgroundElement,
        },
        tabBarActiveTintColor: '#208AEF',
        tabBarInactiveTintColor: colors.textSecondary,
      }}>
      <Tabs.Screen
        name="index"
        options={{
          title: 'Mis Préstamos',
          headerTitle: 'Mis Préstamos',
        }}
      />
      <Tabs.Screen
        name="applications"
        options={{
          title: 'Solicitudes',
          headerTitle: 'Mis Solicitudes',
        }}
      />
      <Tabs.Screen
        name="notifications"
        options={{
          title: 'Notificaciones',
          headerTitle: 'Centro de Notificaciones',
        }}
      />
      <Tabs.Screen
        name="profile"
        options={{
          title: 'Mi Perfil',
          headerTitle: 'Perfil del Cliente',
        }}
      />

      {/* Rutas secundarias no visibles en la barra de tabs */}
      <Tabs.Screen
        name="loans/[id]"
        options={{
          href: null,
          title: 'Detalle de Préstamo',
        }}
      />
      <Tabs.Screen
        name="pay/[loanId]"
        options={{
          href: null,
          title: 'Pagar Cuota',
        }}
      />
      <Tabs.Screen
        name="complaints"
        options={{
          href: null,
          title: 'Libro de Reclamaciones',
        }}
      />
      <Tabs.Screen
        name="onboarding"
        options={{
          href: null,
          title: 'Onboarding y KYC',
        }}
      />
    </Tabs>
  );
}
