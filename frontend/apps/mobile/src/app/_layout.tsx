import '@/lib/crypto-polyfill';
import { useEffect, useState } from 'react';
import { DarkTheme, DefaultTheme, ThemeProvider, Stack, useRouter } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import * as Linking from 'expo-linking';
import { useColorScheme, View, ActivityIndicator } from 'react-native';
import { useSessionStore } from '@/lib/session';
import { ToastBanner } from '@/components/ui/toast-banner';
import { PrivacyOverlay } from '@/components/ui/privacy-overlay';
import { useToastStore } from '@/lib/toast';

SplashScreen.preventAutoHideAsync();

export default function RootLayout() {
  const colorScheme = useColorScheme();
  const router = useRouter();
  const initSession = useSessionStore((state) => state.initSession);
  const touchActivity = useSessionStore((state) => state.touchActivity);
  const isLoading = useSessionStore((state) => state.isLoading);
  const [isReady, setIsReady] = useState(false);

  useEffect(() => {
    async function prepare() {
      try {
        await initSession();
      } finally {
        setIsReady(true);
        await SplashScreen.hideAsync();
      }
    }
    void prepare();
  }, [initSession]);

  // MOV-04: Deep links / universal links (scheme `prestamos`)
  useEffect(() => {
    const subscription = Linking.addEventListener('url', ({ url }) => {
      const parsed = Linking.parse(url);
      if (parsed.path?.startsWith('pay/')) {
        const loanId = parsed.queryParams?.loanId;
        useToastStore.getState().showToast({
          title: 'Retorno de pasarela',
          detail: 'Verificando el estado de tu pago en línea...',
          tone: 'info',
        });
        if (typeof loanId === 'string') {
          router.push({ pathname: '/(app)/pay/[loanId]', params: { loanId } });
        }
      }
    });
    return () => subscription.remove();
  }, [router]);

  if (!isReady || isLoading) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: '#FFFFFF' }}>
        <ActivityIndicator size="large" color="#208AEF" />
      </View>
    );
  }

  return (
    <ThemeProvider value={colorScheme === 'dark' ? DarkTheme : DefaultTheme}>
      <View style={{ flex: 1 }} onTouchStart={touchActivity}>
        <ToastBanner />
        <Stack screenOptions={{ headerShown: false }}>
          <Stack.Screen name="(app)" options={{ headerShown: false }} />
          <Stack.Screen name="(auth)" options={{ headerShown: false }} />
          <Stack.Screen name="+not-found" options={{ title: 'No encontrado' }} />
        </Stack>
        <PrivacyOverlay />
      </View>
    </ThemeProvider>
  );
}
