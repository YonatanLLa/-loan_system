-- =============================================================================
-- SEEDS — DATOS INICIALES DEL SISTEMA
-- Ejecutar DESPUÉS de todas las migraciones e índices.
-- Contiene catálogos base y el usuario administrador inicial.
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Monedas (ISO 4217)
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO currencies (code, name, symbol) VALUES
    ('PEN', 'Sol Peruano',       'S/'),
    ('USD', 'Dólar Americano',   '$'),
    ('EUR', 'Euro',              '€')
ON CONFLICT (code) DO NOTHING;

-- ─────────────────────────────────────────────────────────────────────────────
-- Empresa placeholder (actualizar vía US-39 en el panel de administración)
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO company (legal_name, trade_name, tax_id, legal_address, default_currency_id)
VALUES (
    'Mi Empresa Financiera S.A.C.',
    'FinCredit',
    '00000000000',
    'Lima, Perú',
    (SELECT id FROM currencies WHERE code = 'PEN')
)
ON CONFLICT (tax_id) DO NOTHING;

-- ─────────────────────────────────────────────────────────────────────────────
-- Tipos de documento de identidad
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO document_types (code, name, char_length, validation_regex) VALUES
    ('DNI',       'Documento Nacional de Identidad',    8,   '^\d{8}$'),
    ('CE',        'Carné de Extranjería',               9,   '^\d{9}$'),
    ('RUC',       'Registro Único de Contribuyentes',   11,  '^\d{11}$'),
    ('PASAPORTE', 'Pasaporte',                          NULL, '^[A-Z0-9]{6,12}$')
ON CONFLICT (code) DO NOTHING;

-- ─────────────────────────────────────────────────────────────────────────────
-- Métodos de pago habilitados
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO payment_methods (name, method_type, is_active) VALUES
    ('Efectivo',                  'EFECTIVO',            TRUE),
    ('Transferencia Bancaria',    'TRANSFERENCIA',       TRUE),
    ('Yape',                      'YAPE',                TRUE),
    ('Plin',                      'PLIN',                TRUE),
    ('Tarjeta Débito/Crédito',    'TARJETA',             TRUE),
    ('Depósito en Ventanilla',    'DEPOSITO_VENTANILLA', TRUE)
ON CONFLICT DO NOTHING;

-- ─────────────────────────────────────────────────────────────────────────────
-- Sucursal principal (sede central)
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO branches (code, name, address, district, is_active) VALUES
    ('SUC-01', 'Sede Central', 'Av. Principal 100', 'Lima', TRUE)
ON CONFLICT (code) DO NOTHING;

-- ─────────────────────────────────────────────────────────────────────────────
-- Usuario Administrador inicial
-- ⚠️ IMPORTANTE: Reemplazar el password_hash con un hash real
--    generado con BCrypt (cost=12) o Argon2id ANTES de usar en producción.
--    Nunca almacenar contraseñas en texto plano.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO users (email, password_hash, full_name, role, status, branch_id)
VALUES (
    'admin@sistema.local',
    '$2b$12$PLACEHOLDER_REEMPLAZAR_CON_HASH_REAL',
    'Administrador del Sistema',
    'ROLE_ADMIN',
    'ACTIVE',
    (SELECT id FROM branches WHERE code = 'SUC-01')
)
ON CONFLICT (email) DO NOTHING;

-- ─────────────────────────────────────────────────────────────────────────────
-- Plantilla legal de contrato (versión 1 — placeholder)
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO legal_templates (code, version, content, is_current, created_by) VALUES
(
    'CONTRATO_PRESTAMO',
    1,
    '# CONTRATO DE PRÉSTAMO

**Prestamista:** {{empresa_razon_social}} — RUC: {{empresa_ruc}}
**Prestatario:** {{cliente_nombre}} — {{cliente_tipo_doc}}: {{cliente_documento}}
**Monto del Crédito:** {{moneda}} {{monto_prestamo}}
**Tasa Efectiva Anual (TEA):** {{tasa_tea}}%
**Plazo:** {{plazo_cuotas}} cuotas {{frecuencia_pago}}
**Primera cuota:** {{primera_fecha_vencimiento}}

## Tabla de Amortización
{{cronograma_tabla}}

## Condiciones de Mora
En caso de incumplimiento, se aplicará una tasa de mora de {{tasa_mora_diaria}}% diario
sobre {{base_mora}}, con {{dias_gracia}} día(s) de gracia.

## Cláusula de Cancelación Anticipada
Se aplicará una penalidad de {{penalidad_prepago}}% sobre el saldo de capital insoluto.',
    TRUE,
    (SELECT id FROM users WHERE email = 'admin@sistema.local')
)
ON CONFLICT (code, version) DO NOTHING;

-- ─────────────────────────────────────────────────────────────────────────────
-- Feriados nacionales de Perú (año corriente — ajustar anualmente)
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO holidays (holiday_date, name, scope, is_recurring) VALUES
    ('2026-01-01', 'Año Nuevo',                             'NACIONAL', TRUE),
    ('2026-04-02', 'Jueves Santo',                          'NACIONAL', FALSE),
    ('2026-04-03', 'Viernes Santo',                         'NACIONAL', FALSE),
    ('2026-05-01', 'Día del Trabajo',                       'NACIONAL', TRUE),
    ('2026-06-29', 'San Pedro y San Pablo',                 'NACIONAL', TRUE),
    ('2026-07-28', 'Fiestas Patrias — Independencia',       'NACIONAL', TRUE),
    ('2026-07-29', 'Fiestas Patrias — Día de las FFAA',    'NACIONAL', TRUE),
    ('2026-08-30', 'Santa Rosa de Lima',                    'NACIONAL', TRUE),
    ('2026-10-08', 'Combate de Angamos',                    'NACIONAL', TRUE),
    ('2026-11-01', 'Día de Todos los Santos',               'NACIONAL', TRUE),
    ('2026-12-08', 'Inmaculada Concepción',                 'NACIONAL', TRUE),
    ('2026-12-25', 'Navidad',                               'NACIONAL', TRUE)
ON CONFLICT (holiday_date, scope, branch_id) DO NOTHING;
