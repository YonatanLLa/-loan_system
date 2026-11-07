import { Redirect } from 'expo-router';
import { useSessionStore } from '@/lib/session';

export default function RootIndex() {
  const isAuthenticated = useSessionStore((state) => state.isAuthenticated);
  return <Redirect href={isAuthenticated ? '/(app)' : '/(auth)/login'} />;
}
