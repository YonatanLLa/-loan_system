# Runbook Operativo: Secretos, RS256, TOTP y Respuesta ante Compromiso (BE-SEC-10)

## 1. Formato de secretos en producción (`KeyPerFile` o gestor de secretos)

Montar los secretos bajo `Secrets:Path` (por ejemplo `/run/secrets`) con `Secrets:Provider=KeyPerFile`:

- `ConnectionStrings__Database`: cadena de conexión PostgreSQL con credenciales de producción.
- `ConnectionStrings__Redis` *(opcional)*: cadena de conexión Redis.
- `Jwt__SigningKeys`:
  ```json
  {
    "keys": [
      {
        "privateKeyPem": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----",
        "notBefore": "2026-11-01T00:00:00Z",
        "notAfter": "2027-02-13T00:00:00Z"
      }
    ]
  }
  ```
- `Security__Mfa__EncryptionKeys`:
  ```json
  {
    "active": "2026-11",
    "keys": {
      "2026-11": "<BASE64_DE_32_BYTES>"
    }
  }
  ```

> `LoanSystem.Workers` solo requiere `ConnectionStrings__Database` (y opcionalmente `ConnectionStrings__Redis`).

## 2. Rotación programada de claves JWT RS256 (sin downtime)

Frecuencia recomendada: cada **90 días** (alerta automática en `/health` a **14 días** de vencer).

1. Generar y pre-publicar la clave nueva (`notBefore = UtcNow + 24h`, `notAfter = notBefore + 104d`):
   ```bash
   ./backend/scripts/rotate-jwt-keys.sh /ruta/a/Jwt__SigningKeys
   # o en PowerShell:
   ./backend/scripts/rotate-jwt-keys.ps1 -Path /ruta/a/Jwt__SigningKeys
   ```
2. Actualizar el secreto en el vault / Secret de Kubernetes.
3. Verificar durante la ventana de pre-publicación (24 h) que `GET /.well-known/jwks.json` publica ambos `kid`.
4. Al cumplirse `notBefore` de la clave nueva, los nuevos tokens se firman automáticamente con el nuevo `kid` sin reiniciar pods; los tokens previos siguen siendo válidos hasta `notAfter + AccessTokenMinutes + ClockSkewSeconds` (~31 min).
5. En la siguiente rotación, el script purga automáticamente las claves ya expiradas más allá de la ventana de gracia.

## 3. Rotación de clave de cifrado TOTP (`Security:Mfa:EncryptionKeys`)

1. Generar una nueva clave de 32 bytes (`openssl rand -base64 32`).
2. Actualizar `Security__Mfa__EncryptionKeys` poniendo el nuevo identificador en `"active"` **manteniendo** las claves anteriores en `"keys"` hasta migrar todos los usuarios.
3. Cada usuario que verifique TOTP o apruebe una operación con step-up MFA re-cifra perezosamente su secreto a `v2:{nuevoActive}:...` en DB.
4. Para rotación forzosa antes de retirar una clave antigua, ejecutar un job/mantenimiento con `ISecretProtector` sobre los registros donde `mfa_secret_encrypted NOT LIKE 'v2:<active>:%'`.

## 4. Respuesta ante compromiso de clave

- **Clave RSA de firma (`Jwt:SigningKeys`) comprometida:**
  1. Generar una clave nueva de activación inmediata (`./rotate-jwt-keys.sh <path> --immediate`).
  2. **Eliminar** la clave comprometida del JSON de `Jwt__SigningKeys` (cualquier token firmado con su `kid` pasa a recibir `401` tras el reload).
  3. Revocar refresh tokens activos en DB si aplica y forzar nueva autenticación.
- **Clave TOTP (`Security:Mfa:EncryptionKeys`) comprometida + dump de BD:**
  1. Rotar `Security:Mfa:EncryptionKeys` con nueva clave `"active"`.
  2. Resetear MFA de las cuentas afectadas (`POST /api/users/{id}/mfa/reset`) para invalidar los secretos TOTP anteriores y obligar nuevo enrolamiento en el siguiente login.
