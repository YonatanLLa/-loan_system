# Arquitectura del móvil — Portal del cliente (React Native + Expo)

> App `@prestamos/mobile` (`frontend/apps/mobile/`). Versión nativa del **portal del
> cliente** (`ROLE_CLIENT`). Espeja la filosofía de [`ARQUITECTURA_FRONTEND.md`](../../../../ARQUITECTURA_FRONTEND.md),
> adaptada a React Native. El **backoffice no vive aquí**.

---

## 1. Principios

1. **Mismo contrato que la web.** El móvil consume los mismos endpoints `/api/client/*`
   del backend .NET y reutiliza `@prestamos/api-client` (Axios + tipos OpenAPI). El backend
   es la única autoridad sobre el dinero; el móvil es solo un cliente.
2. **Feature-Driven (vertical slices).** El código se organiza por dominio del negocio,
   no por tipo técnico — igual que `apps/portal`.
3. **El móvil no calcula dinero.** Montos, cuotas, mora y cronogramas vienen del backend ya
   calculados; la app solo formatea y muestra (`formatMoney`, `maskTail` de `@prestamos/utils`).
4. **Tipos generados, nunca a mano** (FE-NF-03). Los DTOs salen del OpenAPI vía `pnpm gen:api`.

---

## 2. Stack

| Área | Elección | Equivalente web | Nota |
|---|---|---|---|
| Runtime / tooling | **Expo SDK 58** + expo-router | Vite + Vue Router | Rutas basadas en archivos (`src/app/`) |
| Lenguaje | **TypeScript strict** | igual | `tsc --noEmit` en verde |
| Cliente HTTP | **`@prestamos/api-client`** (Axios) | igual (reutilizado) | `SessionPort`, interceptores 401, idempotencia |
| Estado global | **Zustand** *(recomendado)* | Pinia | Ligero, sin boilerplate; store de sesión/catálogos |
| Formularios | **React Hook Form + Zod** *(recomendado)* | VeeValidate + Zod | Reusa esquemas de `@prestamos/utils` |
| Validación compartida | **`@prestamos/utils`** | igual (reutilizado) | `dni`, `ruc`, `cci`, `celularPe`, `monto`, `correoOCelular` |
| Almacenamiento seguro | **`expo-secure-store`** | cookie HttpOnly | Refresh token en Keychain/Keystore |
| UUID / correlación | **`expo-crypto`** | `crypto.randomUUID` nativo | Polyfill ya implementado |
| Notificaciones push | **`expo-notifications`** | — (nuevo en móvil) | Ver `MOV-01` |
| Biometría | **`expo-local-authentication`** *(recomendado)* | — | Ver `MOV-03` |
| Cámara (KYC) | **`expo-camera`** | `useUserMedia` | Ver `MOV-05` |
| Toasts | librería nativa (ej. `sonner-native`) *(recomendado)* | Vue Sonner | Mostrar `ApiError.detail` |

> "*(recomendado)*" = propuesta de esta arquitectura, aún no instalada. Lo ya presente
> (`@prestamos/api-client`, `expo-secure-store`, `expo-crypto`, expo-router) está en uso.

---

## 3. Estructura de carpetas (feature-driven)

```
apps/mobile/
├── app.json                      # config Expo (scheme: prestamos)
├── metro.config.js               # resolución del monorepo pnpm
├── docs/                         # esta documentación
└── src/
    ├── app/                      # RUTAS (expo-router, file-based)
    │   ├── _layout.tsx           # provider raíz + polyfill + sesión
    │   ├── (auth)/               # grupo público: login, registro, OTP
    │   ├── (app)/                # grupo protegido (requiere sesión + KYC)
    │   │   ├── _layout.tsx       # guarda de sesión / tabs
    │   │   ├── index.tsx         # home "Mis préstamos"
    │   │   ├── loans/[id].tsx    # detalle + cronograma
    │   │   ├── pay/[loanId].tsx  # pagar cuota
    │   │   └── profile.tsx
    │   └── +not-found.tsx
    ├── features/                 # LÓGICA por dominio (vertical slices)
    │   ├── auth/
    │   ├── onboarding/           # KYC, consentimientos, cuenta de desembolso
    │   ├── applications/         # simulador + solicitud + línea de tiempo
    │   ├── contract/             # revisión + firma con OTP
    │   ├── loans/                # lista, cronograma, estado de cuenta, prepago
    │   ├── payments/             # QR / pasarela / voucher + recibo
    │   ├── profile/              # cambio de cuenta, saldo a favor
    │   ├── notifications/        # centro + preferencias + push
    │   └── complaints/           # Libro de Reclamaciones
    ├── lib/
    │   ├── api.ts                # instancia del cliente (YA EXISTE)
    │   ├── crypto-polyfill.ts    # crypto.randomUUID (YA EXISTE)
    │   └── session.ts            # store de sesión (Zustand)
    └── components/               # design system nativo (equivalente a packages/ui)
```

### Anatomía de una feature

Igual que en el portal web: una feature solo expone su `index.ts`; nadie importa sus archivos
internos.

```
features/payments/
├── index.ts          # API pública de la feature
├── api/              # una función por endpoint, tipada desde OpenAPI
├── hooks/            # equivalente a composables: useXxx() → { data, isLoading, error, refresh }
├── components/       # UI propia de la feature (recibe props, no llama API)
├── schemas/          # esquemas Zod del formulario
└── types.ts          # tipos de vista derivados del contrato
```

---

## 4. Capas y reglas de dependencia

```
pantalla (src/app/*)
   ↓
components + hooks          (estado de pantalla, carga, acciones)
   ↓
features/<x>/api            (una función por endpoint, tipada)
   ↓
@prestamos/api-client       (Axios + tipos OpenAPI + idempotencia + errores)
   ↓
Backend .NET  /api/client/*
```

| Capa | Hace | No hace |
|---|---|---|
| `app/*` (ruta) | Lee params, compone pantalla, conecta hooks | Llamar Axios directo, lógica de negocio |
| `components/` | Presentación por props, emite eventos | Llamar API, leer el store |
| `hooks/` | Estado de pantalla, carga de datos, acciones, errores | — |
| `api/` | Una función por endpoint, tipada | Transformar reglas de negocio |
| `lib/session.ts` | Sesión, perfil, catálogos en cache | Guardar listas/detalles del servidor |

Cada hook de lectura expone `data`, `isLoading`, `error`, `refresh()` y cancela su petición
al desmontar (`AbortController`). Tras una mutación se llama `refresh()`; no hay cache global
de listas/detalles (los catálogos sí viven en el store con vencimiento).

---

## 5. Sesión y autenticación en móvil

Diferencia clave con la web: **no hay navegador, no hay cookie `HttpOnly`**. El patrón se
adapta manteniendo el mismo `SessionPort` de `@prestamos/api-client` (ver `src/lib/api.ts`):

- **Access token:** solo en memoria (equivalente móvil de FE-NF-08). Nunca en disco plano.
- **Refresh token:** en **`expo-secure-store`** (Keychain en iOS / Keystore en Android). Se
  envía explícitamente al endpoint de refresco.
- **401 compartido:** el interceptor de `@prestamos/api-client` reintenta una sola vez tras
  un refresco compartido entre peticiones concurrentes; si falla → `onSessionExpired()` limpia
  la sesión y manda al login.
- **Aislamiento por `customer_id`:** garantizado por el backend; la app nunca asume acceso a
  recursos de otro cliente (`403`/`404`).
- **Cierre por inactividad (15 min, RNF-19):** con `AppState` (background) + temporizador de
  inactividad; al expirar se limpia el access token y el refresh de SecureStore.
- **Reingreso biométrico (`MOV-03`, opcional):** Face ID / huella para re-habilitar la sesión
  sin reescribir la contraseña.

---

## 6. Idempotencia y manejo de errores

Se reutiliza **tal cual** lo de `@prestamos/api-client`:

- **Idempotencia (FE-NF-04):** toda operación de dinero (pago, cambio de cuenta) usa
  `createIdempotentAction` / `IDEMPOTENCY_HEADER`. Se genera **una** `Idempotency-Key` en el
  primer intento y se reutiliza en reintentos; el botón queda deshabilitado mientras espera.
- **Errores (FE-NF-05):** el backend responde `ProblemDetails` (RFC 7807) → `toApiError` lo
  normaliza a `ApiError { status, title, detail, fieldErrors, traceId }`.
  - `detail` → toast nativo en español.
  - `fieldErrors` → `toFormErrors` → `setError` de React Hook Form.

---

## 7. Qué se reutiliza del monorepo y qué no

| Paquete | ¿Se reutiliza en móvil? | Detalle |
|---|---|---|
| `@prestamos/api-client` | **Sí, directo** | Cliente HTTP, `SessionPort`, idempotencia, errores, tipos OpenAPI |
| `@prestamos/utils` | **Sí, directo** | Esquemas Zod (`dni`, `ruc`, `cci`, `celularPe`, `monto`) y formatos (`formatMoney`, `formatBusinessDate`, `maskTail`) |
| `@prestamos/ui` | **No** | Son componentes Vue (shadcn-vue). El móvil tiene su propio design system nativo en `src/components/`, pero **copia el catálogo de estados/tonos** (verde/amarillo/rojo/azul) de `status.ts` |
| `@prestamos/mocks` | Opcional | API simulada MSW; útil para desarrollo sin backend |

---

## 8. Adaptaciones nativas

- **`crypto.randomUUID`** no existe en Hermes → polyfill con `expo-crypto` en
  `src/lib/crypto-polyfill.ts` (se importa una sola vez, antes de crear el cliente).
- **URL del backend** vía `EXPO_PUBLIC_API_URL`:
  - iOS simulator → `http://localhost:5000`
  - Android emulator → `http://10.0.2.2:5000`
  - Dispositivo físico → `http://<IP-de-tu-PC>:5000`
- **Almacenamiento seguro** del refresh token (`expo-secure-store`).
- **Push, biometría, cámara, deep links** → ver requerimientos `MOV-01`..`MOV-08` en
  [`REQUERIMIENTOS_MOBILE.md`](./REQUERIMIENTOS_MOBILE.md).

---

## 9. Testing y calidad

| Tipo | Herramienta | Cobertura |
|---|---|---|
| Estático | `tsc --noEmit` (`pnpm type-check`) | Tipos estrictos; en verde |
| Unitarias | Jest + esquemas Zod | Formateadores, validaciones, hooks |
| Componentes | **React Native Testing Library** *(recomendado)* | Formularios con errores del backend, estados de carga |
| Lint | `expo lint` (ESLint) | Fronteras entre features (`index.ts` público) |

---

## 10. Distribución

- **EAS Build** para generar binarios iOS/Android; publicación en App Store / Google Play
  (`MOV-07`).
- **OTA updates** (expo-updates) para parches de JS sin pasar por review de stores.
- `EXPO_PUBLIC_API_URL` configurado por ambiente (dev / qa / staging / prod).

---

## Referencias

- Arquitectura web: [`ARQUITECTURA_FRONTEND.md`](../../../../ARQUITECTURA_FRONTEND.md)
- Arquitectura general y seguridad: [`ARQUITECTURA_GENERAL.md`](../../../../ARQUITECTURA_GENERAL.md) §9
- Requerimientos del móvil: [`REQUERIMIENTOS_MOBILE.md`](./REQUERIMIENTOS_MOBILE.md)
