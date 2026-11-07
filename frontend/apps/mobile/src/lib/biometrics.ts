import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

const BIOMETRICS_ENABLED_KEY = 'prestamos.biometricsEnabled';
const BIOMETRICS_IDENTIFIER_KEY = 'prestamos.biometricsIdentifier';

export async function isBiometricsEnabled(): Promise<boolean> {
  if (Platform.OS === 'web') return false;
  try {
    const val = await SecureStore.getItemAsync(BIOMETRICS_ENABLED_KEY);
    return val === 'true';
  } catch {
    return false;
  }
}

export async function getBiometricIdentifier(): Promise<string | null> {
  if (Platform.OS === 'web') return null;
  try {
    return await SecureStore.getItemAsync(BIOMETRICS_IDENTIFIER_KEY);
  } catch {
    return null;
  }
}

export async function setBiometricsPreference(
  enabled: boolean,
  identifier?: string
): Promise<void> {
  if (Platform.OS === 'web') return;
  if (enabled) {
    await SecureStore.setItemAsync(BIOMETRICS_ENABLED_KEY, 'true');
    if (identifier) {
      await SecureStore.setItemAsync(BIOMETRICS_IDENTIFIER_KEY, identifier);
    }
  } else {
    await SecureStore.deleteItemAsync(BIOMETRICS_ENABLED_KEY);
    await SecureStore.deleteItemAsync(BIOMETRICS_IDENTIFIER_KEY);
  }
}
