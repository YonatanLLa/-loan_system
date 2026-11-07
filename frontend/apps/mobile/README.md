# @prestamos/mobile — Portal del cliente (React Native + Expo)

App móvil del **portal del cliente** del sistema de préstamos. Reemplaza/complementa
a `apps/portal` (web) para el rol `ROLE_CLIENT`: onboarding, KYC, solicitud,
firma, cronograma y pagos. El **backoffice no va aquí**.

Forma parte del monorepo pnpm `prestamos-frontend` y reutiliza el contrato del
backend vía `@prestamos/api-client` (misma fuente OpenAPI que el portal web).

## Arrancar

Instala siempre desde la **raíz del frontend** (no con `npm install` aquí):

```bash
# en frontend/
pnpm install
pnpm dev:mobile        # equivale a: pnpm --filter @prestamos/mobile start
```

Luego, en el menú de Expo: `a` (Android), `i` (iOS) o `w` (web).

## URL del backend

Se lee de la variable `EXPO_PUBLIC_API_URL` (con fallback a `http://localhost:5000`).
Créate un `.env` en esta carpeta según dónde corras la app:

```bash
# iOS simulator
EXPO_PUBLIC_API_URL=http://localhost:5000
# Android emulator (localhost del host es 10.0.2.2)
EXPO_PUBLIC_API_URL=http://10.0.2.2:5000
# Dispositivo físico (IP de tu PC en la LAN)
EXPO_PUBLIC_API_URL=http://192.168.x.x:5000
```

## Diferencias con el portal web (importante)

El cliente HTTP (`src/lib/api.ts`) reutiliza `@prestamos/api-client`, pero móvil
no es un navegador, así que hay dos adaptaciones:

1. **`crypto.randomUUID`** no existe en Hermes → lo rellenamos con `expo-crypto`
   en `src/lib/crypto-polyfill.ts` (se importa antes de crear el cliente).
2. **Refresh token:** la web usa una cookie `HttpOnly`; aquí no hay cookies de
   navegador, así que el refresh token se guarda en el almacén seguro del sistema
   (`expo-secure-store` → Keychain / Keystore) y se envía explícitamente al
   endpoint de refresco. El access token sigue viviendo solo en memoria.
   > Ajusta la ruta/campos de `/api/auth/refresh` en `src/lib/api.ts` al contrato
   > real del backend .NET.

## Reconciliar versiones nativas

Si Expo avisa de versiones de paquetes nativos fuera de rango:

```bash
npx expo install --fix
```
