<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  ArrowRight,
  BadgeCheck,
  Banknote,
  Camera,
  CheckCircle2,
  ChevronDown,
  Clock,
  CreditCard,
  FileSearch,
  FileText,
  Lock,
  Minus,
  Plus,
  Shield,
  ShieldCheck,
  Smartphone,
  Sparkles,
  TrendingDown,
  Wallet,
  Zap,
} from '@lucide/vue'

const router = useRouter()

// ── Simulador público (FE-LP-14 a 18) ──────────────────────────────────────
const simAmount = ref(3000)
const simTerm = ref(12)

// Cálculo estimado client-side como fallback (FE-LP-16)
const simResult = computed(() => {
  const principal = simAmount.value
  const months = simTerm.value
  // TEA referencial fija para estimación pública
  const teaRef = 0.72
  const tem = Math.pow(1 + teaRef, 1 / 12) - 1
  const installment = (principal * tem) / (1 - Math.pow(1 + tem, -months))
  const total = installment * months
  const tceaRef = teaRef * 1.08 // TCEA estimada con comisiones
  return {
    installment: installment.toFixed(2),
    total: total.toFixed(2),
    tea: `${(teaRef * 100).toFixed(0)}%`,
    tcea: `${(tceaRef * 100).toFixed(0)}%`,
  }
})

function goToRegisterWithSim() {
  void router.push({
    path: '/registro',
    query: { amount: simAmount.value, term: simTerm.value },
  })
}

// ── FAQ acordeón (FE-LP-29 a 31) ──────────────────────────────────────────
const openFaq = ref<number | null>(null)
function toggleFaq(i: number) {
  openFaq.value = openFaq.value === i ? null : i
}

const faqs = [
  {
    q: '¿Cuánto tiempo tarda el desembolso?',
    a: 'Una vez aprobada tu solicitud y firmado el contrato digitalmente, el dinero llega a tu cuenta bancaria o billetera en minutos. El proceso completo desde el registro hasta el desembolso es en menos de 24 horas.',
  },
  {
    q: '¿Qué pasa si no puedo pagar una cuota?',
    a: 'Te recomendamos contactarnos antes de la fecha de vencimiento. Podemos evaluar una reprogramación. Si la cuota vence, se generará una mora diaria sobre el saldo vencido conforme a la tasa pactada en tu contrato.',
  },
  {
    q: '¿Puedo pagar antes del plazo?',
    a: 'Sí, tienes derecho a cancelar tu crédito anticipadamente en cualquier momento sin penalidad, según el Código de Protección al Consumidor (Ley 29571). Solo pagas el capital más los intereses devengados hasta la fecha.',
  },
  {
    q: '¿Mis datos personales están seguros?',
    a: 'Absolutamente. Tus datos se cifran con TLS 1.3 y se almacenan conforme a la Ley N° 29733 de Protección de Datos Personales. No compartimos tu información con terceros sin tu autorización expresa.',
  },
  {
    q: '¿Qué es la TCEA y en qué se diferencia de la TEA?',
    a: 'La TEA (Tasa Efectiva Anual) es el costo del dinero prestado en un año. La TCEA (Tasa de Costo Efectivo Anual) incluye además los cargos y comisiones del préstamo, representando el costo total real del crédito para el consumidor.',
  },
  {
    q: '¿Cómo verifican mi identidad?',
    a: 'Usamos tecnología de reconocimiento biométrico: tomamos foto de tu DNI (anverso y reverso) y una selfie con prueba de vida desde tu celular. El proceso es automático y tarda menos de 2 minutos.',
  },
  {
    q: '¿Necesito un aval o garantía?',
    a: 'No. Nuestros préstamos son sin garantías hipotecarias ni avales. La evaluación es crediticia y se basa en tu historial de pagos, ingresos y la consulta a centrales de riesgo.',
  },
]

// ── Beneficios (FE-LP-19 a 21) ─────────────────────────────────────────────
const benefits = [
  { icon: Zap, title: 'Desembolso en minutos', desc: 'Aprobación y transferencia automática sin esperas.' },
  { icon: Smartphone, title: '100% desde tu celular', desc: 'Sin filas, sin papeles, sin salir de tu casa.' },
  { icon: TrendingDown, title: 'Sin penalidad por prepago', desc: 'Cancela cuando quieras y ahorra intereses futuros.' },
  { icon: ShieldCheck, title: 'Transparencia total', desc: 'TEA y TCEA visibles antes de firmar. Sin letra chica.' },
  { icon: Banknote, title: 'Sin garantías ni avales', desc: 'Préstamos personales basados solo en tu perfil.' },
  { icon: Lock, title: 'Datos 100% seguros', desc: 'Cifrado bancario TLS 1.3 y Ley N° 29733.' },
]
</script>

<template>
  <div class="font-sans text-slate-900">

    <!-- ══════════════════════════════════════════════════════════
         SECCIÓN 1: HERO (FE-LP-04 a 08)
         ══════════════════════════════════════════════════════════ -->
    <section class="relative bg-[#070F1E] text-white overflow-hidden pt-6 pb-16 lg:pt-12 lg:pb-20">
      <!-- Fondo con glow y rejilla sutil -->
      <div class="absolute inset-0 bg-[radial-gradient(ellipse_80%_60%_at_50%_-15%,rgba(37,99,235,0.22),rgba(255,255,255,0))] pointer-events-none"></div>
      <div class="absolute inset-0 bg-[linear-gradient(to_right,#ffffff05_1px,transparent_1px),linear-gradient(to_bottom,#ffffff05_1px,transparent_1px)] bg-[size:3.5rem_3.5rem] pointer-events-none"></div>

      <div class="relative max-w-6xl mx-auto px-4 lg:px-8">
        <div class="grid lg:grid-cols-2 gap-12 lg:gap-8 items-center">

          <!-- Texto principal -->
          <div class="space-y-6">
            <div class="inline-flex items-center gap-2 bg-blue-950/70 border border-blue-500/30 rounded-full px-4 py-1.5 text-xs font-semibold text-blue-400 tracking-wide uppercase shadow-sm">
              <Zap class="w-3.5 h-3.5 fill-blue-400" />
              Préstamos 100% Digitales · Sin Filas
            </div>

            <h1 class="text-4xl sm:text-5xl lg:text-6xl font-black leading-[1.08] tracking-tight text-white">
              Tu préstamo<br />
              aprobado<br />
              <span class="text-sky-400">en minutos, desde tu</span><br />
              <span class="text-sky-400">celular</span>
            </h1>

            <p class="text-base sm:text-lg text-slate-300 max-w-lg leading-relaxed">
              Solicita hasta <strong class="text-white font-bold">S/ 20,000</strong> sin garantías, sin papeles y sin ir a ninguna oficina.
              Desembolsamos directo a tu cuenta bancaria o billetera digital.
            </p>

            <!-- CTAs principales (FE-LP-06) -->
            <div class="flex flex-col sm:flex-row gap-3 pt-2">
              <RouterLink
                to="/registro"
                class="inline-flex items-center justify-center gap-2 px-7 py-3.5 rounded-xl bg-brand-primary hover:bg-brand-primary-dark text-white font-bold text-sm transition-all shadow-lg shadow-blue-600/30 hover:shadow-xl hover:-translate-y-0.5"
              >
                <Zap class="w-4 h-4 fill-white" />
                Solicitar mi Préstamo →
              </RouterLink>
              <a
                href="#como-funciona"
                class="inline-flex items-center justify-center gap-2 px-6 py-3.5 rounded-xl border border-slate-700 bg-slate-850/60 hover:bg-slate-800 text-white font-semibold text-sm transition-colors"
              >
                Ver cómo funciona
                <ChevronDown class="w-4 h-4" />
              </a>
            </div>

            <!-- Indicadores de confianza (FE-LP-07) -->
            <div class="flex flex-wrap items-center gap-5 pt-3 text-sm text-slate-300 font-medium">
              <div class="flex items-center gap-1.5">
                <CheckCircle2 class="w-4 h-4 text-emerald-400" />
                <span>+5,000 clientes</span>
              </div>
              <div class="flex items-center gap-1.5">
                <CheckCircle2 class="w-4 h-4 text-emerald-400" />
                <span>S/ 15M desembolsados</span>
              </div>
              <div class="flex items-center gap-1.5">
                <CheckCircle2 class="w-4 h-4 text-emerald-400" />
                <span>4.8 ★ satisfacción</span>
              </div>
            </div>
          </div>

          <!-- Mockup Celular CrediYa (FE-LP-08) -->
          <div class="flex justify-center lg:justify-end">
            <div class="relative w-full max-w-[340px]">
              <!-- Badge flotante -->
              <div class="absolute -top-3 -right-2 z-20 bg-emerald-500 text-white text-xs font-bold px-3 py-1.5 rounded-full shadow-lg flex items-center gap-1.5">
                <Zap class="w-3.5 h-3.5 fill-white" />
                En &lt; 24 h
              </div>

              <!-- Marco del celular -->
              <div class="bg-[#0B1324] border-[3px] border-[#1E293B] rounded-[2.8rem] p-3 shadow-2xl shadow-blue-950/60 ring-1 ring-white/10">
                <!-- Speaker notch -->
                <div class="flex justify-center mb-2.5">
                  <div class="w-24 h-3.5 bg-slate-800/90 rounded-full flex items-center justify-center gap-2">
                    <div class="w-2 h-2 rounded-full bg-slate-950 border border-slate-700"></div>
                    <div class="w-1.5 h-1.5 rounded-full bg-emerald-400"></div>
                  </div>
                </div>

                <!-- Pantalla del celular -->
                <div class="bg-white rounded-[2rem] p-6 text-slate-900 shadow-xl space-y-4">
                  <!-- Header evaluación -->
                  <div class="flex items-center justify-between text-xs">
                    <span class="font-bold text-slate-800">Evaluación digital</span>
                    <span class="inline-flex items-center gap-1 text-[11px] font-bold text-emerald-600 bg-emerald-50 px-2.5 py-0.5 rounded-full border border-emerald-200/80">
                      <span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span> Listo
                    </span>
                  </div>

                  <!-- Aprobado con círculo check -->
                  <div class="text-center space-y-1.5 py-2">
                    <div class="mx-auto w-16 h-16 rounded-full bg-emerald-50 border-2 border-emerald-200/80 flex items-center justify-center text-emerald-500 shadow-xs">
                      <CheckCircle2 class="w-9 h-9 text-emerald-500" />
                    </div>
                    <p class="font-black text-2xl text-slate-900">¡Aprobado!</p>
                    <p class="text-xs text-slate-500">Tu préstamo está listo para transferir</p>
                  </div>

                  <!-- Datos del préstamo -->
                  <div class="divide-y divide-slate-100 text-xs">
                    <div class="flex justify-between py-2.5 text-slate-500">
                      <span>Monto aprobado</span>
                      <span class="font-bold text-slate-900 text-sm">S/ 5,000</span>
                    </div>
                    <div class="flex justify-between py-2.5 text-slate-500">
                      <span>Cuota mensual</span>
                      <span class="font-bold text-brand-primary text-sm">S/ 487.50</span>
                    </div>
                    <div class="flex justify-between py-2.5 text-slate-500">
                      <span>Plazo</span>
                      <span class="font-semibold text-slate-900">12 meses</span>
                    </div>
                  </div>

                  <!-- CTA botón -->
                  <RouterLink
                    to="/registro"
                    class="block w-full bg-brand-primary hover:bg-brand-primary-dark text-white font-bold text-sm py-3 rounded-xl text-center transition-all shadow-md shadow-blue-500/25"
                  >
                    Firmar y recibir dinero →
                  </RouterLink>

                  <!-- Nota de seguridad -->
                  <p class="text-[10px] text-slate-400 text-center flex items-center justify-center gap-1">
                    <Lock class="w-3 h-3 text-slate-400" />
                    Transferencia protegida por token digital
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- ══════════════════════════════════════════════════════════
             3 TARJETAS DESTACADAS (VALUE PROPS) EN FONDO OSCURO
             ══════════════════════════════════════════════════════════ -->
        <div class="mt-16 pt-12 border-t border-white/10 grid sm:grid-cols-3 gap-6">
          <!-- Card 1 -->
          <div class="flex items-start gap-4">
            <div class="w-12 h-12 rounded-2xl bg-blue-950/80 border border-blue-500/30 text-blue-400 flex items-center justify-center shrink-0 shadow-sm">
              <Clock class="w-6 h-6" />
            </div>
            <div class="space-y-1">
              <h3 class="font-bold text-white text-base">Evaluación en 3 Minutos</h3>
              <p class="text-xs sm:text-sm text-slate-400 leading-relaxed">
                Algoritmo inteligente de scoring crediticio sin papeleos ni esperas telefónicas.
              </p>
            </div>
          </div>

          <!-- Card 2 -->
          <div class="flex items-start gap-4">
            <div class="w-12 h-12 rounded-2xl bg-emerald-950/80 border border-emerald-500/30 text-emerald-400 flex items-center justify-center shrink-0 shadow-sm">
              <ShieldCheck class="w-6 h-6" />
            </div>
            <div class="space-y-1">
              <h3 class="font-bold text-white text-base">Sin Avales ni Garantías</h3>
              <p class="text-xs sm:text-sm text-slate-400 leading-relaxed">
                Créditos de libre disponibilidad con tasas transparentes y sin comisiones ocultas.
              </p>
            </div>
          </div>

          <!-- Card 3 -->
          <div class="flex items-start gap-4">
            <div class="w-12 h-12 rounded-2xl bg-indigo-950/80 border border-indigo-500/30 text-indigo-400 flex items-center justify-center shrink-0 shadow-sm">
              <Wallet class="w-6 h-6" />
            </div>
            <div class="space-y-1">
              <h3 class="font-bold text-white text-base">Abono Inmediato</h3>
              <p class="text-xs sm:text-sm text-slate-400 leading-relaxed">
                Recepción directa por transferencia bancaria interbancaria o billeteras Yape/Plin.
              </p>
            </div>
          </div>
        </div>

      </div>
    </section>

    <!-- ══════════════════════════════════════════════════════════
         SECCIÓN 2: CÓMO FUNCIONA (FE-LP-09 a 13)
         ══════════════════════════════════════════════════════════ -->
    <section id="como-funciona" class="scroll-mt-16 py-16 lg:py-24 bg-slate-50">
      <div class="max-w-6xl mx-auto px-4 lg:px-8">
        <div class="text-center mb-14">
          <p class="text-xs font-bold uppercase tracking-widest text-brand-primary mb-2">Proceso simple</p>
          <h2 class="text-2xl sm:text-3xl lg:text-4xl font-extrabold text-slate-900">¿Cómo funciona?</h2>
          <p class="mt-2 text-slate-500 text-sm sm:text-base">3 pasos desde tu celular. Sin filas, sin papeles.</p>
        </div>

        <div class="grid sm:grid-cols-3 gap-6 lg:gap-8">
          <!-- Paso 1 (FE-LP-10) -->
          <div class="bg-white rounded-3xl p-6 sm:p-8 border border-slate-200/80 shadow-xs hover:shadow-md transition-all text-center space-y-4">
            <div class="mx-auto w-16 h-16 rounded-2xl bg-blue-50 border border-brand-primary/20 flex items-center justify-center relative">
              <Camera class="w-8 h-8 text-brand-primary" />
              <span class="absolute -top-2 -right-2 w-6 h-6 rounded-full bg-brand-primary text-white text-xs font-bold flex items-center justify-center shadow-xs">1</span>
            </div>
            <div class="space-y-1.5">
              <h3 class="font-bold text-slate-900 text-base">Regístrate y verifica</h3>
              <p class="text-xs sm:text-sm text-slate-500 leading-relaxed">
                Foto de tu DNI y selfie con prueba de vida desde tu celular. Validación automática con RENIEC.
              </p>
            </div>
          </div>

          <!-- Paso 2 (FE-LP-11) -->
          <div class="bg-white rounded-3xl p-6 sm:p-8 border border-slate-200/80 shadow-xs hover:shadow-md transition-all text-center space-y-4">
            <div class="mx-auto w-16 h-16 rounded-2xl bg-sky-50 border border-brand-accent/40 flex items-center justify-center relative">
              <FileSearch class="w-8 h-8 text-brand-navy" />
              <span class="absolute -top-2 -right-2 w-6 h-6 rounded-full bg-brand-navy text-white text-xs font-bold flex items-center justify-center shadow-xs">2</span>
            </div>
            <div class="space-y-1.5">
              <h3 class="font-bold text-slate-900 text-base">Simula y solicita</h3>
              <p class="text-xs sm:text-sm text-slate-500 leading-relaxed">
                Elige el monto y plazo que necesitas. Adjunta tu última boleta de pago o recibo por honorarios.
              </p>
            </div>
          </div>

          <!-- Paso 3 (FE-LP-12) -->
          <div class="bg-white rounded-3xl p-6 sm:p-8 border border-slate-200/80 shadow-xs hover:shadow-md transition-all text-center space-y-4">
            <div class="mx-auto w-16 h-16 rounded-2xl bg-emerald-50 border border-emerald-500/20 flex items-center justify-center relative">
              <Wallet class="w-8 h-8 text-emerald-600" />
              <span class="absolute -top-2 -right-2 w-6 h-6 rounded-full bg-emerald-600 text-white text-xs font-bold flex items-center justify-center shadow-xs">3</span>
            </div>
            <div class="space-y-1.5">
              <h3 class="font-bold text-slate-900 text-base">Recibe tu dinero</h3>
              <p class="text-xs sm:text-sm text-slate-500 leading-relaxed">
                Firma el contrato digitalmente con tu OTP. El dinero llega a tu cuenta o billetera en minutos.
              </p>
            </div>
          </div>
        </div>

        <!-- Tiempo estimado (FE-LP-13) -->
        <div class="mt-10 mx-auto max-w-xl bg-white border border-brand-primary/20 rounded-2xl px-6 py-4 shadow-xs">
          <div class="flex items-center justify-center gap-3 text-center sm:text-left">
            <div class="w-9 h-9 rounded-xl bg-brand-primary/10 text-brand-primary flex items-center justify-center shrink-0">
              <Clock class="w-5 h-5" />
            </div>
            <p class="text-xs sm:text-sm font-medium text-slate-700 m-0">
              <span>Desde el registro hasta el dinero en tu cuenta:</span>
              <strong class="text-brand-navy font-bold ml-1.5 whitespace-nowrap">menos de 24 horas</strong>
            </p>
          </div>
        </div>
      </div>
    </section>

    <!-- ══════════════════════════════════════════════════════════
         SECCIÓN 3: SIMULADOR PÚBLICO (FE-LP-14 a 18)
         ══════════════════════════════════════════════════════════ -->
    <section id="simulador" class="scroll-mt-16 py-16 lg:py-20 bg-white">
      <div class="max-w-4xl mx-auto px-4 lg:px-8">
        <div class="text-center mb-10">
          <p class="text-xs font-bold uppercase tracking-widest text-brand-primary mb-2">Simulador</p>
          <h2 class="text-2xl sm:text-3xl font-extrabold text-slate-900">¿Cuánto necesitas?</h2>
          <p class="mt-2 text-slate-500 text-sm">Mueve los controles y ve tu cuota estimada al instante.</p>
        </div>

        <div class="bg-slate-50 border border-slate-200 rounded-3xl p-6 sm:p-8 shadow-sm">
          <div class="grid sm:grid-cols-2 gap-8">
            <!-- Controles -->
            <div class="space-y-6">
              <!-- Monto -->
              <div class="space-y-2">
                <div class="flex justify-between items-center">
                  <label class="text-sm font-semibold text-slate-700">Monto que necesitas</label>
                  <span class="text-xl font-extrabold font-mono text-brand-primary">
                    S/ {{ simAmount.toLocaleString('es-PE') }}
                  </span>
                </div>
                <input
                  v-model.number="simAmount"
                  type="range"
                  min="500"
                  max="20000"
                  step="500"
                  class="w-full accent-brand-primary h-2 bg-slate-200 rounded-lg cursor-pointer"
                />
                <div class="flex justify-between text-[11px] text-slate-400 font-mono">
                  <span>S/ 500</span>
                  <span>S/ 20,000</span>
                </div>
              </div>

              <!-- Plazo -->
              <div class="space-y-2">
                <div class="flex justify-between items-center">
                  <label class="text-sm font-semibold text-slate-700">Plazo de pago</label>
                  <span class="text-xl font-extrabold font-mono text-slate-900">{{ simTerm }} meses</span>
                </div>
                <input
                  v-model.number="simTerm"
                  type="range"
                  min="3"
                  max="36"
                  step="3"
                  class="w-full accent-brand-primary h-2 bg-slate-200 rounded-lg cursor-pointer"
                />
                <div class="flex justify-between text-[11px] text-slate-400 font-mono">
                  <span>3 meses</span>
                  <span>36 meses</span>
                </div>
              </div>
            </div>

            <!-- Resultado estimado -->
            <div class="bg-gradient-to-br from-brand-navy to-brand-navy-mid rounded-2xl p-6 text-white space-y-4">
              <div class="text-center space-y-1">
                <p class="text-xs text-white/60 font-medium uppercase tracking-wider">Tu cuota estimada sería</p>
                <p class="text-4xl font-extrabold font-mono tracking-tight">
                  S/ {{ simResult.installment }}
                </p>
                <p class="text-xs text-white/50">por mes</p>
              </div>

              <div class="grid grid-cols-2 gap-2 text-center">
                <div class="bg-white/10 rounded-xl p-2.5">
                  <p class="text-[10px] text-white/60">TEA</p>
                  <p class="font-bold text-sm">{{ simResult.tea }}</p>
                </div>
                <div class="bg-white/10 rounded-xl p-2.5">
                  <p class="text-[10px] text-brand-accent font-semibold">TCEA</p>
                  <p class="font-bold text-sm text-brand-accent">{{ simResult.tcea }}</p>
                </div>
              </div>

              <div class="bg-white/10 rounded-xl p-2.5 text-center">
                <p class="text-[10px] text-white/60">Total a devolver</p>
                <p class="font-bold text-sm">S/ {{ simResult.total }}</p>
              </div>

              <!-- Botón solicitar (FE-LP-17) -->
              <button
                type="button"
                class="w-full bg-brand-primary hover:bg-brand-primary-dark text-white font-bold text-sm py-3 rounded-xl transition-colors flex items-center justify-center gap-2 shadow-lg"
                @click="goToRegisterWithSim"
              >
                Solicitar S/ {{ simAmount.toLocaleString('es-PE') }}
                <ArrowRight class="w-4 h-4" />
              </button>
            </div>
          </div>

          <!-- Aviso legal (FE-LP-18) -->
          <p class="mt-4 text-center text-[11px] text-slate-400">
            * Estimación referencial. La tasa final depende de la evaluación crediticia individual.
            TEA y TCEA reales se muestran antes de firmar el contrato.
          </p>
        </div>
      </div>
    </section>

    <!-- ══════════════════════════════════════════════════════════
         SECCIÓN 4: BENEFICIOS (FE-LP-19 a 21)
         ══════════════════════════════════════════════════════════ -->
    <section id="beneficios" class="scroll-mt-16 py-16 lg:py-20 bg-slate-50">
      <div class="max-w-6xl mx-auto px-4 lg:px-8">
        <div class="text-center mb-12">
          <p class="text-xs font-bold uppercase tracking-widest text-brand-primary mb-2">¿Por qué elegirnos?</p>
          <h2 class="text-2xl sm:text-3xl font-extrabold text-slate-900">Préstamos que se adaptan a ti</h2>
        </div>

        <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-5">
          <div
            v-for="b in benefits"
            :key="b.title"
            class="bg-white rounded-2xl p-5 border border-slate-200 shadow-sm hover:shadow-md hover:-translate-y-0.5 transition-all space-y-3"
          >
            <div class="w-11 h-11 rounded-xl bg-brand-primary/10 flex items-center justify-center">
              <component :is="b.icon" class="w-5 h-5 text-brand-primary" />
            </div>
            <div>
              <h3 class="font-bold text-slate-900 text-sm">{{ b.title }}</h3>
              <p class="text-xs text-slate-500 mt-0.5 leading-relaxed">{{ b.desc }}</p>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- ══════════════════════════════════════════════════════════
         SECCIÓN 5: REQUISITOS (FE-LP-22 a 24)
         ══════════════════════════════════════════════════════════ -->
    <section class="py-16 lg:py-20 bg-white">
      <div class="max-w-4xl mx-auto px-4 lg:px-8">
        <div class="grid sm:grid-cols-2 gap-12 items-center">
          <div class="space-y-6">
            <div>
              <p class="text-xs font-bold uppercase tracking-widest text-brand-primary mb-2">Requisitos</p>
              <h2 class="text-2xl sm:text-3xl font-extrabold text-slate-900">¿Qué necesitas?</h2>
              <p class="mt-2 text-slate-500 text-sm">Solo 4 cosas. Sin ir a ninguna oficina.</p>
            </div>

            <!-- Lista de requisitos (FE-LP-22) -->
            <ul class="space-y-3">
              <li class="flex items-start gap-3">
                <div class="w-8 h-8 rounded-lg bg-brand-primary/10 flex items-center justify-center shrink-0 mt-0.5">
                  <BadgeCheck class="w-4 h-4 text-brand-primary" />
                </div>
                <div>
                  <p class="font-semibold text-slate-900 text-sm">DNI vigente</p>
                  <p class="text-xs text-slate-500">También aceptamos Carné de Extranjería o Pasaporte.</p>
                </div>
              </li>
              <li class="flex items-start gap-3">
                <div class="w-8 h-8 rounded-lg bg-brand-primary/10 flex items-center justify-center shrink-0 mt-0.5">
                  <Smartphone class="w-4 h-4 text-brand-primary" />
                </div>
                <div>
                  <p class="font-semibold text-slate-900 text-sm">Celular con cámara</p>
                  <p class="text-xs text-slate-500">Para tomar las fotos del documento y tu selfie de verificación.</p>
                </div>
              </li>
              <li class="flex items-start gap-3">
                <div class="w-8 h-8 rounded-lg bg-brand-primary/10 flex items-center justify-center shrink-0 mt-0.5">
                  <CreditCard class="w-4 h-4 text-brand-primary" />
                </div>
                <div>
                  <p class="font-semibold text-slate-900 text-sm">Cuenta bancaria o billetera digital</p>
                  <p class="text-xs text-slate-500">BCP, BBVA, Interbank, Scotiabank, Yape o Plin. Debe estar a tu nombre.</p>
                </div>
              </li>
              <li class="flex items-start gap-3">
                <div class="w-8 h-8 rounded-lg bg-brand-primary/10 flex items-center justify-center shrink-0 mt-0.5">
                  <FileText class="w-4 h-4 text-brand-primary" />
                </div>
                <div>
                  <p class="font-semibold text-slate-900 text-sm">Sustento de ingresos</p>
                  <p class="text-xs text-slate-500">Última boleta de pago o recibo por honorarios. PDF o foto.</p>
                </div>
              </li>
            </ul>

            <!-- Rango de edad y sin oficinas (FE-LP-23 y 24) -->
            <div class="flex flex-wrap gap-3 pt-2">
              <span class="inline-flex items-center gap-1.5 bg-slate-100 text-slate-700 text-xs font-medium px-3 py-1.5 rounded-full">
                <CheckCircle2 class="w-3.5 h-3.5 text-emerald-500" />
                De 18 a 70 años
              </span>
              <span class="inline-flex items-center gap-1.5 bg-slate-100 text-slate-700 text-xs font-medium px-3 py-1.5 rounded-full">
                <CheckCircle2 class="w-3.5 h-3.5 text-emerald-500" />
                Sin ir a ninguna oficina
              </span>
              <span class="inline-flex items-center gap-1.5 bg-slate-100 text-slate-700 text-xs font-medium px-3 py-1.5 rounded-full">
                <CheckCircle2 class="w-3.5 h-3.5 text-emerald-500" />
                Sin imprimir documentos
              </span>
            </div>
          </div>

          <!-- Visual derecha -->
          <div class="hidden sm:block">
            <div class="bg-gradient-to-br from-slate-50 to-slate-100 rounded-3xl border border-slate-200 p-8 space-y-4 text-center">
              <div class="mx-auto w-20 h-20 rounded-2xl bg-brand-primary flex items-center justify-center shadow-lg">
                <Shield class="w-10 h-10 text-white" />
              </div>
              <div class="space-y-1">
                <p class="font-bold text-slate-900 text-lg">Proceso Seguro</p>
                <p class="text-sm text-slate-500 leading-relaxed">
                  Tus documentos se procesan con cifrado de nivel bancario y se eliminan automáticamente después de la verificación.
                </p>
              </div>
              <div class="bg-white rounded-xl p-3 border border-slate-200 text-left space-y-2 text-xs text-slate-600">
                <p class="flex items-center gap-2"><CheckCircle2 class="w-3.5 h-3.5 text-emerald-500 shrink-0" /> Validación RENIEC automática</p>
                <p class="flex items-center gap-2"><CheckCircle2 class="w-3.5 h-3.5 text-emerald-500 shrink-0" /> Biometría facial anti-spoofing</p>
                <p class="flex items-center gap-2"><CheckCircle2 class="w-3.5 h-3.5 text-emerald-500 shrink-0" /> Firma electrónica con OTP</p>
                <p class="flex items-center gap-2"><CheckCircle2 class="w-3.5 h-3.5 text-emerald-500 shrink-0" /> Contrato con validez legal</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- ══════════════════════════════════════════════════════════
         SECCIÓN 6: TRANSPARENCIA FINANCIERA (FE-LP-25 a 28)
         ══════════════════════════════════════════════════════════ -->
    <section class="py-16 lg:py-20 bg-blue-50">
      <div class="max-w-4xl mx-auto px-4 lg:px-8">
        <div class="text-center mb-10">
          <p class="text-xs font-bold uppercase tracking-widest text-brand-primary mb-2">Transparencia</p>
          <h2 class="text-2xl sm:text-3xl font-extrabold text-slate-900">Sin letra chica. Sin sorpresas.</h2>
          <p class="mt-2 text-slate-500 text-sm">Ejemplo real de un préstamo de S/ 5,000 a 12 meses.</p>
        </div>

        <!-- Tabla de ejemplo (FE-LP-25) -->
        <div class="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
          <div class="grid sm:grid-cols-5 divide-y sm:divide-y-0 sm:divide-x divide-slate-200 text-center">
            <div class="p-5 space-y-1">
              <p class="text-[10px] text-slate-400 uppercase font-bold tracking-wide">Monto</p>
              <p class="text-xl font-extrabold text-slate-900">S/ 5,000</p>
            </div>
            <div class="p-5 space-y-1">
              <p class="text-[10px] text-slate-400 uppercase font-bold tracking-wide">Plazo</p>
              <p class="text-xl font-extrabold text-slate-900">12 meses</p>
            </div>
            <div class="p-5 space-y-1">
              <p class="text-[10px] text-slate-400 uppercase font-bold tracking-wide">Cuota mensual</p>
              <p class="text-xl font-extrabold text-brand-primary">S/ 487.50</p>
            </div>
            <div class="p-5 space-y-1">
              <p class="text-[10px] text-slate-400 uppercase font-bold tracking-wide">TEA</p>
              <p class="text-xl font-extrabold text-slate-900">72%</p>
            </div>
            <div class="p-5 space-y-1 bg-brand-primary/5">
              <p class="text-[10px] text-brand-primary uppercase font-bold tracking-wide">TCEA</p>
              <p class="text-xl font-extrabold text-brand-primary">78%</p>
            </div>
          </div>
          <div class="bg-slate-50 border-t border-slate-200 px-6 py-3 flex justify-between text-sm">
            <span class="text-slate-500">Total a devolver:</span>
            <span class="font-bold text-slate-900">S/ 5,850.00</span>
          </div>
        </div>

        <!-- Explicación TEA vs TCEA (FE-LP-26) -->
        <div class="mt-6 grid sm:grid-cols-2 gap-4">
          <div class="bg-white rounded-xl border border-slate-200 p-4 space-y-1.5">
            <p class="font-bold text-slate-900 text-sm flex items-center gap-2">
              <span class="w-5 h-5 rounded-full bg-slate-900 text-white text-[10px] flex items-center justify-center font-bold">T</span>
              ¿Qué es la TEA?
            </p>
            <p class="text-xs text-slate-500 leading-relaxed">
              La <strong>Tasa Efectiva Anual</strong> es el costo del dinero prestado en un año, sin incluir comisiones ni gastos adicionales.
            </p>
          </div>
          <div class="bg-brand-primary/5 rounded-xl border border-brand-primary/20 p-4 space-y-1.5">
            <p class="font-bold text-brand-primary text-sm flex items-center gap-2">
              <span class="w-5 h-5 rounded-full bg-brand-primary text-white text-[10px] flex items-center justify-center font-bold">C</span>
              ¿Qué es la TCEA?
            </p>
            <p class="text-xs text-slate-600 leading-relaxed">
              La <strong>Tasa de Costo Efectivo Anual</strong> incluye la TEA más todas las comisiones y seguros. Representa el costo total real del crédito según la SBS.
            </p>
          </div>
        </div>

        <!-- Normativa (FE-LP-27 y 28) -->
        <div class="mt-6 flex flex-wrap gap-3 justify-center">
          <div class="inline-flex items-center gap-2 bg-white border border-slate-200 rounded-full px-4 py-2 text-xs text-slate-600 shadow-sm">
            <ShieldCheck class="w-4 h-4 text-emerald-500" />
            Supervisados por la SBS — Ley N° 26702
          </div>
          <div class="inline-flex items-center gap-2 bg-white border border-slate-200 rounded-full px-4 py-2 text-xs text-slate-600 shadow-sm">
            <CheckCircle2 class="w-4 h-4 text-emerald-500" />
            Sin penalidad por prepago — Ley 29571
          </div>
        </div>
      </div>
    </section>

    <!-- ══════════════════════════════════════════════════════════
         SECCIÓN 7: FAQ (FE-LP-29 a 31)
         ══════════════════════════════════════════════════════════ -->
    <section id="faq" class="scroll-mt-16 py-16 lg:py-20 bg-white">
      <div class="max-w-3xl mx-auto px-4 lg:px-8">
        <div class="text-center mb-10">
          <p class="text-xs font-bold uppercase tracking-widest text-brand-primary mb-2">FAQ</p>
          <h2 class="text-2xl sm:text-3xl font-extrabold text-slate-900">Preguntas frecuentes</h2>
        </div>

        <div class="space-y-2">
          <div
            v-for="(faq, i) in faqs"
            :key="i"
            class="border border-slate-200 rounded-xl overflow-hidden"
          >
            <button
              type="button"
              class="w-full flex items-center justify-between gap-4 px-5 py-4 text-left bg-white hover:bg-slate-50 transition-colors"
              @click="toggleFaq(i)"
            >
              <span class="font-semibold text-sm text-slate-900">{{ faq.q }}</span>
              <component
                :is="openFaq === i ? Minus : Plus"
                class="w-4 h-4 text-brand-primary shrink-0 transition-transform"
              />
            </button>
            <div
              v-if="openFaq === i"
              class="px-5 pb-4 text-sm text-slate-600 leading-relaxed border-t border-slate-100 bg-slate-50/50"
            >
              <p class="pt-3">{{ faq.a }}</p>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- ══════════════════════════════════════════════════════════
         SECCIÓN 8: CTA FINAL (FE-LP-32 a 33)
         ══════════════════════════════════════════════════════════ -->
    <section class="py-16 lg:py-20 bg-gradient-to-br from-brand-navy to-brand-navy-mid">
      <div class="max-w-3xl mx-auto px-4 lg:px-8 text-center space-y-6">
        <div class="space-y-2">
          <h2 class="text-2xl sm:text-3xl font-extrabold text-white leading-tight">
            ¿Listo para recibir tu préstamo hoy?
          </h2>
          <p class="text-white/70 text-sm sm:text-base">
            Únete a miles de peruanos que ya financiaron sus proyectos con nosotros.
          </p>
        </div>

        <RouterLink
          to="/registro"
          class="inline-flex items-center gap-2 px-8 py-4 rounded-2xl bg-brand-primary hover:bg-brand-primary-dark text-white font-bold text-base transition-all shadow-xl hover:shadow-2xl hover:-translate-y-0.5"
        >
          <Sparkles class="w-5 h-5" />
          Solicitar mi Préstamo Ahora
          <ArrowRight class="w-5 h-5" />
        </RouterLink>

        <!-- Frase de seguridad (FE-LP-33) -->
        <p class="text-xs text-white/50">
          Sin costos ocultos · Sin letra chica · 100% digital
        </p>
      </div>
    </section>

    <!-- ══════════════════════════════════════════════════════════
         SECCIÓN 9: FOOTER LEGAL (FE-LP-34 a 38)
         ══════════════════════════════════════════════════════════ -->
    <footer class="bg-[#050B16] border-t border-white/10 text-slate-400 px-4 lg:px-8 py-10">
      <div class="max-w-6xl mx-auto space-y-8">
        <div class="grid sm:grid-cols-3 gap-8">
          <!-- Empresa -->
          <div class="space-y-3">
            <div class="flex items-center gap-2.5">
              <div class="h-9 w-9 rounded-xl bg-gradient-to-br from-blue-500 to-sky-400 flex items-center justify-center font-bold text-white shadow-md shadow-blue-500/20">
                <Zap class="w-5 h-5 fill-white text-white" />
              </div>
              <div>
                <p class="font-black text-lg leading-none text-white tracking-tight">
                  Credi<span class="text-sky-400">Ya</span>
                </p>
                <p class="text-[9px] text-slate-400 font-bold tracking-widest uppercase leading-none mt-1">
                  Finanzas Digitales
                </p>
              </div>
            </div>
            <p class="text-xs text-slate-400 leading-relaxed">
              CrediYa S.A.C. · RUC: 20601234567<br />
              Av. Las Camelias 820, San Isidro, Lima<br />
              Empresa supervisada por la SBS
            </p>
          </div>

          <!-- Links legales (FE-LP-35 y 36) -->
          <div class="space-y-2">
            <p class="font-semibold text-slate-300 text-xs uppercase tracking-wider">Legal</p>
            <ul class="space-y-2 text-xs">
              <li>
                <RouterLink to="/libro-de-reclamaciones" class="hover:text-white transition-colors inline-flex items-center gap-1.5 text-sky-400 font-medium">
                  Libro de Reclamaciones Virtual
                </RouterLink>
              </li>
              <li>
                <a href="#" class="hover:text-white transition-colors">Política de Privacidad</a>
              </li>
              <li>
                <a href="#" class="hover:text-white transition-colors">Términos y Condiciones</a>
              </li>
            </ul>
          </div>

          <!-- Supervisión (FE-LP-37) -->
          <div class="space-y-2">
            <p class="font-semibold text-slate-300 text-xs uppercase tracking-wider">Regulación</p>
            <div class="space-y-2 text-xs">
              <div class="flex items-center gap-2">
                <ShieldCheck class="w-4 h-4 text-emerald-400 shrink-0" />
                <span>Supervisados por la SBS — Ley N° 26702</span>
              </div>
              <div class="flex items-center gap-2">
                <ShieldCheck class="w-4 h-4 text-emerald-400 shrink-0" />
                <span>Protección al Consumidor — Ley N° 29571</span>
              </div>
              <div class="flex items-center gap-2">
                <ShieldCheck class="w-4 h-4 text-emerald-400 shrink-0" />
                <span>Datos Personales — Ley N° 29733</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Copyright (FE-LP-38) estilo de referencia -->
        <div class="border-t border-white/10 pt-6 flex flex-col sm:flex-row items-center justify-between gap-4 text-xs text-slate-500 text-center sm:text-left">
          <p>© 2026 CrediYa S.A.C. Todos los derechos reservados. Regulado bajo normativa de protección al consumidor y seguridad financiera digital.</p>
          <div class="flex items-center gap-4 text-xs">
            <a href="#" class="hover:text-slate-300 transition-colors">Términos</a>
            <span class="text-slate-700">·</span>
            <a href="#" class="hover:text-slate-300 transition-colors">Privacidad</a>
            <span class="text-slate-700">·</span>
            <RouterLink to="/libro-de-reclamaciones" class="hover:text-sky-300 transition-colors text-sky-400">
              Libro de Reclamaciones
            </RouterLink>
          </div>
        </div>
      </div>
    </footer>

  </div>
</template>
