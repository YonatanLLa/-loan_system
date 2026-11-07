# Dominio 14: Evolución del Modelo de Negocio V2 (Lógica Financiera, Riesgos, Cobranza y Tributación Avanzada)

## 1. Descripción del Dominio V2
Este documento especifica la **Versión 2.0 (V2)** de la lógica de negocio para la plataforma fintech de préstamos 100% digital en Perú. Mientras que la **V1 (MVP)** cubre el ciclo operativo end-to-end base (originación, motor de 4 métodos de amortización, desembolso digital, prelación de cobros, mora diaria, conciliación y portal de autoservicio), la **V2** incorpora los 6 pilares de madurez financiera, regulatoria y comercial indispensables para escalar el negocio con rentabilidad, control de riesgo sistémico y cumplimiento normativo peruano (SBS, BCRP, SUNAT e Indecopi):

1. **Riesgo Sistémico y Fidelización (*Credit Ladder*):** Integración con Centrales de Riesgo (Equifax/Infocorp, Sentinel, SBS), cálculo de DTI sistémico real y escalonamiento automático de clientes recurrentes (*Renovación 1-Click*).
2. **Ingeniería de Producto y Cálculo Financiero V2:** Seguro de Desgravamen sobre saldo insoluto, Períodos de Gracia Inicial (*Total* y *Parcial*), control automático de Tasa Máxima BCRP (Ley N° 31143 contra la Usura) e ITF ($0.005\%$).
3. **Negociación y Recuperación Avanzada de Cartera:** Condonación parcial/total de mora e intereses (*Waivers* / campañas de liquidación) con flujo de 4 ojos y fecha de expiración, y Convenios de Reestructuración condicionados a abono inicial (*Down Payment*).
4. **Pasarelas en Lote y Débito Automático (*Auto-Pay*):** Liquidación en lote de pasarelas (*Settlements* separando bruto, comisión pasarela + IGV y neto bancario) y afiliación de tarjetas tokenizadas para cargo recurrente automático en fecha de vencimiento.
5. **Facturación Electrónica (SUNAT / OSE) y Libro Mayor Contable (*Double-Entry Ledger*):** Emisión automática de Boletas/Facturas por intereses, mora y comisiones cobradas, Notas de Crédito por reversos, asientos contables de partida doble y cálculo mensual de provisiones de cartera según clasificación SBS.
6. **Garantías Personales, Avales y Firma Mancomunada:** Vinculación de fiador solidario o cónyuge interviniente con validación KYC y doble firma electrónica OTP previa al desembolso.

---

## 2. Historias de Usuario y Requerimientos Funcionales (US-44 a US-55)

---

### Pilar 1: Riesgo Sistémico y Fidelización (*Credit Ladder*)

### US-44: Integración con Centrales de Riesgo (SBS / Equifax / Sentinel) y DTI Sistémico
**Como** Analista de Crédito o Motor de Evaluación Automática,  
**Quiero** consultar en línea el reporte crediticio externo del solicitante en la SBS y burós de crédito (Equifax/Sentinel) e incorporar su deuda del sistema financiero en el cálculo del DTI,  
**Para** evitar sobreendeudar a clientes que aparentan liquidez interna pero mantienen mora o alto endeudamiento en otras entidades.

#### Requerimientos Funcionales
- **RF-44.1 (Consulta al Buró con Caché e Inmutabilidad):**
  - Endpoint `POST /api/customers/{id}/credit-bureau-pull` y ejecución automática al pasar una solicitud a `EN_EVALUACION`.
  - Consulta mediante el puerto `ICreditBureauGateway` obteniendo:
    - Clasificación SBS del último RCC: `NORMAL` (0), `CPP` (1), `DEFICIENTE` (2), `DUDOSO` (3), `PERDIDA` (4) o `SIN_HISTORIAL` (-1).
    - Número de entidades financieras acreedoras (`entity_count`).
    - Saldo total de deuda directa vigente y vencida en el sistema financiero (`external_total_debt`, `external_overdue_debt`).
    - Cuota mensual estimada en el sistema financiero (`external_monthly_installment`).
    - Protestos y moras comerciales vigentes en cámara de comercio (`unpaid_protests_amount`).
  - El snapshot JSON crudo y normalizado queda congelado en `credit_bureau_reports` vinculado a la evaluación (`application_id`), con vigencia configurable en caché (por defecto 15 días calendario para evitar cobros duplicados del proveedor).
- **RF-44.2 (Fórmula de DTI Sistémico Real):**
  $$\text{DTI}_{\text{sistémico}} = \frac{\text{Cuota Proyectada Nueva} + \text{Cuotas Internas Vigentes} + \text{Cuota Mensual Sistema Financiero (RCC)}}{\text{Ingreso Neto Mensual Validado}} \times 100$$
- **RF-44.3 (Reglas de Rechazo Automático — *Knock-out Rules*):**
  - Si la clasificación SBS es `DEFICIENTE`, `DUDOSO` o `PERDIDA` → Dictamen sugerido `RECHAZO_AUTOMATICO_BURO` (bloqueante salvo excepción de `ROLE_ADMIN`).
  - Si `external_overdue_debt > 0` mayor al umbral configurable (ej. S/ 200) → `RECHAZO_AUTOMATICO_BURO`.
  - Si `entity_count > max_allowed_entities` configurado en el producto (ej. > 4 entidades para mitigar *loan stacking*) → `REVISION_MANUAL` o `RECHAZO_SUGERIDO`.

#### Criterios de Aceptación
- **CA-44.1 (Cálculo de DTI Sistémico con Deuda Externa):**  
  *Dado que* un solicitante declara un ingreso neto de S/ 3,000, solicita un préstamo cuya cuota proyectada es S/ 500 y el buró reporta una cuota mensual en bancos de S/ 900,  
  *Cuando* el motor ejecuta la evaluación crediticia,  
  *Entonces* calcula $\text{DTI}_{\text{sistémico}} = (500 + 900) / 3000 = 46.67\%$ y clasifica la solicitud en `REVISION_MANUAL` (en lugar del $16.67\%$ que habría arrojado sin buró).
- **CA-44.2 (Knock-out por Clasificación SBS Deficiente o Peor):**  
  *Dado que* el reporte del buró devuelve clasificación SBS `DUDOSO`,  
  *Cuando* se evalúa la solicitud,  
  *Entonces* el motor marca la regla `BUREAU_SBS_CLASSIFICATION` como `FAIL` y bloquea la aprobación estándar.

---

### US-45: Escalonamiento de Clientes Recurrentes (*Credit Ladder* y Renovación Rápida)
**Como** Cliente puntual o Analista de Riesgos,  
**Quiero** que el sistema evalúe automáticamente el comportamiento histórico de pago al cancelar o estar próximo a cancelar un préstamo para desbloquear un nivel superior de crédito (*Tier*),  
**Para** premiar la puntualidad con mayor línea aprobada, menor TEA y aprobación rápida (*Fast-Track*).

#### Requerimientos Funcionales
- **RF-45.1 (Niveles de Escalonamiento Configurables por Producto — *Tiers*):**
  - Cada producto o política global define niveles (`Tier 1: Nuevo`, `Tier 2: Recurrente Bronce`, `Tier 3: Recurrente Plata`, `Tier 4: Recurrente VIP`):
    - `max_amount_multiplier` o `tier_max_amount` (ej. S/ 1,000 → S/ 2,000 → S/ 3,500 → S/ 6,000).
    - `tea_discount_bps` (reducción de TEA en puntos básicos, ej. `-200 bps` / `-2.00%` en `Tier 2`).
    - `fast_track_eligible` (`true`/`false`: aprobación automática sin intervención humana si cumple DTI y buró `NORMAL`).
- **RF-45.2 (Reglas de Ascenso y Degradación de Nivel):**
  - Al pasar un préstamo a `CANCELADO` (o tras haber pagado $\ge 80\%$ de las cuotas de un préstamo vigente sin superar `max_days_past_due_for_upgrade`, ej. $\le 3$ días de atraso máximo histórico), el motor recalcula el `credit_tier` del cliente y genera una **Oferta Pre-Aprobada (`PreApprovedOffer`)** con fecha de vigencia (`expires_at`).
  - Si un cliente incurre en mora $> 15$ días en cualquier préstamo, pierde elegibilidad de ascenso y baja al `Tier 1` o queda bloqueado para renovación automática.
- **RF-45.3 (Endpoints de Oferta y Renovación):**
  - `GET /api/customers/{id}/pre-approved-offers` y `GET /api/portal/me/pre-approved-offers`.
  - `POST /api/portal/me/pre-approved-offers/{offerId}/accept`: crea la solicitud pre-aprobada (o de refinanciamiento limpio si liquida el saldo del préstamo vigente con el nuevo desembolso) y pasa directo a firma de contrato.

#### Criterios de Aceptación
- **CA-45.1 (Desbloqueo Automático de Tier 2 al Cancelar Puntualmente):**  
  *Dado que* un cliente `Tier 1` termina de pagar su primer préstamo de S/ 1,000 con un atraso máximo histórico de 0 días,  
  *Cuando* el préstamo cambia a estado `CANCELADO`,  
  *Entonces* el sistema actualiza su nivel a `Tier 2`, genera una oferta pre-aprobada hasta por S/ 2,000 con la TEA preferencial del nivel y la muestra en su Portal del Cliente.

---

### Pilar 2: Ingeniería de Producto y Cálculo Financiero V2

### US-46: Seguro de Desgravamen, Períodos de Gracia Inicial e ITF
**Como** Administrador Financiero,  
**Quiero** parametrizar en los productos el Seguro de Desgravamen sobre saldo insoluto, períodos de gracia inicial (total o parcial) y el cálculo del ITF,  
**Para** que los cronogramas y desembolsos reflejen la estructura real de costos de los productos de crédito peruanos y la TCEA exacta.

#### Requerimientos Funcionales
- **RF-46.1 (Seguro de Desgravamen Separado de Comisiones):**
  - Configuración en producto: `desgravamen_rate_monthly` (% mensual sobre saldo deudor, ej. $0.085\%$) o monto fijo, y `desgravamen_Fixed_cap`.
  - En cada cuota $k$ del cronograma, el seguro se calcula sobre el saldo capital insoluto del período ($S_{k-1}$) ajustado a los días del período:
    $$\text{Seguro}_k = \text{round}\left(S_{k-1} \times \text{TasaDesgravamen}_{\text{diaria}} \times \text{Días}_k, 2\right)$$
  -En el método Francés con seguro de desgravamen sobre saldo, la tasa periódica compuesta para hallar la cuota constante total ($C$) incorpora la tasa efectiva del período más la tasa de seguro del período ($i_{\text{total}} = \text{TEP} + d_{\text{período}}$), manteniendo la suma exacta de amortización de capital igual al principal.
- **RF-46.2 (Períodos de Gracia Inicial — *Grace Periods*):**
  - Parámetros en producto y solicitud: `initial_grace_periods` (0 a $N$ cuotas iniciales) y `initial_grace_type` (`NONE`, `PARTIAL`, `TOTAL`).
  - **Gracia Parcial (Gracia de Capital):** Durante las primeras $g$ cuotas ($k = 1 \dots g$), $\text{Capital}_k = 0$ y el cliente solo paga $\text{Interés}_k + \text{Seguro}_k + \text{Comisión}_k$. A partir de la cuota $g+1$, el capital original se amortiza en las $n - g$ cuotas restantes.
  - **Gracia Total (Diferimiento / Capitalización):** Durante las primeras $g$ cuotas ($k = 1 \dots g$), la cuota a pagar es $0.00$. Los intereses devengados y seguros de los períodos $1 \dots g$ se capitalizan (o se distribuyen linealmente según configuración `grace_interest_treatment`) sobre el saldo base que se amortiza entre las cuotas $g+1 \dots n$.
- **RF-46.3 (Impuesto a las Transacciones Financieras — ITF):**
  - Tasa parametrizable global (`itf_rate`, por defecto $0.005\%$ según ley peruana, truncado/redondeado según norma SUNAT).
  - Se desglosa informativa y contablemente tanto en el desembolso como en cada pago bancario, sin alterar la amortización del principal del cliente.

#### Criterios de Aceptación
- **CA-46.1 (Cronograma con 2 Cuotas de Gracia Parcial):**  
  *Dado que* se simula un préstamo de S/ 10,000 a 12 meses con `initial_grace_periods = 2` y `initial_grace_type = PARTIAL`,  
  *Cuando* el motor genera el cronograma,  
  *Entonces* las cuotas 1 y 2 tienen amortización de capital `S/ 0.00` y cobran únicamente interés + desgravamen, y las cuotas 3 a 12 amortizan exactamente los `S/ 10,000.00` de capital.

---

### US-47: Control Automático de Tasa Máxima BCRP (Ley N° 31143 — Ley contra la Usura)
**Como** Oficial de Cumplimiento o Administrador,  
**Quiero** que el sistema valide todas las tasas compensatorias (TEA) y moratorias configuradas en productos, simulaciones y contratos contra el tope máximo vigente publicado por el BCRP,  
**Para** impedir legal y técnicamente cualquier cobro que configure infracción de usura según la Ley N° 31143 y normativas SBS.

#### Requerimientos Funcionales
- **RF-47.1 (Tabla de Topes Regulatorios Vigentes por Semestre y Moneda):**
  - Entidad `regulatory_rate_caps`: `currency` (`PEN` / `USD`), `effective_from`, `effective_to`, `max_compensatory_tea` (ej. $101.86\%$ anual en PEN), `max_moratory_tea` (por ley en créditos de consumo y MYPES equivale hasta el $15\%$ de la tasa máxima compensatoria vigente), `source_resolution` (ej. Circular BCRP N° 00XX-2026-BCRP).
- **RF-47.2 (Validación Preventiva en Caliente):**
  - Al crear o editar un producto (`POST/PUT /api/products`), al simular (`POST /api/loan-applications/simulate`) o al generar un contrato para desembolso, el motor verifica:
    $$\text{TEA}_{\text{producto}} \le \text{TEA}_{\text{máx BCRP vigente}} \quad \land \quad \text{TasaMora}_{\text{anualizada}} \le \text{TopeMora}_{\text{BCRP vigente}}$$
  - Si algún parámetro excede el tope legal vigente a la fecha de la operación, el sistema bloquea la operación con `422 Unprocessable Entity` (`ERR_USURY_CAP_EXCEEDED`).

#### Criterios de Aceptación
- **CA-47.1 (Bloqueo de Producto que Excede el Tope BCRP):**  
  *Dado que* el tope máximo BCRP vigente en Soles (`PEN`) está configurado en $101.86\%$ TEA,  
  *Cuando* un administrador intenta guardar o activar un producto con `TEA = 110.00%`,  
  *Entonces* el sistema rechaza la petición con código `422` detallando el límite legal vigente y la circular del BCRP.

---

### Pilar 3: Negociación, Condonaciones y Acuerdos de Pago

### US-48: Condonación / Descuento de Mora e Intereses (*Waivers* y Campañas de Liquidación)
**Como** Gestor de Cobranza y Supervisor de Cobranzas,  
**Quiero** registrar propuestas de condonación parcial o total de intereses moratorios y compensatorios vencidos sujetas a un pago con fecha límite y aprobación de 4 ojos,  
**Para** recuperar capital en créditos con mora crítica que de otro modo terminarían en castigo total.

#### Requerimientos Funcionales
- **RF-48.1 (Solicitud de Condonación — *Waiver Request*):**
  - Endpoint `POST /api/loans/{id}/waivers`.
  - Permite especificar montos o porcentajes de descuento sobre:
    - Mora y penalidades devengadas (`waived_late_fee_amount` — hasta $100\%$).
    - Intereses compensatorios vencidos (`waived_interest_amount` — hasta el % máximo permitido por política).
    - Comisiones vencidas (`waived_fee_amount`).
    - **Regla Inviolable:** El capital principal (`principal`) no admite condonación operativa estándar (requiere flujo de castigo de directorio).
  - Toda solicitud define un `minimum_settlement_payment` (monto exacto que el cliente se compromete a pagar) y una fecha de caducidad (`valid_until`, ej. 72 horas).
- **RF-48.2 (Aprobación de 4 Ojos y Aplicación Condicionada al Pago):**
  - Si el descuento supera el umbral de autonomía del gestor (ej. $> 30\%$ de mora o cualquier descuento de interés), pasa a estado `PENDING_APPROVAL` requiriendo que un usuario distinto con rol `ROLE_ADMIN` o Supervisor apruebe en `POST /api/loans/{id}/waivers/{waiverId}/approve`.
  - **Efecto al Pagar:** Mientras el *waiver* esté `APPROVED` y vigente (`now <= valid_until`), la intención de pago y la cascada de prelación aplican el crédito compensatorio inmutable (`WAIVER_CREDIT`) reduciendo instantáneamente la mora/interés exonerado en el mismo asiento transaccional en que ingresa el pago acordado. Si llega `valid_until` y el cliente no pagó, el *waiver* pasa a `EXPIRED` y la deuda original permanece intacta.

#### Criterios de Aceptación
- **CA-48.1 (Ejecución de Condonación al Recibir el Pago Acordado):**  
  *Dado que* un préstamo en mora debe S/ 1,000 de capital, S/ 200 de interés y S/ 300 de mora (Total S/ 1,500), y tiene un *waiver* `APPROVED` vigente que exonera el $100\%$ de la mora (S/ 300) si el cliente abona S/ 1,200 antes del viernes,  
  *Cuando* el cliente realiza el pago de S/ 1,200 dentro del plazo,  
  *Entonces* el sistema asienta el contra-asiento de exoneración por S/ 300 en mora, aplica los S/ 1,200 a cubrir los S/ 200 de interés y S/ 1,000 de capital, y el préstamo pasa a estado `CANCELADO`.

---

### US-49: Convenios de Reestructuración con Cuota Inicial (*Down Payment*)
**Como** Analista de Riesgos o Gestor de Cobranza,  
**Quiero** condicionar la activación de una reestructuración a que el cliente pague primero una cuota inicial de buena fe (*down payment*),  
**Para** evitar reestructurar artificialmente préstamos (práctica de *evergreening*) sin compromiso real de pago del deudor.

#### Requerimientos Funcionales
- **RF-49.1 (Estado `PENDING_DOWN_PAYMENT` en Reestructuración):**
  - Al crear un acuerdo de reestructuración (`POST /api/loans/{id}/restructure`), se incluye el parámetro `required_down_payment_amount` ($\ge 0$) y `down_payment_due_date`.
  - Si `required_down_payment_amount > 0`, el acuerdo se guarda en estado `PENDING_DOWN_PAYMENT` y el préstamo original continúa en su estado actual (`EN_MORA` / `VIGENTE`) sin activar aún la versión `v+1` del cronograma.
- **RF-49.2 (Activación Automática tras Confirmación del Abono Inicial):**
  - Cuando ingresa un pago confirmado que cubre `required_down_payment_amount` antes de `down_payment_due_date`, el sistema aplica dicho pago según prelación, congela el saldo remanente resultante, genera y activa atómicamente la versión `v+1` del cronograma reestructurado y cambia el préstamo a `REESTRUCTURADO` (o `VIGENTE` normalizado).
  - Si vence el plazo sin abono, el acuerdo pasa a `CANCELLED_NON_PAYMENT`.

#### Criterios de Aceptación
- **CA-49.1 (Reestructuración Activada solo al Pagar Cuota Inicial):**  
  *Dado que* se aprueba reestructurar un préstamo vencido exigiendo un abono inicial de S/ 250 en un plazo de 48 horas,  
  *Cuando* se registra el acuerdo,  
  *Entonces* el cronograma `v2` permanece en estado `PENDING_DOWN_PAYMENT` y solo se activa como vigente en el instante en que se confirma el pago de S/ 250.

---

### Pilar 4: Pasarelas Avanzadas, Liquidación en Lote y Débito Automático

### US-50: Conciliación de Liquidaciones en Lote de Pasarelas (*Gateway Settlements*)
**Como** Operador de Tesorería,  
**Quiero** conciliar un depósito bancario único de una pasarela (Culqi, Niubiz, Izipay) contra los $N$ pagos individuales de clientes que lo componen, registrando la comisión retenida y su IGV,  
**Para** que el cuadre bancario cierre al centavo sin descuadrar la amortización del $100\%$ reconocida a cada cliente.

#### Requerimientos Funcionales
- **RF-50.1 (Regla de Separación entre Amortización al Cliente y Neto de Tesorería):**
  - Si un cliente paga S/ 500.00 con tarjeta vía pasarela, **al préstamo del cliente se le amortizan exactamente S/ 500.00** (`gross_amount`).
  - Cuando la pasarela liquida el lote ($T+1$ o $T+2$) y deposita en la cuenta bancaria de la empresa, transfiere el neto (`net_amount`), habiendo retenido su comisión (`gateway_fee_amount`) y el IGV de la comisión (`gateway_igv_amount`):
    $$\sum_{i=1}^{N} \text{GrossPayment}_i - \text{GatewayFee}_{\text{total}} - \text{IGV}_{\text{total}} = \text{NetBankDeposit}$$
- **RF-50.2 (Importación y Conciliación de Liquidación — *Settlement Batch*):**
  - Endpoint `POST /api/treasury/gateway-settlements`.
  - Recibe el detalle del lote de la pasarela (`provider`, `settlement_reference`, `settlement_date`, `bank_statement_line_id`, `gross_total`, `fee_total`, `igv_total`, `net_total` y la lista de `payment_ids` o `external_transaction_ids`).
  - Valida que la ecuación contable cierre exactamente ($\Delta = 0.00$), vincula los $N$ pagos con el movimiento del extracto bancario (`1 Extracto ↔ 1 Settlement ↔ N Pagos`) y marca tanto los pagos como la línea del extracto como `RECONCILED`.

#### Criterios de Aceptación
- **CA-50.1 (Conciliación 1 a N con Comisión de Pasarela):**  
  *Dado que* 10 clientes pagaron S/ 100.00 cada uno con tarjeta (Bruto = S/ 1,000.00) y la pasarela deposita en el BCP un abono único de S/ 960.00 reteniendo S/ 33.90 de comisión + S/ 6.10 de IGV,  
  *Cuando* se procesa la liquidación del lote contra la línea del extracto de S/ 960.00,  
  *Entonces* los 10 pagos de S/ 100.00 y el extracto de S/ 960.00 quedan conciliados con diferencia S/ 0.00 y se registra el gasto financiero de pasarela por S/ 40.00.

---

### US-51: Débito Automático / Cargo Recurrente con Tarjeta Tokenizada (*Auto-Pay*)
**Como** Cliente en el Portal de Autoservicio,  
**Quiero** afiliar mi tarjeta de débito o crédito mediante tokenización segura para que mis cuotas se cobren automáticamente el día de su vencimiento,  
**Para** evitar olvidos, ahorrar tiempo y no incurrir en cargos por mora.

#### Requerimientos Funcionales
- **RF-51.1 (Tokenización PCI-DSS Compliant y Mandato de Débito):**
  - El frontend captura la tarjeta usando el SDK JS de la pasarela y envía únicamente el `gateway_card_token`, marca (`VISA`/`MASTERCARD`), últimos 4 dígitos (`last4`), fecha de expiración (`exp_month`, `exp_year`) y el consentimiento firmado con OTP (`mandate_accepted_at`). **Nunca se almacenan PAN completos ni CVV.**
  - Endpoints: `POST /api/portal/me/autopay-methods`, `DELETE /api/portal/me/autopay-methods/{id}`, `PUT /api/portal/me/loans/{loanId}/autopay` (activar/desactivar débito automático en un préstamo).
- **RF-51.2 (Job de Cargo Recurrente y Política de Reintentos Inteligentes):**
  - Job diario `AutoPayChargeJob` (07:00 AM `America/Lima`) busca cuotas en estado `PENDING` o `PARTIAL` cuya `due_date == today` (o reintentos programados) de préstamos con `autopay_enabled = true`.
  - Genera una `PaymentIntent` idempotente (`AUTOPAY-{installment_id}-{attempt}`) y ejecuta el cargo contra `IPaymentGateway.ChargeTokenAsync`.
  - Si es rechazado por fondos insuficientes (`INSUFFICIENT_FUNDS`), programa hasta 2 reintentos automáticos ($T+1$ y $T+3$ dentro de los días de gracia) y notifica al cliente con enlace de pago alternativo por Yape/Plin. Si falla por tarjeta inválida/robada (`CARD_DECLINED_PERMANENT`), desactiva el `autopay` y alerta al cliente.

#### Criterios de Aceptación
- **CA-51.1 (Cobro Automático Exitoso en Fecha de Vencimiento):**  
  *Dado que* un cliente tiene activo el Débito Automático en su préstamo y hoy vence su cuota N° 3 por S/ 320.00,  
  *Cuando* se ejecuta el job matutino de *Auto-Pay*,  
  *Entonces* realiza el cargo por S/ 320.00 al token afiliado, aplica el pago en cascada, emite el comprobante electrónico y envía la constancia al cliente.

---

### Pilar 5: Facturación Electrónica (SUNAT / OSE) y Libro Mayor Contable

### US-52: Emisión Automática de Comprobantes Electrónicos SUNAT y Notas de Crédito
**Como** Contador o Cliente,  
**Quiero** que cada cobro de intereses, mora y comisiones genere automáticamente una Boleta o Factura Electrónica validada por SUNAT/OSE y que todo reverso de pago emita su Nota de Crédito Electrónica vinculada,  
**Para** cumplir con la legislación tributaria peruana y entregar comprobantes con validez fiscal.

#### Requerimientos Funcionales
- **RF-52.1 (Regla Tributaria de Base Imponible en Créditos):**
  - La devolución del **Capital Principal (`principal_paid`) NO es ingreso ni está afecta a comprobante de venta** (es retorno de mutuo dinerario).
  - El comprobante electrónico (Boleta `03` para DNI / persona natural sin Ruc o Factura `01` para RUC 20 / persona con negocio) se emite únicamente por los conceptos de ingreso financiero devengado/cobrado en la transacción:
    - `interest_paid` (Interés compensatorio).
    - `late_fee_paid` (Interés moratorio / penalidades).
    - `fee_paid` (Comisiones administrativas; afectas o inafectas de IGV según el régimen tributario configurado en el parámetro del producto/empresa: `TAX_EXEMPT_FINANCIAL` vs `TAXABLE_IGV_18`).
- **RF-52.2 (Integración Asíncrona vía Outbox con Proveedor OSE / PSE — UBL 2.1):**
  - Al asentarse un pago (`Payment`) cuya suma `interest_paid + late_fee_paid + fee_paid > 0`, se encola un evento en el Outbox hacia `IElectronicBillingGateway`.
  - Se genera el correlativo (`B001-00001234` o `F001-00000567`), se transmite el XML UBL 2.1 y se almacena el estado SUNAT (`PENDING_CDR`, `ACCEPTED`, `OBSERVED`, `REJECTED`), el hash CDR, el enlace al XML/CDR y el PDF representativo disponible en Backoffice y en el Portal del Cliente.
- **RF-52.3 (Nota de Crédito Electrónica Automática por Reverso):**
  - Cuando un pago con comprobante `ACCEPTED` es revertido (`POST /api/payments/{id}/reverse`), el sistema emite automáticamente una **Nota de Crédito Electrónica (Tipo `07`, motivo `01 - Anulación de la operación`)** referenciando la serie y número de la Boleta/Factura original.

#### Criterios de Aceptación
- **CA-52.1 (Emisión de Boleta solo por Intereses y Comisiones, Excluyendo Capital):**  
  *Dado que* un cliente con DNI paga una cuota total de S/ 450.00 compuesta por S/ 350.00 de capital, S/ 85.00 de interés y S/ 15.00 de comisión,  
  *Cuando* el pago queda confirmado,  
  *Entonces* el sistema emite una Boleta de Venta Electrónica `B001-XXXXXXXX` por un total de **S/ 100.00** (detallando S/ 85.00 de interés compensatorio y S/ 15.00 de comisión), dejando los S/ 350.00 de amortización de capital reflejados en el recibo/estado de cuenta financiero.
- **CA-52.2 (Emisión de Nota de Crédito al Revertir un Pago):**  
  *Dado que* el pago anterior cuenta con la Boleta `B001-00000120` aceptada por SUNAT,  
  *Cuando* un operador autorizado ejecuta el reverso del pago,  
  *Entonces* el sistema genera automáticamente la Nota de Crédito `BC01-00000015` vinculada a `B001-00000120` por S/ 100.00.

---

### US-53: Libro Mayor de Partida Doble (*Double-Entry Ledger*) y Provisiones de Cartera
**Como** Contador General o Auditor Financiero,  
**Quiero** que cada evento financiero genere asientos contables inmutables de partida doble (Debe = Haber) y que el sistema calcule mensualmente la provisión por incobrabilidad según el tramo de mora,  
**Para** exportar los libros contables cuadrados al ERP y reflejar el riesgo real de la cartera en los estados financieros.

#### Requerimientos Funcionales
- **RF-53.1 (Motor de Asientos Contables Inmutables — *Journal Entries*):**
  - Toda operación financiera confirmada (`DISBURSEMENT`, `PAYMENT`, `PAYMENT_REVERSAL`, `WAIVER`, `GATEWAY_SETTLEMENT`, `WRITE_OFF`, `REFUND`) genera un asiento `accounting_journal_entries` con $N \ge 2$ líneas (`accounting_journal_lines`) donde obligatoriamente:
    $$\sum \text{Debit} = \sum \text{Credit}$$
  - Mapeo de cuentas contables configurable por producto y moneda (Plan Contable General Empresarial / SBS): Cuentas por Cobrar Vigentes, Cuentas por Cobrar Vencidas, Bancos Tesorería, Pasarelas por Liquidar, Ingresos por Intereses, Ingresos por Mora, Comisiones de Pasarela, Provisiones de Cartera.
- **RF-53.2 (Cálculo de Provisión de Cartera por Tramo de Mora — Cierre Mensual):**
  - Endpoint `POST /api/accounting/provisions/calculate` y vista de reporte.
  - Clasifica cada préstamo activo según sus días de atraso (`days_past_due`) al corte de mes aplicando las tasas de provisión parametrizadas (alineadas a tramos SBS consumo/MYPE):
    - `0 - 8 días` (*Normal*): $1.00\%$ sobre saldo capital.
    - `9 - 30 días` (*CPP*): $5.00\%$ sobre saldo capital.
    - `31 - 60 días` (*Deficiente*): $25.00\%$ sobre saldo capital.
    - `61 - 120 días` (*Dudoso*): $60.00\%$ sobre saldo capital.
    - `> 120 días` (*Pérdida*): $100.00\%$ sobre saldo capital.

#### Criterios de Aceptación
- **CA-53.1 (Invariante de Partida Doble en Todo Asiento):**  
  *Dado que* se ejecuta cualquier desembolso, pago, reverso o liquidación de pasarela,  
  *Cuando* se persiste el asiento en el libro mayor,  
  *Entonces* la suma de cargos (`debit`) es idéntica al centavo a la suma de abonos (`credit`) y el asiento no puede editarse ni eliminarse.

---

### Pilar 6: Garantías Personales, Avales y Firma Mancomunada

### US-54: Vinculación de Aval / Fiador Solidario o Cónyuge Interviniente
**Como** Analista de Crédito,  
**Quiero** vincular uno o más intervinientes adicionales (Fiador Solidario / Aval o Cónyuge) a una solicitud de crédito cuando el monto supere el límite de sola firma o cuando el dictamen lo exija como condición,  
**Para** respaldar jurídicamente la recuperación del crédito con patrimonio solidario.

#### Requerimientos Funcionales
- **RF-54.1 (Reglas de Exigencia de Interviniente):**
  - El producto configura `guarantor_required_above_amount` (ej. obligatorio para solicitudes $> \text{S/ } 15,000$) y `spouse_signature_required_if_married` (`true`/`false` para régimen de sociedad de gananciales encima de un umbral).
  - El analista también puede dictaminar `APROBADO_CON_CONDICIONES` marcando el check `requires_guarantor = true`.
- **RF-54.2 (Expediente y Evaluación del Aval):**
  - Endpoint `POST /api/loan-applications/{id}/co-debtors`.
  - Registra el rol (`SOLIDARY_GUARANTOR` o `SPOUSE`), datos de identidad (DNI/CE, nombres, contacto, ingresos), verificación KYC y consulta al buró de crédito del aval (el aval debe tener clasificación SBS `NORMAL` y no tener mora interna activa).

#### Criterios de Aceptación
- **CA-54.1 (Bloqueo de Paso a Contrato si Falta el Aval Requerido):**  
  *Dado que* una solicitud fue aprobada con la condición `requires_guarantor = true` (o supera el monto de sola firma del producto),  
  *Cuando* se intenta generar el contrato o habilitar el desembolso sin un aval con KYC `VERIFIED` y buró `NORMAL`,  
  *Entonces* el sistema rechaza el paso con `422 Unprocessable Entity` indicando que falta completar y validar al fiador solidario.

---

### US-55: Firma Electrónica Mancomunada con Doble OTP (Titular + Aval / Cónyuge)
**Como** Titular del Crédito y Aval/Cónyuge Interviniente,  
**Quiero** revisar el contrato, la Hoja Resumen (con TCEA) y el pagaré incompleto digital y firmarlos electrónicamente cada uno con su propio código OTP enviado a su celular/correo verificado,  
**Para** perfeccionar el contrato con plenos efectos legales antes de que Tesorería ejecute el desembolso.

#### Requerimientos Funcionales
- **RF-55.1 (Flujo de Firma Multiparte — *Multi-Party Digital Signature*):**
  - Cuando una solicitud tiene intervinientes (`SOLIDARY_GUARANTOR` / `SPOUSE`), el contrato generado crea registros de firma individuales en `contract_signers` para el `PRIMARY_BORROWER` y para cada co-deudor.
  - Cada firmante recibe un enlace único y su propio código OTP de 6 dígitos (`POST /api/contracts/{id}/signers/{signerId}/send-otp` y `POST /api/contracts/{id}/signers/{signerId}/verify-otp`), registrando IP, `User-Agent`, `timestamp` UTC y el hash SHA-256 del PDF firmado.
- **RF-55.2 (Condición de Desembolso):**
  - El contrato permanece en estado `PARTIALLY_SIGNED` mientras falte al menos una firma. Solo cuando **todos** los firmantes requeridos pasan a `SIGNED`, el contrato cambia a `FULLY_SIGNED` y la solicitud queda habilitada para crear la orden de desembolso.

#### Criterios de Aceptación
- **CA-55.1 (Imposibilidad de Desembolsar con Firma Parcial):**  
  *Dado que* un crédito mancomunado requiere la firma del Titular y de su Fiador Solidario, y únicamente el Titular ha validado su OTP,  
  *Cuando* un operador intenta ejecutar el desembolso,  
  *Entonces* el sistema bloquea la operación indicando que el contrato se encuentra en estado `PARTIALLY_SIGNED` a la espera de la firma del Fiador Solidario.

---

## 3. Mapa de Endpoints REST API V2

| Método y Ruta | Módulo | Historia | Descripción |
|---|---|---|---|
| `POST /api/customers/{id}/credit-bureau-pull` | Customers / Origination | US-44 | Ejecuta o recupera consulta a Centrales de Riesgo (SBS/Equifax) |
| `GET /api/customers/{id}/pre-approved-offers` | Customers | US-45 | Lista ofertas pre-aprobadas vigentes del cliente (*Credit Ladder*) |
| `GET /api/portal/me/pre-approved-offers` | Portal | US-45 | Consulta de oferta de renovación rápida del cliente autenticado |
| `POST /api/portal/me/pre-approved-offers/{offerId}/accept` | Portal | US-45 | Aceptación 1-Click de oferta pre-aprobada |
| `GET /api/regulatory-caps` / `POST /api/regulatory-caps` | Products / Admin | US-47 | Gestión de topes de tasa máxima BCRP (Ley N° 31143) |
| `GET /api/loans/{id}/waivers` / `POST /api/loans/{id}/waivers` | Collections / Loans | US-48 | Solicitud y listado de condonaciones (*Waivers*) de mora/interés |
| `POST /api/loans/{id}/waivers/{waiverId}/approve` | Collections / Loans | US-48 | Aprobación o rechazo de 4 ojos de una condonación |
| `POST /api/treasury/gateway-settlements` | Treasury | US-50 | Registro y conciliación en lote (`1 ↔ N`) de liquidación de pasarela |
| `GET /api/portal/me/autopay-methods` / `POST` / `DELETE` | Portal / Integrations | US-51 | Afiliación y baja de tarjetas tokenizadas para Débito Automático |
| `PUT /api/portal/me/loans/{loanId}/autopay` | Portal / Loans | US-51 | Activar/desactivar cargo automático recurrente en un préstamo |
| `GET /api/tax-vouchers` / `GET /api/payments/{id}/tax-voucher` | Accounting / Tax | US-52 | Consulta y descarga de XML/CDR/PDF de Boletas, Facturas y Notas de Crédito SUNAT |
| `POST /api/tax-vouchers/{id}/retry` | Accounting / Tax | US-52 | Reenvío manual a OSE/SUNAT de comprobante observado o en contingencia |
| `GET /api/accounting/journal-entries` | Accounting | US-53 | Libro mayor de asientos contables de partida doble |
| `POST /api/accounting/provisions/calculate` | Accounting | US-53 | Ejecución de cálculo de provisión mensual de cartera por tramo SBS |
| `POST /api/loan-applications/{id}/co-debtors` | Origination | US-54 | Vinculación y evaluación KYC/Buró de aval o cónyuge |
| `POST /api/contracts/{id}/signers/{signerId}/verify-otp` | Loans / Portal | US-55 | Firma electrónica OTP individual por cada interviniente del contrato |
