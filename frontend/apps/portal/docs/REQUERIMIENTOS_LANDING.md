# Requerimientos — Landing Page Pública (`/`)

Página de bienvenida para **visitantes no autenticados**. Es el primer punto de contacto
del cliente potencial con la fintech. Debe comunicar confianza, transparencia y velocidad
del proceso 100% digital.

**Referencia de inspiración:** prestamype.com (estructura general, no diseño).
**Ruta:** `/` — redirige al Home del cliente si ya tiene sesión activa.
**ID de requerimientos:** `FE-LP-NN`
**Fase:** todos son `MVP` salvo indicación.

---

## 1. Estructura General de Secciones

La landing se compone de las siguientes secciones en orden vertical:

```
┌─────────────────────────────────┐
│  1. Hero                        │  Propuesta de valor + CTA principal
├─────────────────────────────────┤
│  2. Cómo funciona               │  3 pasos del proceso digital
├─────────────────────────────────┤
│  3. Simulador público           │  Monto / plazo → cuota estimada
├─────────────────────────────────┤
│  4. Beneficios                  │  Diferenciadores vs. entidades tradicionales
├─────────────────────────────────┤
│  5. Requisitos                  │  Qué necesita el cliente para aplicar
├─────────────────────────────────┤
│  6. Transparencia financiera    │  TEA, TCEA, mora — sin letra chica
├─────────────────────────────────┤
│  7. Preguntas frecuentes        │  Acordeón con FAQs
├─────────────────────────────────┤
│  8. CTA final                   │  Llamada a registrarse
├─────────────────────────────────┤
│  9. Footer legal                │  Datos empresa, Libro de Reclamaciones
└─────────────────────────────────┘
```

---

## 2. Requerimientos por Sección

### 2.1 Header Público

| ID | Requerimiento |
|---|---|
| FE-LP-01 | El header en modo no autenticado muestra solo logo + botones **Iniciar Sesión** y **Crear Cuenta** (sin la navegación de cliente) |
| FE-LP-02 | En mobile el header es sticky con fondo sólido; los botones colapsan en un menú hamburguesa si no caben |
| FE-LP-03 | Si el usuario ya tiene sesión activa, el header muestra su nombre y botón **Mi Portal**; la landing redirige automáticamente al `/inicio` del cliente |

---

### 2.2 Sección Hero

| ID | Requerimiento |
|---|---|
| FE-LP-04 | Titular principal con la propuesta de valor: préstamo personal 100% digital, desembolso en minutos |
| FE-LP-05 | Subtítulo breve con los beneficios clave: sin filas, sin papeles, 100% online |
| FE-LP-06 | Dos CTAs primarios prominentes: **Solicitar Préstamo** (→ `/registro`) y **Ver cómo funciona** (scroll a sección 2.2) |
| FE-LP-07 | Indicadores de confianza en línea bajo los CTAs: N° de clientes atendidos · Monto desembolsado · Calificación de satisfacción (datos del backend o hardcodeados en MVP) |
| FE-LP-08 | Imagen/ilustración representativa del proceso digital (mobile con préstamo aprobado) — puede ser SVG inline |

---

### 2.3 Sección Cómo Funciona

| ID | Requerimiento |
|---|---|
| FE-LP-09 | Exactamente **3 pasos** explicados visualmente con ícono, número y descripción corta |
| FE-LP-10 | Paso 1: **Regístrate y verifica tu identidad** — foto de DNI y selfie desde el celular (KYC) |
| FE-LP-11 | Paso 2: **Simula y solicita tu crédito** — elige monto y plazo, adjunta tu boleta de pago |
| FE-LP-12 | Paso 3: **Recibe tu dinero** — aprobación en minutos, desembolso a tu cuenta o billetera |
| FE-LP-13 | Tiempo estimado total visible: "Desde el registro hasta el dinero en tu cuenta: menos de 24 horas" |

---

### 2.4 Simulador Público

| ID | Requerimiento |
|---|---|
| FE-LP-14 | Simulador interactivo con slider de **monto** y selector de **plazo** (en meses) accesible sin iniciar sesión |
| FE-LP-15 | El cálculo lo hace el backend (`POST /api/portal/simulate`); se muestra cuota estimada, TEA y TCEA |
| FE-LP-16 | Si el backend no responde, mostrar cuota aproximada con indicador "Estimación referencial" sin bloquear la UX |
| FE-LP-17 | Botón **Solicitar este préstamo** bajo el resultado redirige a `/registro` pre-llenando el monto y plazo en el query (`?amount=X&term=Y`) |
| FE-LP-18 | Aviso legal visible: "El cálculo es referencial. La tasa final depende de la evaluación crediticia" |

---

### 2.5 Sección Beneficios

| ID | Requerimiento |
|---|---|
| FE-LP-19 | Grid de **4 a 6 tarjetas** con los diferenciadores del producto |
| FE-LP-20 | Tarjetas mínimas: Sin garantías · Respuesta en minutos · Sin penalidad por prepago · Proceso 100% digital · Transparencia (TEA/TCEA visibles) · Cancelación anticipada sin costo |
| FE-LP-21 | Cada tarjeta tiene ícono, título corto y descripción de 1 línea |

---

### 2.6 Sección Requisitos

| ID | Requerimiento |
|---|---|
| FE-LP-22 | Lista clara de **qué necesita** el cliente para solicitar: DNI vigente · Celular con cámara · Número de cuenta bancaria o billetera · Boleta de pago o recibo por honorarios |
| FE-LP-23 | Rango de edad elegible visible (ej. 18 a 70 años) según la configuración del producto |
| FE-LP-24 | Indicar que el proceso no requiere ir a ninguna oficina ni imprimir documentos |

---

### 2.7 Sección Transparencia Financiera

| ID | Requerimiento |
|---|---|
| FE-LP-25 | Tabla o tarjetas con un ejemplo concreto de préstamo: monto · plazo · cuota · TEA · TCEA · total a pagar |
| FE-LP-26 | Explicación en lenguaje simple de qué es la **TEA** y qué es la **TCEA** |
| FE-LP-27 | Mención a la normativa SBS: "Supervisados por la SBS — Ley N° 26702" |
| FE-LP-28 | Mención de que no hay penalidad por pago anticipado (Código de Protección al Consumidor, Ley 29571) |

---

### 2.8 Preguntas Frecuentes (FAQ)

| ID | Requerimiento |
|---|---|
| FE-LP-29 | Sección de acordeón con **6 a 8 preguntas frecuentes** expandibles |
| FE-LP-30 | Preguntas mínimas: ¿Cuánto tiempo tarda el desembolso? · ¿Qué pasa si no puedo pagar una cuota? · ¿Puedo pagar antes del plazo? · ¿Mis datos están seguros? · ¿Qué es la TCEA? · ¿Cómo verifican mi identidad? |
| FE-LP-31 | Una sola pregunta abierta a la vez (acordeón exclusivo) |

---

### 2.9 CTA Final

| ID | Requerimiento |
|---|---|
| FE-LP-32 | Bloque de fondo destacado (color de marca) con titular motivacional y un único botón grande **Solicitar mi Préstamo Ahora** (→ `/registro`) |
| FE-LP-33 | Frase de seguridad bajo el botón: "Sin costos ocultos · Sin letra chica · 100% digital" |

---

### 2.10 Footer Legal

| ID | Requerimiento |
|---|---|
| FE-LP-34 | Nombre, RUC y dirección de la empresa |
| FE-LP-35 | Enlace a **Libro de Reclamaciones Virtual** (`/libro-de-reclamaciones`) — Ley 29571 |
| FE-LP-36 | Enlace a **Política de Privacidad** y **Términos y Condiciones** (pueden ser modales o rutas separadas) |
| FE-LP-37 | Aviso de supervisión: "Empresa supervisada por la SBS" |
| FE-LP-38 | Copyright y año |

---

## 3. Comportamiento y Routing

| ID | Requerimiento |
|---|---|
| FE-LP-39 | La ruta `/` carga la `LandingPage` si el usuario **no** tiene sesión; si tiene sesión redirige a `/inicio` |
| FE-LP-40 | La landing **no** usa el `AppLayout` (sin header de cliente, sin bottom nav); tiene su propio layout o no-layout |
| FE-LP-41 | Los enlaces **Iniciar Sesión** apuntan a `/login`; **Crear Cuenta** / **Solicitar** apuntan a `/registro` |
| FE-LP-42 | El parámetro `?amount=X&term=Y` pasado desde el simulador público debe ser leído por `ApplyPage` y pre-poblar el simulador de la solicitud |

---

## 4. Requerimientos No Funcionales

| ID | Requerimiento |
|---|---|
| FE-LP-43 | La landing carga sin JavaScript crítico bloqueante; skeleton visible mientras carga el simulador |
| FE-LP-44 | Responsive completo: 360 px (mobile) · 768 px (tablet) · 1280 px (desktop) |
| FE-LP-45 | Sección activa del header se resalta con scroll spy (opcional, si se implementa navegación interna) |
| FE-LP-46 | Animaciones suaves de entrada por sección (fade-in al hacer scroll); no deben bloquear la lectura |

---

## 5. Archivos a Crear

```
src/
  features/
    landing/
      pages/
        LandingPage.vue          ← página principal pública
      components/
        HeroSection.vue          ← sección 1
        HowItWorksSection.vue    ← sección 2
        PublicSimulator.vue      ← sección 3
        BenefitsSection.vue      ← sección 4
        RequirementsSection.vue  ← sección 5
        TransparencySection.vue  ← sección 6
        FaqSection.vue           ← sección 7
        CtaSection.vue           ← sección 8
        LandingFooter.vue        ← sección 9
      routes.ts
      index.ts
  app/
    layouts/
      PublicLayout.vue           ← layout sin nav de cliente (header público + footer)
```

---

## 6. Resumen de Cobertura

| Sección | Requerimientos | Fase | Estado |
|---|---|---|---|
| Header público | FE-LP-01 a 03 | MVP | ✅ Implementado (`PublicLayout.vue`) |
| Hero | FE-LP-04 a 08 | MVP | ✅ Implementado (`LandingPage.vue`) |
| Cómo funciona | FE-LP-09 a 13 | MVP | ✅ Implementado (`LandingPage.vue`) |
| Simulador público | FE-LP-14 a 18 | MVP | ✅ Implementado (`LandingPage.vue`) |
| Beneficios | FE-LP-19 a 21 | MVP | ✅ Implementado (`LandingPage.vue`) |
| Requisitos | FE-LP-22 a 24 | MVP | ✅ Implementado (`LandingPage.vue`) |
| Transparencia | FE-LP-25 a 28 | MVP | ✅ Implementado (`LandingPage.vue`) |
| FAQ | FE-LP-29 a 31 | MVP | ✅ Implementado (`LandingPage.vue`) |
| CTA Final | FE-LP-32 a 33 | MVP | ✅ Implementado (`LandingPage.vue`) |
| Footer legal | FE-LP-34 a 38 | MVP | ✅ Implementado (`LandingPage.vue`) |
| Routing | FE-LP-39 a 42 | MVP | ✅ Implementado (`router/index.ts`, `loans/routes.ts`) |
| No funcionales | FE-LP-43 a 46 | MVP | ✅ Responsive · Tailwind · Sin bloqueantes |
| **Total** | **46** | **MVP** | **46 / 46** |
