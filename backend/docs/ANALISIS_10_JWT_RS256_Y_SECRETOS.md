# Análisis — BE-SEC-10: JWT firmado con RS256, claves rotadas y secretos solo en el gestor de secretos

> Documento de **análisis previo**: no implementa código. Revisa el requisito, el estado actual del backend, la
> configuración y el frontend, propone un diseño coherente con la Arquitectura Hexagonal del módulo `Security` y deja
> listadas las decisiones que hay que tomar antes de escribir el plan de implementación.
>
> Fecha: 2026-11-13 · Relacionado: [`PLAN_01_SECURITY.md`](PLAN_01_SECURITY.md) §8,
> [`ANALISIS_03_MFA_TOTP.md`](ANALISIS_03_MFA_TOTP.md) (clave de cifrado TOTP),
> [`ANALISIS_09_CUATRO_OJOS.md`](ANALISIS_09_CUATRO_OJOS.md) (TOTP al aprobar).

---

## 1. Resumen

- **Qué pide el requisito:**
  - firmar los access tokens con **RS256** (par de claves RSA) en lugar de HS256 (clave compartida);
  - **rotar** las claves de firma de forma periódica;
  - que **todos los secretos** vivan solo en un **gestor de secretos**: nunca en el repositorio, la BD ni las imágenes.
- **Estado hoy:**
  - Los tokens se firman con **HS256** y una sola clave, `Jwt:Key`, sin `kid`, así que no hay forma de rotarla sin
    invalidar a la vez todos los access tokens.
  - La clave de desarrollo está **versionada en git**.
  - La clave que cifra los secretos TOTP (`Security:Mfa:EncryptionKey`) tampoco admite rotación y, en local, no se puede
    cargar como dice la documentación (ver §3.4).
  - No hay ningún gestor de secretos integrado.
- **Propuesta:**
  - Un **juego de claves** RSA (*key ring*) que se lee del gestor y se recarga sin reiniciar. Cada clave tiene una
    ventana de firma (`notBefore` / `notAfter`), y su `kid` se calcula a partir de la propia clave.
  - La aplicación firma con la clave más reciente que esté vigente y valida por `kid`, aceptando solo `RS256`.
  - Una clave nueva se publica **antes** de empezar a firmar con ella, y la anterior sigue validando hasta que caduque
    el último token que firmó. Así la rotación **no corta sesiones** y no hay que coordinar las instancias.
  - Las claves públicas se exponen en `GET /.well-known/jwks.json`.
  - En el host, el gestor se conecta como un **proveedor de `IConfiguration`**, de modo que los módulos no conocen
    ningún SDK.
- **Tamaño:**
  - **Sin migraciones SQL** y **sin cambios en el frontend**.
  - Código nuevo: 1 componente de juego de claves, el endpoint JWKS, un *health check* y una extensión de host para el
    gestor de secretos.
  - Código que cambia: `JwtTokenGenerator`, la validación (sale de `Program.cs` hacia el módulo `Security`) y
    `AesGcmSecretProtector` (pasa a admitir varias claves).
  - Además: guion de rotación, *runbook* y escaneo de secretos en CI.

---

## 2. Requisito y fuentes

| Fuente | Texto |
|---|---|
| [`REQUERIMIENTOS_BACKEND.md`](../../docs/REQUERIMIENTOS_BACKEND.md) — **BE-SEC-10** | JWT firmado con RS256 y claves rotadas; secretos solo en el gestor de secretos · RNF-01, RNF-03 · MVP · Especificado |
| [`01-seguridad-y-accesos.md`](../../docs/01-seguridad-y-accesos.md) — **RNF-01** | Claves privadas JWT rotadas periódicamente; tokens firmados con RS256 o HS512 |
| [`01-seguridad-y-accesos.md`](../../docs/01-seguridad-y-accesos.md) — **RNF-03** | Variables sensibles (connection strings, JWT secrets) gestionadas vía almacén de secretos (Key Vault o variables protegidas) |
| [`ARQUITECTURA_GENERAL.md`](../../ARQUITECTURA_GENERAL.md) §9 | **Secretos**: gestor de secretos (credenciales bancarias, HMAC, JWT); nada en BD ni en el repositorio |
| [`08-tesoreria-digital.md`](../../docs/08-tesoreria-digital.md) — **RF-23.4** | Credenciales de API bancaria o de pasarela en el gestor de secretos, nunca en la BD |
| [`13-administracion-y-configuracion.md`](../../docs/13-administracion-y-configuracion.md) — **RF-40.4** | API keys y secretos HMAC de cada proveedor en el gestor, **rotados sin intervención en BD** |
| [`PLAN_01_SECURITY.md`](PLAN_01_SECURITY.md) §8 | Migrar de HS256 a RS256 con claves en el gestor y rotación; afecta a `JwtTokenGenerator` y a la validación de `Program.cs` |

RNF-01 admite RS256 **o** HS512; BE-SEC-10 fija **RS256**, y este análisis sigue a BE-SEC-10. La ventaja que se
busca es que **quien valida no pueda firmar**: con una clave simétrica, cualquier proceso o servicio que valide
tokens podría también emitirlos.

**Fuera de alcance:**

- **Refresh tokens:** son opacos y se guardan como hash; no los firma la clave JWT y su rotación ya existe (BE-SEC-01).
- **Lista de revocación de access tokens** (`jti`).
- **Proveedor de identidad externo / OIDC:** Auth0 ya se descartó en `PLAN_02` §1.
- **Cifrado de la BD en reposo.**
- **Credenciales de BD de corta duración por IAM:** F2 (ver §5.9).
- **Elegir la nube** (Q1).

---

## 3. Estado actual

### 3.1 Backend: firma y validación de JWT

| Archivo | Qué hace hoy | Problema para BE-SEC-10 |
|---|---|---|
| [`Program.cs`](../src/LoanSystem.WebApi/Program.cs) (líneas 71–94) | Lee `Jwt:Key`, exige ≥ 32 bytes y valida con `SymmetricSecurityKey`. Valida emisor, audiencia, vigencia y firma | Clave simétrica (quien valida puede firmar); una sola clave sin `kid`; no fija `ValidAlgorithms`; `ClockSkew` queda en el valor por defecto de **5 min**, así que un token de 30 min vale en la práctica 35. La validación vive fuera del módulo `Security` |
| [`JwtTokenGenerator.cs`](../src/Modules/Security/LoanSystem.Modules.Security.Adapters/Outbound/Providers/JwtTokenGenerator.cs) | Firma HS256 con la misma `Jwt:Key`, que lee por su cuenta. TTL `Jwt:AccessTokenMinutes` (30 por defecto) | No pone `kid`. Emisor, audiencia y clave se leen en **dos sitios** (aquí y en `Program.cs`) |
| `IJwtTokenGenerator` (`Security.Core/Ports/Outbound`) | `Generate(User) → (AccessToken, ExpiresAt)` | **No cambia**: el cambio queda en el adaptador y el Core no se toca |
| [`ConfigurationExtensions.cs`](../src/Common/LoanSystem.Common.Adapters/Configuration/ConfigurationExtensions.cs) | `GetRequiredValue` / `GetRequiredConnectionString`: fallan al arrancar si falta el valor | Correcto; se reutiliza |
| [`SecurityModule.cs`](../src/Modules/Security/LoanSystem.Modules.Security.Adapters/SecurityModule.cs) | Registra `JwtTokenGenerator` y `AesGcmSecretProtector` como singletons perezosos; `OnTokenValidated` comprueba que el usuario siga activo y con el mismo rol (BE-SEC-06) | `LoanSystem.Workers` también llama a `AddSecurityModule`. Hoy no falla porque nadie resuelve esos servicios, pero el diseño nuevo **no** debe obligar a Workers a tener acceso a las claves |
| `RefreshTokenHasher` / desafío MFA (`mfaToken`) | Tokens aleatorios de 256 bits guardados como SHA-256 | No usan secretos y **no cambian** |
| [`JwtTokenValidationStatusTests.cs`](../tests/LoanSystem.Adapters.UnitTests/JwtTokenValidationStatusTests.cs) | Monta su propio *pipeline* con HS256 | Hay que pasarlo a RSA |
| [`ApprovalFlowIntegrationTests.cs`](../tests/LoanSystem.IntegrationTests/ApprovalFlowIntegrationTests.cs) | Inyecta `Jwt:Key` y `Security:Mfa:EncryptionKey` en memoria | Hay que adaptarlo al nuevo formato |

No hay pruebas con `WebApplicationFactory`, aunque `Program` ya expone `public partial class Program` para eso.

### 3.2 Inventario de secretos

| Secreto | Clave de configuración | Dónde está hoy | Proceso que lo usa | ¿Se puede rotar hoy? |
|---|---|---|---|---|
| Clave de firma JWT | `Jwt:Key` | `WebApi/appsettings.Development.json`, **en git**; fuera de Development, en variable de entorno | WebApi (firma y valida) | Solo con corte: sin `kid` no pueden convivir dos claves |
| Clave de cifrado de secretos TOTP | `Security:Mfa:EncryptionKey` (AES-256, Base64) | **En ningún archivo**. Hay que definir a mano `Security__Mfa__EncryptionKey` (ver §3.4) | WebApi (verificar y enrolar MFA, aprobar con TOTP en BE-SEC-09) | No: una sola clave y formato `v1:` sin id de clave |
| Cadena de conexión PostgreSQL (con contraseña) | `ConnectionStrings:Database` | `appsettings.Development.json` de WebApi y Workers, con la contraseña de `docker-compose.yml` | WebApi, Workers, DbMigrator (argumento o variable) | Requiere reinicio: la cadena se captura al arrancar (`AddSecurityModule`, `NpgsqlConnectionFactory`) |
| Redis | `ConnectionStrings:Redis` | `appsettings.Development.json`, sin contraseña | WebApi (idempotencia) | Requiere reinicio |
| **Futuros** | — | Todavía no existen | — | — |
| ↳ API key de Resend | `Email:Resend:ApiKey` (propuesta) | — | Quien envíe correo (BE-SEC-13) | — |
| ↳ Secretos HMAC de webhooks | `Integrations:{Proveedor}:WebhookSecret` (propuesta) | — | WebApi (BE-INT-01) | Debe admitir dos secretos a la vez (RF-40.4) |
| ↳ Credenciales bancarias y de pasarela, OSE/PSE, SMS/WhatsApp | — | — | Workers (Outbox) | — |
| ↳ Almacenamiento de objetos | — | — | WebApi (BE-CUS-03) | Mejor con identidad administrada que con clave |

No son secretos, y siguen en `appsettings.json`: `Jwt:Issuer`, `Jwt:Audience`, `Security:*`, `Approvals:*`,
`Idempotency:*` y `ForwardedHeaders:*`.

### 3.3 Frontend (backoffice, portal y app móvil)

- Las tres apps usan `@prestamos/api-client`. El access token es **opaco** para ellas: ninguna lo decodifica, y los
  permisos se leen de `GET /api/me`.
- Ante un `401`, el interceptor de [`http.ts`](../../frontend/packages/api-client/src/http.ts) refresca la sesión una
  vez y reintenta la petición. Por eso cambiar el algoritmo o rotar una clave **no obliga a iniciar sesión de nuevo**:
  el refresh token vive en la BD y no depende de la clave JWT.
- Los `.env.*` del frontend solo tienen URLs y banderas. `VITE_*` y `EXPO_PUBLIC_*` acaban **dentro del bundle
  público**, así que nunca pueden contener un secreto.

### 3.4 Hallazgos

> ⚠️ **Clave JWT de desarrollo en el repositorio.** `appsettings.Development.json` lleva una `Jwt:Key` fija que está
> en el historial de git desde el commit `c35b7ec`. Con HS256, cualquiera con acceso al repositorio puede **firmar**
> tokens válidos para cualquier entorno que, por error, arranque con esa configuración. Con RS256 y una clave efímera
> en desarrollo (§5.6), en el repositorio no queda ninguna clave. La clave vieja deja de servir en cuanto se valida
> solo RS256, así que no hace falta reescribir el historial.

> ⚠️ **La clave de cifrado TOTP no se puede cargar en local como dice la documentación.** `ANALISIS_03` §6.7 indica
> que va en *user-secrets* en desarrollo, pero `LoanSystem.WebApi.csproj` no tiene `UserSecretsId`, así que ASP.NET
> Core no carga *user-secrets*. Tampoco está en `appsettings.Development.json` ni en el `README`. Resultado: en local,
> `/api/auth/mfa/enroll`, `/mfa/verify` y la aprobación de BE-SEC-09 lanzan `InvalidOperationException` (`500`) salvo
> que se defina la variable `Security__Mfa__EncryptionKey`.

> ⚠️ **La configuración de un token se lee en dos sitios.** El emisor, la audiencia y la clave los lee tanto
> `JwtTokenGenerator` como `Program.cs`. Cualquier cambio (y RS256 lo es) obliga a tocar ambos sin que nada garantice
> que coincidan.

> ℹ️ No hay pipeline de CI en el repositorio, así que tampoco hay **escaneo de secretos** que impida subir uno nuevo.

---

## 4. Alcance

### 4.1 Reglas

| # | Regla |
|---|---|
| **R1** | Los access tokens se firman con **RS256** (RSA ≥ 2048 bits; por defecto 3072) usando solo la clave activa. Cada token lleva `kid` en la cabecera |
| **R2** | La validación acepta **solo `RS256`** y solo claves del juego vigente, elegidas por `kid`. Un token sin `kid`, con `kid` desconocido o con otro `alg` (incluido `none`) recibe `401` |
| **R3** | **Rotación sin cortes:** una clave se publica para validar **antes** de usarse para firmar. La anterior sigue validando hasta que caduque el último token que firmó (TTL del access token + `ClockSkew`) |
| **R4** | **Rotación programada** (90 días, Q4) y **rotación de emergencia**: quitar una clave comprometida invalida sus tokens en ≤ `ReloadInterval`, sin redesplegar |
| **R5** | La clave privada nunca está en el repositorio, en imágenes, en la BD, en logs, en volcados de configuración ni en respuestas HTTP. Los logs solo muestran el `kid` |
| **R6** | En entornos desplegados, los secretos (claves de firma, clave TOTP, credenciales de BD, Redis y proveedores) se leen solo del gestor. Los módulos solo conocen claves de `IConfiguration`; solo el host sabe qué gestor hay |
| **R7** | **Fallo al arrancar** fuera de Development si falta un secreto obligatorio, si no hay clave de firma activa o si se detecta un valor de desarrollo conocido |
| **R8** | **Mínimo privilegio por proceso:** solo WebApi lee las claves JWT y la clave TOTP. Workers y DbMigrator no tienen acceso a ellas |
| **R9** | Las claves **públicas** vigentes se publican en `GET /.well-known/jwks.json`, sin parámetros privados |
| **R10** | Para llegar al gestor se usa una **identidad administrada o de carga de trabajo**, no un secreto guardado en configuración |
| **R11** | La clave de cifrado TOTP admite **varias versiones**: descifra con cualquiera de ellas y cifra con la activa, para poder rotarla |

### 4.2 Excepción explícita: desarrollo local

Los valores que solo sirven contra los contenedores de `docker-compose.yml` (contraseña de PostgreSQL, Redis sin
contraseña) pueden seguir en `appsettings.Development.json`: son públicos por definición y no protegen nada real. Las
claves criptográficas **no** entran en esta excepción (§5.6, D10).

---

## 5. Diseño propuesto

### 5.1 Visión general

```mermaid
flowchart LR
    ROT["Rotación (fuera de la app)<br/>pipeline o guion de ops"] -->|nueva versión del secreto| GS[("Gestor de secretos")]
    GS -->|proveedor de IConfiguration<br/>recarga periódica| CFG["IConfiguration"]
    CFG --> KR["JwtKeyRing (singleton)<br/>instantánea inmutable"]
    KR -->|clave activa| GEN["JwtTokenGenerator<br/>RS256 + kid"]
    KR -->|clave por kid| VAL["JwtBearer<br/>IssuerSigningKeyResolver"]
    KR -->|solo la parte pública| JWKS["GET /.well-known/jwks.json"]
    KR --> HC["Health check<br/>(clave activa, días hasta caducar)"]
```

- La aplicación **solo lee** el gestor, nunca escribe en él. Generar claves es tarea del proceso de rotación (D11).
- `JwtKeyRing` vive en `Security.Adapters/Outbound/Providers`. Guarda una **instantánea inmutable** (claves ya
  parseadas e indexadas por `kid`) que se reemplaza de forma atómica en cada recarga. Validar no toma ningún *lock*.

### 5.2 Formato del juego de claves

Un **único secreto**, `Jwt:SigningKeys`, con este JSON (D3):

```json
{
  "keys": [
    {
      "privateKeyPem": "-----BEGIN PRIVATE KEY-----\n…\n-----END PRIVATE KEY-----",
      "notBefore": "2026-11-20T15:00:00Z",
      "notAfter":  "2027-03-04T15:00:00Z"
    },
    {
      "privateKeyPem": "-----BEGIN PRIVATE KEY-----\n…\n-----END PRIVATE KEY-----",
      "notBefore": "2027-02-18T15:00:00Z",
      "notAfter":  "2027-06-03T15:00:00Z"
    }
  ]
}
```

- **`kid`:** es la huella JWK de la clave pública (RFC 7638, base64url de SHA-256), y la calcula la aplicación. No
  viaja en el secreto, así que no puede quedar mal etiquetado (D4).
- **Clave que firma:** la de `notBefore` más reciente que cumpla `notBefore ≤ ahora < notAfter`.
- **Claves que validan:** todas las cargadas, incluidas las que todavía no firman (prepublicadas), hasta
  `notAfter + AccessTokenMinutes + ClockSkew`. A partir de ahí se ignoran, aunque sigan en el secreto.
- **Validaciones al cargar:** PEM PKCS#8 RSA de al menos 2048 bits, fechas coherentes y ningún `kid` repetido. Si una
  recarga trae un JSON inválido o **ninguna clave activa**, se mantiene la instantánea anterior, se registra un error
  y el *health check* pasa a `Degraded`. El juego de claves **nunca queda vacío**. Al arrancar, el mismo error impide
  el arranque (R7).
- **Tamaño:** una clave RSA-3072 en PEM ocupa unos 2,5 KB, así que 3 claves caben de sobra en los límites habituales
  (Azure Key Vault: 25 KB; AWS Secrets Manager: 64 KB).

### 5.3 Ciclo de vida de una clave

| Fase | Duración | Clave A (sale) | Clave B (entra) |
|---|---|---|---|
| 1. Publicación | Desde que B se añade al secreto hasta `B.notBefore` (≥ 24 h, muy por encima de `ReloadInterval`) | firma y valida | **solo valida** (sale en el JWKS) |
| 2. Cambio | Instantáneo en `B.notBefore` | deja de firmar | firma y valida |
| 3. Drenaje | TTL del access token + `ClockSkew` (≈ 31 min con D9) | solo valida | firma y valida |
| 4. Retiro | En la siguiente rotación | el guion la quita del secreto | firma y valida |

Como el cambio depende de la **hora** y no de una orden, todas las instancias cambian a la vez sin coordinarse. Si una
instancia tiene el reloj algo adelantado y firma antes con B, las demás ya conocen B desde la fase 1 (D5).

`notAfter` de A es un **tope de seguridad**: si la rotación no se ejecuta, A deja de firmar en esa fecha. El *health
check* avisa `RotationWarningDays` antes (14 por defecto), para que nunca se llegue a quedar sin clave activa.

### 5.4 Firma: `JwtTokenGenerator`

- Recibe `IJwtKeyRing` y crea `SigningCredentials(new RsaSecurityKey(rsa) { KeyId = kid }, SecurityAlgorithms.RsaSha256)`
  con la clave activa en ese momento.
- **Los claims no cambian** (`NameIdentifier`, `Email`, `Role`), ni el TTL ni el puerto `IJwtTokenGenerator`. La
  cabecera queda como `{"alg":"RS256","kid":"…","typ":"JWT"}`.
- No se libera (`Dispose`) la instancia `RSA` de una instantánea anterior mientras pueda haber firmas en curso. Se
  deja al recolector de basura.

### 5.5 Validación: sale de `Program.cs` hacia el módulo `Security`

La configuración de `AddAuthentication().AddJwtBearer(...)` pasa a una extensión pública del módulo:
`AddSecurityAuthentication(configuration, environment)`, en el espacio de nombres raíz de `Security.Adapters`. La
prueba de arquitectura de hosts permite esta llamada, porque el host no toca `Adapters.Outbound`. Solo la llama
**WebApi**, así que Workers no carga el juego de claves (R8). Emisor, audiencia, clave y TTL salen de **un solo
lugar** (corrige el tercer hallazgo de §3.4).

```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,  ValidIssuer = issuer,
    ValidateAudience = true, ValidAudience = audience,
    ValidateLifetime = true, RequireExpirationTime = true,
    RequireSignedTokens = true,
    ValidateIssuerSigningKey = true,
    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],          // R2: bloquea HS256 y none
    IssuerSigningKeyResolver = (_, _, kid, _) => keyRing.GetValidationKeys(kid),
    TryAllIssuerSigningKeys = false,                            // sin kid o con kid desconocido → 401
    ClockSkew = TimeSpan.FromSeconds(clockSkewSeconds),         // D9
    RoleClaimType = ClaimTypes.Role
};
```

- El `kid` solo se busca en un diccionario en memoria. No se usa para leer archivos, URLs ni la BD, y nunca se siguen
  las cabeceras `jku`, `x5u` o `jwk`.
- Se mantiene el `OnTokenValidated` de `SecurityModule`, que comprueba que el usuario siga activo y con el mismo rol.
- **Perezoso salvo en WebApi:** en Development, el host valida el grafo de DI al arrancar (`ValidateOnBuild`), y
  Workers registra los casos de uso de `Security`. Por eso `JwtKeyRing` se registra en `AddSecurityModule` sin cargar
  nada en el constructor. La carga anticipada (fallo al arrancar, R7) la añade solo `AddSecurityAuthentication`, que
  registra una comprobación de arranque.

### 5.6 Desarrollo y pruebas

- **Development sin `Jwt:SigningKeys`:** se genera una clave RSA **efímera** en memoria al arrancar y se registra un
  aviso en el log. Al reiniciar la API, los access tokens dejan de valer, el frontend recibe `401` y refresca, y la
  sesión sigue. No queda ninguna clave en el repositorio.
- **Se borra `Jwt:Key`** de `appsettings.Development.json`.
- **Fuera de Development no hay clave efímera:** si no hay juego de claves, la aplicación no arranca (R7).
- **Pruebas:** un *helper* genera juegos de claves en memoria (una clave activa, una prepublicada y una caducada) y
  las fechas se controlan con `IClock`.

### 5.7 Endpoint JWKS

- **Ruta:** `GET /.well-known/jwks.json`, con `.AllowAnonymous()` y `Cache-Control: public, max-age=300`.
- **Contenido:** solo `kty`, `use: "sig"`, `alg: "RS256"`, `kid`, `n` y `e` de las claves que validan (incluidas las
  prepublicadas).
- **Cómo se respeta la arquitectura:** el endpoint (adaptador de entrada) no puede depender de
  `Adapters.Outbound`. Por eso llama a un caso de uso `IGetJwksUseCase`, que a su vez usa un puerto de salida
  `IPublicSigningKeysProvider` y devuelve un DTO `JwkDto` de `Contracts`. El Core no ve tipos de `Microsoft.IdentityModel`.
- **La clave pública se exporta con `rsa.ExportParameters(includePrivateParameters: false)`.** Nunca se pasa la clave
  privada por `JsonWebKeyConverter`, porque copiaría `d`, `p`, `q`, `dp`, `dq` y `qi`. Una prueba lo comprueba (§11).
- No es un documento de descubrimiento OIDC: solo publica las claves.

### 5.8 Gestor de secretos: integración en el host

- Una extensión `builder.Configuration.AddSecretManager(builder.Environment)` en
  `Common.Adapters/Configuration` añade el proveedor que diga la configuración, que no es secreta:

  ```json
  "Secrets": {
    "Provider": "KeyPerFile",              // None | KeyPerFile | AzureKeyVault | AwsSystemsManager
    "Path": "/mnt/secrets",                // KeyPerFile
    "VaultUri": null,                      // Azure Key Vault
    "ReloadInterval": "00:05:00"
  }
  ```

- La llaman **WebApi, Workers y DbMigrator**, cada uno con **su propia identidad** y una política de acceso que solo
  le deja leer sus secretos (R8, R10).
- Los módulos siguen leyendo `IConfiguration`. Equivalencia de nombres:

  | Origen | Nombre de `Jwt:SigningKeys` |
  |---|---|
  | Azure Key Vault | `Jwt--SigningKeys` |
  | Archivo (`KeyPerFile`) | `Jwt__SigningKeys` |
  | Variable de entorno | `Jwt__SigningKeys` |

- **Opciones de proveedor** (la elección depende de Q1):

  | Opción | Paquete | Credenciales | Recarga |
  |---|---|---|---|
  | **Archivos montados por la plataforma** (Kubernetes + Secrets Store CSI Driver, que funciona con Azure, AWS, GCP y Vault; o Docker *secrets*) | `Microsoft.Extensions.Configuration.KeyPerFile` (oficial) | La plataforma; la app no tiene SDK ni credenciales | `reloadOnChange`; en Kubernetes el archivo se cambia con un *symlink*, así que conviene sondeo (`DOTNET_USE_POLLING_FILE_WATCHER`) |
  | **Azure Key Vault** | `Azure.Extensions.AspNetCore.Configuration.Secrets` (oficial) | `DefaultAzureCredential` (identidad administrada) | `ReloadInterval` |
  | **AWS** | `Amazon.Extensions.Configuration.SystemsManager` (Parameter Store, que puede referenciar Secrets Manager) | Rol IAM de la tarea o del *pod* | `reloadAfter` |
  | **Variables de entorno inyectadas por la plataforma** | — | — | Solo al reiniciar. **No sirve para el juego de claves**: es un PEM multilínea, se ve en `docker inspect` o en `/proc/<pid>/environ` y no se recarga |

- **Imagen de producción:** `dotnet publish` copia por defecto `appsettings.Development.json` a la salida. Hay que
  excluirlo (`CopyToPublishDirectory="Never"`) para que la imagen no lleve ni siquiera los valores de desarrollo.
- **Guardia de arranque** fuera de Development (R7): comprueba que estén todos los secretos obligatorios y rechaza
  valores de desarrollo conocidos (la contraseña de `docker-compose.yml`, o la antigua `Jwt:Key` mientras exista).

### 5.9 Otros secretos

- **PostgreSQL:**
  - La cadena de conexión sale del gestor.
  - Hoy, rotar la contraseña obliga a **reiniciar de forma escalonada** cada proceso, porque la cadena se captura al
    arrancar. En F2 se puede usar `NpgsqlDataSourceBuilder.UsePeriodicPasswordProvider` o autenticación IAM (Entra ID
    o RDS IAM).
  - Se recomienda separar el rol de la app (DML) del rol de DbMigrator (DDL), y que la credencial de DbMigrator solo
    exista en CI/CD (Q8).
- **Redis:** contraseña y TLS en producción, desde el gestor. Rotarla también requiere reinicio.
- **Proveedores futuros:**
  - Convención de nombres `Integrations:{Proveedor}:{Nombre}`. Se leen con `IOptionsMonitor<T>` para que una rotación
    no requiera reiniciar (RF-40.4).
  - Los secretos HMAC de webhooks admiten `Current` y `Previous` a la vez, para rotar sin coordinar el momento exacto
    con el proveedor.
- **Logs:**
  - Nunca se registran valores de configuración ni `IConfiguration.GetDebugView()`.
  - Npgsql no incluye la contraseña en la cadena de conexión que expone (`Persist Security Info=false` por defecto),
    y no se debe activar.

### 5.10 Clave de cifrado TOTP: varias versiones (R11)

`AesGcmSecretProtector` pasa de una sola clave a un juego de claves:

```json
"Security": { "Mfa": { "EncryptionKeys": "{\"active\":\"2026-11\",\"keys\":{\"v1\":\"<base64>\",\"2026-11\":\"<base64>\"}}" } }
```

- **Formato nuevo:** `v2:{keyId}:{base64(nonce|ciphertext|tag)}`. Los valores `v1:` existentes se descifran con la
  clave de id `v1`, que es la clave actual.
- **Se liga el cifrado al usuario:** el `user_id` va como dato asociado (AAD) de AES-GCM. Así, quien pueda escribir
  en la BD no puede copiar su propio secreto TOTP cifrado sobre la fila de otro usuario. Para eso
  `ISecretProtector.Protect` y `Unprotect` reciben el `userId`, un cambio pequeño en el puerto del Core.
- **Re-cifrado perezoso:** cuando `VerifyMfaUseCase`, `ConfirmMfaEnrollmentUseCase` o `ApproveRequestUseCase`
  descifran un valor cifrado con una clave que no es la activa, lo vuelven a cifrar y lo guardan en la misma
  transacción. Para detectarlo, el puerto ofrece `NeedsReprotect(value)`.
- **Retiro de la clave vieja:** pasado un plazo (D12), los usuarios que sigan con la clave vieja pasan por `ResetMfa`
  y se vuelven a enrolar en su próximo login. Son pocos, porque solo hay MFA en Admin y Operador. Después se quita la
  clave del juego.
- El interceptor de auditoría ya excluye las propiedades que contienen `secret`, así que `audit_logs` nunca guarda el
  texto cifrado.

### 5.11 Configuración resultante

`appsettings.json` (sin secretos):

```json
"Jwt": {
  "Issuer": "LoanSystem.Identity",
  "Audience": "LoanSystem.App",
  "AccessTokenMinutes": 30,
  "ClockSkewSeconds": 60,
  "RotationWarningDays": 14,
  "MinRsaKeySizeBits": 2048
},
"Secrets": { "Provider": "None", "ReloadInterval": "00:05:00" }
```

**En el gestor** (uno por proceso):

| Proceso | Secretos |
|---|---|
| WebApi | `Jwt:SigningKeys`, `Security:Mfa:EncryptionKeys`, `ConnectionStrings:Database`, `ConnectionStrings:Redis` |
| Workers | `ConnectionStrings:Database` y las credenciales de los proveedores que despacha el Outbox |
| DbMigrator | Su propia `ConnectionStrings:Database` (rol DDL) |

**Development:** `appsettings.Development.json` conserva solo las cadenas de `docker-compose`. La clave TOTP va en
*user-secrets*, lo que exige añadir `UserSecretsId` al `.csproj` de WebApi y documentar
`dotnet user-secrets set "Security:Mfa:EncryptionKeys" …` en el `README`.

---

## 6. Procedimientos operativos (*runbook*)

### 6.1 Rotación programada (cada 90 días; lo ejecuta un pipeline, D11)

1. Generar el par fuera de la aplicación, en el *runner* del pipeline y sin escribirlo en disco persistente:
   `openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072`.
2. Crear una **nueva versión** del secreto `Jwt:SigningKeys`:
   - añadir la clave nueva con `notBefore = ahora + 24 h` y `notAfter = notBefore + 90 d + 14 d`;
   - quitar las claves con `notAfter + 1 h < ahora`.
3. Comprobar, en ≤ `ReloadInterval`, que cada instancia registra «juego de claves recargado» con el `kid` nuevo, y que
   el JWKS muestra las dos claves.
4. Pasado `notBefore`, comprobar en los logs y métricas que los tokens nuevos llevan el `kid` nuevo.

Nadie inicia sesión de nuevo y no se redespliega nada.

### 6.2 Rotación de emergencia (clave privada comprometida)

1. En **una sola versión** del secreto, quitar la clave comprometida y añadir una nueva con `notBefore = ahora`, sin
   prepublicación.
2. En ≤ `ReloadInterval`, todas las instancias rechazan los tokens del `kid` comprometido. Los clientes reciben `401`
   y refrescan, y los usuarios legítimos siguen trabajando.
3. Si 5 minutos es demasiado, se puede forzar un **reinicio escalonado** para acortar la ventana.
4. **Investigar:**
   - Un token falsificado con la clave robada pasa `OnTokenValidated` si suplanta a un usuario activo con su rol real.
     Hay que revisar en `audit_logs` las acciones de ese periodo.
   - Revisar el registro de accesos del gestor de secretos (quién leyó el secreto).
   - Si el incidente también afectó a la BD, revocar todos los refresh tokens.

### 6.3 Rotación de la clave TOTP

1. Añadir una clave nueva al juego y marcarla como `active`.
2. Esperar a que se re-cifre por uso (§5.10).
3. Al cumplirse el plazo, `ResetMfa` de los usuarios rezagados.
4. Quitar la clave vieja del juego.

### 6.4 Rotación de la contraseña de PostgreSQL

1. Crear una contraseña nueva (`ALTER ROLE`) o, mejor, un segundo rol con los mismos permisos.
2. Actualizar el secreto.
3. Reiniciar de forma escalonada WebApi y Workers.
4. Retirar la contraseña o el rol anterior.

---

## 7. Decisiones técnicas (alternativas y recomendación)

| # | Decisión | Alternativas | Recomendación |
|---|---|---|---|
| **D1** | Algoritmo y tamaño de clave | (a) RS256 RSA-2048 · (b) RS256 RSA-3072 · (c) ES256 · (d) HS512 | **(b)**. BE-SEC-10 fija RS256. RSA-2048 sirve hasta 2030 (NIST SP 800-57), pero 3072 evita revisarlo y su coste extra (≈ 4 ms al firmar, ~170 caracteres más por token) solo se paga en login y refresh. El cargador rechaza claves de menos de 2048 bits |
| **D2** | Dónde está la clave privada al firmar | (a) en el gestor, cargada en la memoria del proceso · (b) en KMS/HSM, con firma remota y sin que la clave salga nunca · (c) cifrada en la BD | **(a) para el MVP**. (b) es lo más fuerte, pero añade latencia (10–50 ms), coste por operación y una dependencia de disponibilidad en cada login y refresh. El diseño admite (b) más adelante con un `SignatureProvider` propio. (c) está prohibida («nada en BD»). Depende de Q3 |
| **D3** | Formato del juego de claves | (a) un secreto JSON con todas las claves · (b) un secreto por clave, con etiquetas · (c) versiones nativas de claves del gestor | **(a)**: se lee de forma atómica, funciona con cualquier proveedor (también con archivos y variables) y es fácil de probar |
| **D4** | Origen del `kid` | (a) huella JWK calculada (RFC 7638) · (b) nombre asignado a mano | **(a)**: no puede quedar mal etiquetado ni repetirse |
| **D5** | Cómo entra en servicio una clave | (a) por hora (`notBefore`) · (b) bandera `active` que se cambia a mano · (c) orden a cada instancia | **(a)**: las instancias no se coordinan y la prepublicación sale sola |
| **D6** | Cómo llegan los secretos al proceso | (a) proveedor nativo del gestor · (b) archivos montados por la plataforma (`KeyPerFile`) · (c) variables de entorno | **(b) por defecto** (sin SDK ni credenciales en la app, y portable) y **(a)** en cuanto se resuelva Q1. **(c) no** para el juego de claves |
| **D7** | Paso de HS256 a RS256 | (a) cambio directo · (b) tres fases: validar ambos y firmar HS256 → firmar RS256 → quitar HS256 | **(a) si ningún entorno tiene usuarios reales (Q2)**: los access tokens HS256 reciben `401` y el refresh los renueva sin que nadie se dé cuenta, sin código de transición. **(b)** solo si hay producción con varias instancias y despliegue escalonado: con (a), una instancia vieja y una nueva se rechazarían los tokens entre sí durante el despliegue |
| **D8** | Endpoint JWKS | (a) publicarlo ya · (b) solo red interna · (c) no publicarlo | **(a)**: es pequeño, no expone nada sensible y es la razón de ser de RS256 (verificar sin poder firmar). Sirve para un futuro *gateway* o BI, y para depurar. Depende de Q5 |
| **D9** | `ClockSkew` | (a) dejar el valor por defecto (5 min) · (b) 60 s configurable | **(b)**: los servidores sincronizan por NTP, y 5 min alarga un 17 % la vida real de un token de 30 min |
| **D10** | Claves en desarrollo | (a) clave fija en el repositorio · (b) JWT efímera y TOTP en *user-secrets* · (c) todo en variables de entorno | **(b)**. La clave JWT puede ser efímera gracias al refresh. La TOTP no, porque cifra datos que persisten en la BD; va en *user-secrets* (hay que añadir `UserSecretsId`) |
| **D11** | Quién rota | (a) la propia app (necesita escribir en el gestor) · (b) un pipeline programado con un guion del repositorio · (c) la rotación nativa del gestor · (d) manual con alerta | **(b)** más la alerta del *health check* como red de seguridad. (a) rompe el mínimo privilegio. (c) no genera el JSON de D3 sin una función propia (posible en AWS, no en la política de rotación de Azure) |
| **D12** | Rotación de la clave TOTP | (a) formato `v2` con varias claves ya, re-cifrado perezoso y re-enrolamiento al cumplir el plazo · (b) un comando que re-cifra todo de una vez · (c) dejarlo para más adelante | **(a)**. Cambiar el formato después obliga a migrar datos igualmente; (b) necesita otra herramienta con acceso a la clave (rompe R8). Plazo propuesto: 30 días (Q6) |

---

## 8. Riesgos y vectores de ataque

| Riesgo / vector | Cómo se cubre |
|---|---|
| **Confusión de algoritmo:** un token HS256 firmado usando la clave pública como secreto HMAC, o `alg: none` | `ValidAlgorithms = [RS256]`, `RequireSignedTokens`, y cada `kid` corresponde a una `RsaSecurityKey` (§5.5). Hay prueba |
| **`kid` malicioso** (*path traversal*, SQL) o sin `kid` para forzar que se prueben todas las claves | El `kid` solo se busca en un diccionario en memoria y `TryAllIssuerSigningKeys = false` |
| **Cabeceras `jku`, `x5u` o `jwk`** que apuntan a claves del atacante | Nunca se usan para obtener claves |
| **JWKS que filtra la clave privada** | Se exporta con `ExportParameters(false)`. Una prueba falla si aparecen `d`, `p`, `q`, `dp`, `dq` o `qi` |
| **Clave privada en logs, excepciones, volcados o variables de entorno** | Solo se registra el `kid`; el juego de claves no tiene `ToString`; no se usan variables de entorno para el juego (D6) |
| **La rotación no se ejecuta** | El *health check* pasa a `Degraded` `RotationWarningDays` antes de `notAfter` y a `Unhealthy` si no queda clave activa; el monitor alerta |
| **Una versión del secreto mal formada** | Se mantiene la instantánea anterior, se registra un error y el *health check* pasa a `Degraded`. Al arrancar, no arranca (R7) |
| **Una instancia no recarga** (el vigilante de archivos no detecta el cambio de *symlink*) | Sondeo y `ReloadInterval`; se publica una métrica con el número de claves y su `kid` por instancia |
| **Una clave retirada sigue valiendo hasta la recarga** | Como mucho `ReloadInterval` (5 min); en emergencia, reinicio escalonado (§6.2) |
| **Clave robada:** suplantación de cualquier usuario activo con su rol real | Solo la identidad de WebApi puede leer el secreto, el gestor registra cada acceso con alertas, y hay rotación de emergencia (§6.2) |
| **Workers o DbMigrator comprometidos** | No tienen acceso ni a las claves JWT ni a la TOTP (R8) |
| **Valores de desarrollo en producción** (`ASPNETCORE_ENVIRONMENT=Development` por error, o cadenas copiadas) | Guardia de arranque (§5.8); la imagen no incluye `appsettings.Development.json`; la clave efímera solo existe en Development |
| **Alguien sube un secreto nuevo al repositorio** | Escaneo de secretos (p. ej. *gitleaks*) en CI y en *pre-commit*, y una prueba de configuración (§11) |
| **Un secreto en el bundle del frontend** | Regla: `VITE_*` y `EXPO_PUBLIC_*` son públicos y nunca llevan secretos |
| **Clave TOTP robada junto con un volcado de la BD** | Permite generar códigos, pero aún hace falta la contraseña. Se rota la clave (§6.3) o se fuerza el re-enrolamiento de todos |
| **Credenciales para llegar al gestor** | Identidad administrada o de carga de trabajo, sin credenciales fijas (R10) |
| **Token válido de un usuario suspendido** | Ya lo cubre `OnTokenValidated` (BE-SEC-06); no cambia |

---

## 9. Impacto en otros requisitos y documentos

| Requisito / documento | Impacto |
|---|---|
| **BE-SEC-01** | Los tokens pasan a RS256; los claims y el TTL no cambian |
| **BE-SEC-03** / `ANALISIS_03` §6.7 | La clave TOTP pasa al gestor y al formato `v2` con varias claves. Hay que corregir la nota de *user-secrets*, que hoy no funciona (§3.4) |
| **BE-SEC-06** | Sin cambios: `OnTokenValidated` se mantiene |
| **BE-SEC-09** | La aprobación con TOTP depende de la clave TOTP; su re-cifrado perezoso incluye `ApproveRequestUseCase` |
| **BE-SEC-13** (Resend) | `Email:Resend:ApiKey` en el gestor, accesible solo para el proceso que envía |
| **BE-INT-01** (webhooks) | Secretos HMAC en el gestor, con `Current` y `Previous` para rotar |
| **RF-23.4, RF-40.4** (credenciales bancarias y de proveedores) | En el gestor, leídas con `IOptionsMonitor` para rotar sin reiniciar |
| **BE-CUS-03** (URLs firmadas) | Acceso al almacenamiento con identidad administrada. Si se firma con una clave de cuenta, esa clave va en el gestor |
| `PLAN_01_SECURITY.md` §1 y §8 | Actualizar el estado de BE-SEC-10 al implementar |
| `README.md` y `ARQUITECTURA_Y_CONFIGURACION.md` | Desaparece `Jwt__Key`. Documentar `Jwt:SigningKeys`, `Secrets:*`, *user-secrets* para la clave TOTP y el *runbook* |
| **BD** | Ninguno: no hay migración |

---

## 10. Impacto en el frontend

- **No hay cambios de código.** El token es opaco y el cambio de algoritmo o de clave se resuelve con el
  refresco automático tras un `401`.
- **Regla para mantener:** no decodificar el JWT en el cliente para leer el rol o los permisos; se usa `GET /api/me`.
- **Regla para mantener:** ningún secreto en `.env*` (`VITE_*` y `EXPO_PUBLIC_*` son públicos).
- **Nota aparte, no relacionada con BE-SEC-10:** en la app móvil, `loginApi` guarda el access token como
  `refreshToken` (`features/auth/api/index.ts`). Cuando exista el login real de clientes, el refresco fallará y el
  usuario tendrá que volver a entrar. Conviene registrarlo como defecto aparte.

---

## 11. Pruebas

| Suite | Casos |
|---|---|
| **Adapters.UnitTests: `JwtKeyRing`** | <ul><li>Firma con la clave más reciente vigente.</li><li>Valida con una clave prepublicada.</li><li>Deja de validar tras `notAfter + TTL + ClockSkew`.</li><li>Rechaza claves de menos de 2048 bits, PEM inválidos y `kid` repetidos.</li><li>Una recarga inválida conserva la instantánea anterior.</li><li>Fuera de Development, sin claves, lanza excepción.</li><li>En Development, sin claves, genera una efímera.</li></ul> |
| **Adapters.UnitTests: `JwtTokenGenerator`** | <ul><li>La cabecera lleva `alg: RS256` y `kid`.</li><li>Los claims no cambian.</li><li>El token valida con la clave pública.</li></ul> |
| **Adapters.UnitTests: validación** (adaptar `JwtTokenValidationStatusTests`) | <ul><li>RS256 válido → `200`.</li><li>HS256 firmado con los bytes de la clave pública → `401`.</li><li>`alg: none` → `401`.</li><li>Sin `kid` o con `kid` desconocido → `401`.</li><li>Token de una clave retirada tras la recarga → `401`.</li><li>Caducado por encima de `ClockSkew` → `401`.</li></ul> |
| **Adapters.UnitTests: JWKS** | <ul><li>Solo aparecen `kty`, `use`, `alg`, `kid`, `n` y `e`; **nunca** `d`, `p`, `q`, `dp`, `dq` ni `qi`.</li><li>Incluye la clave prepublicada.</li></ul> |
| **Adapters.UnitTests: `AesGcmSecretProtector` v2** | <ul><li>Descifra `v1:` con la clave `v1`.</li><li>Cifra con la clave activa.</li><li>Descifra con una clave anterior.</li><li>Con un `keyId` desconocido, falla.</li><li>Con el AAD de otro usuario, falla.</li><li>`NeedsReprotect` es correcto.</li></ul> |
| **Adapters.UnitTests: guardia de arranque** | <ul><li>Fuera de Development, falla con la contraseña de `docker-compose` o sin `Jwt:SigningKeys`.</li><li>En Development, arranca.</li></ul> |
| **IntegrationTests** (primer uso de `WebApplicationFactory`) | <ul><li>`login` → token RS256 → `/api/me` `200`.</li><li>Rotación simulada con `IClock` y recarga de configuración: los tokens de A siguen valiendo durante el drenaje y los nuevos llevan el `kid` de B.</li><li>Flujo MFA con la clave TOTP `v2`.</li></ul> |
| **Prueba de configuración** (Architecture.Tests o una suite nueva) | <ul><li>`appsettings.json` de WebApi y Workers no tiene valores en claves con `Key`, `Secret`, `Password`, `ApiKey` ni en `ConnectionStrings`.</li><li>`appsettings.Development.json` no tiene `Jwt:Key` ni claves PEM.</li></ul> |
| **CI** | Escaneo de secretos en cada PR, con una lista de excepciones para los valores de `docker-compose` |

---

## 12. Orden de trabajo propuesto

Los pasos 1 a 4 no dependen de qué gestor se elija (Q1) y se pueden empezar ya.

1. **`JwtKeyRing`:** carga, validación, selección de la clave activa, instantánea inmutable y recarga, con sus pruebas.
2. **Firma RS256 y `AddSecurityAuthentication`:**
   - mover la validación de `Program.cs` al módulo;
   - añadir `ValidAlgorithms`, el *resolver* y `ClockSkew`;
   - generar la clave efímera en Development y borrar `Jwt:Key` de `appsettings.Development.json`;
   - adaptar las pruebas existentes (D7: cambio directo si se confirma Q2).
3. **Guardia de arranque y *health check*** del juego de claves. `/health` público solo devuelve el estado; el detalle
   va a logs y métricas.
4. **Endpoint JWKS:** `IGetJwksUseCase`, `IPublicSigningKeysProvider` y `JwkDto`.
5. **`AddSecretManager` en los tres hosts:**
   - empezar por `KeyPerFile`;
   - excluir `appsettings.Development.json` de la publicación;
   - actualizar `README` y `ARQUITECTURA_Y_CONFIGURACION`.
6. **Clave TOTP con varias versiones:** formato `v2` con AAD, re-cifrado perezoso, `UserSecretsId` y documentación de
   desarrollo.
7. **Rotación:** guion de rotación en el repositorio, pipeline programado, *runbook* (§6) y escaneo de secretos en CI.
8. **Proveedor nativo de la nube elegida** (cuando se resuelva Q1) y políticas de acceso por identidad (WebApi,
   Workers, DbMigrator).

---

## 13. Preguntas abiertas (decidir antes del plan de implementación)

| # | Pregunta | Propuesta |
|---|---|---|
| **Q1** | ¿En qué nube o plataforma se despliega y qué gestor de secretos se usa? La documentación menciona S3 y Azure Blob, pero no hay infraestructura definida | Decidirlo. Mientras tanto, implementar `KeyPerFile`, que funciona con cualquier gestor a través de la plataforma |
| **Q2** | ¿Hay hoy algún entorno con usuarios o sesiones reales (staging o producción)? | Si no, cambio directo a RS256 (D7-a) |
| **Q3** | ¿Alguna norma interna o del regulador (SBS) exige que la clave de firma esté en un HSM? | Si la exige, firmar con KMS/HSM (D2-b) desde el principio |
| **Q4** | ¿Cada cuánto se rota la clave JWT y quién es responsable del pipeline? | Cada 90 días; responsable: DevOps; alerta a 14 días de `notAfter` |
| **Q5** | ¿El JWKS debe ser público en internet o solo interno? ¿Hay consumidores externos previstos (*gateway*, BI, otros servicios)? | Público (no expone nada sensible); se revisa si aparece un *gateway* |
| **Q6** | ¿La clave TOTP se rota de forma periódica (anual) o solo ante un incidente? ¿Se acepta forzar el re-enrolamiento de quien no haya entrado en 30 días? | Anual y ante incidentes; sí al re-enrolamiento |
| **Q7** | ¿Se confirma `ClockSkew` de 60 s y TTL de 30 min? | Sí (D9) |
| **Q8** | ¿Se separan los roles de PostgreSQL de la app (DML) y de DbMigrator (DDL)? ¿Se acepta el reinicio escalonado para rotar la contraseña de BD en el MVP? | Sí y sí; IAM o contraseña periódica en F2 |
| **Q9** | ¿Las cadenas de `docker-compose` se quedan en `appsettings.Development.json` y la clave TOTP pasa a *user-secrets*? | Sí (D10, §4.2) |
| **Q10** | ¿Portal y backoffice deben usar **audiencias distintas** (`LoanSystem.Portal` y `LoanSystem.Backoffice`) con la misma clave? Hoy un token de cliente sirve para llamar a endpoints del backoffice, aunque RBAC lo frene | Recomendable como defensa en profundidad, pero es otro cambio; registrarlo como requisito aparte |
