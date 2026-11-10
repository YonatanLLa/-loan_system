<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import {
  ArrowRight,
  CheckCircle2,
  Clock,
  FileCheck2,
  FilePlus,
  Info,
  ShieldAlert,
  Sparkles,
} from '@lucide/vue'

import { MoneyText, StatusBadge } from '@prestamos/ui'
import { Badge } from '@prestamos/ui/components/ui/badge'
import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardHeader,
} from '@prestamos/ui/components/ui/card'
import { Skeleton } from '@prestamos/ui/components/ui/skeleton'
import { formatInstant } from '@prestamos/utils'

import { useApplicationsTimeline } from '../composables'

// FE-CP-08: Línea de tiempo de la solicitud (SOLICITADO → EN_EVALUACION → APROBADO → DESEMBOLSADO).

const { applications, isLoading, error, loadApplications, getTimelineSteps } =
  useApplicationsTimeline()

onMounted(() => {
  void loadApplications()
})
</script>

<template>
  <div class="space-y-4 py-2">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div>
        <h2 class="text-xl font-bold">Mis Solicitudes</h2>
        <p class="text-xs text-muted-foreground">
          Seguimiento en tiempo real de tus préstamos en evaluación (FE-CP-08).
        </p>
      </div>

      <Button as-child size="sm" class="h-8 text-xs">
        <RouterLink to="/solicitar">
          <FilePlus class="size-3.5 mr-1.5" />
          Nueva Solicitud
        </RouterLink>
      </Button>
    </div>

    <!-- Loading -->
    <div v-if="isLoading" class="space-y-3 p-4">
      <Skeleton class="h-28 w-full rounded-xl" />
      <Skeleton class="h-28 w-full rounded-xl" />
    </div>

    <!-- Error -->
    <div v-else-if="error" class="p-4 rounded-xl border border-destructive/30 bg-destructive/5 text-destructive text-xs">
      {{ error }}
    </div>

    <!-- Empty State -->
    <div v-else-if="applications.length === 0" class="py-12 text-center space-y-4">
      <div class="mx-auto size-14 rounded-2xl bg-primary/10 flex items-center justify-center text-primary">
        <Sparkles class="size-7" />
      </div>
      <div class="space-y-1">
        <h3 class="font-bold text-base">Aún no tienes solicitudes</h3>
        <p class="text-xs text-muted-foreground max-w-xs mx-auto">
          Simula y solicita tu primer préstamo 100% digital con desembolso inmediato.
        </p>
      </div>
      <Button as-child size="sm" class="h-9 text-xs">
        <RouterLink to="/solicitar">
          Simular mi Préstamo
          <ArrowRight class="size-3.5 ml-1.5" />
        </RouterLink>
      </Button>
    </div>

    <!-- Lista de Solicitudes con Línea de Tiempo (FE-CP-08) -->
    <div v-else class="space-y-4">
      <Card
        v-for="app in applications"
        :key="app.id"
        class="shadow-md border-border overflow-hidden"
      >
        <!-- Card Header -->
        <CardHeader class="pb-3 border-b bg-muted/10">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <div>
              <div class="flex items-center gap-2">
                <span class="font-mono font-bold text-sm text-foreground">#{{ app.id }}</span>
                <Badge variant="outline" class="text-[10px]">
                  {{ app.productName }}
                </Badge>
              </div>
              <span class="text-[10px] text-muted-foreground block mt-0.5">
                Ingresada el {{ formatInstant(app.createdAt) }}
              </span>
            </div>

            <StatusBadge kind="application" :status="app.status" />
          </div>
        </CardHeader>

        <CardContent class="space-y-4 pt-4 text-xs">
          <!-- Monto y Condiciones -->
          <div class="grid grid-cols-2 sm:grid-cols-3 gap-2 p-3 rounded-lg bg-muted/20 text-xs">
            <div>
              <span class="text-muted-foreground block text-[10px]">Monto Solicitado:</span>
              <span class="font-bold font-mono text-foreground text-sm">
                <MoneyText :amount="app.requestedAmount" />
              </span>
            </div>
            <div>
              <span class="text-muted-foreground block text-[10px]">Plazo de Pago:</span>
              <span class="font-semibold text-foreground text-sm">
                {{ app.term }} meses
              </span>
            </div>
            <div v-if="app.approvedAmount && app.status.includes('APROBADO')" class="col-span-2 sm:col-span-1">
              <span class="text-primary font-semibold block text-[10px]">Monto Aprobado:</span>
              <span class="font-bold font-mono text-primary text-sm">
                <MoneyText :amount="app.approvedAmount" />
              </span>
            </div>
          </div>

          <!-- LÍNEA DE TIEMPO VISUAL (FE-CP-08) -->
          <div class="space-y-3 pt-1">
            <h4 class="font-semibold text-xs text-foreground flex items-center gap-1.5">
              <Clock class="size-3.5 text-primary" />
              Línea de Tiempo del Proceso
            </h4>

            <div class="space-y-3 pl-2 border-l-2 border-primary/20 ml-2">
              <div
                v-for="step in getTimelineSteps(app.status)"
                :key="step.key"
                class="relative pl-5"
              >
                <!-- Dot Indicador -->
                <div
                  class="absolute -left-[11px] top-0.5 size-4 rounded-full flex items-center justify-center text-[10px]"
                  :class="
                    step.isDone
                      ? 'bg-success text-white'
                      : step.isCurrent
                      ? 'bg-primary text-white ring-4 ring-primary/20 animate-pulse'
                      : 'bg-muted border border-border text-muted-foreground'
                  "
                >
                  <CheckCircle2 v-if="step.isDone" class="size-3" />
                  <span v-else-if="step.isCurrent" class="size-1.5 rounded-full bg-white" />
                </div>

                <div class="space-y-0.5">
                  <span
                    class="font-semibold text-xs block"
                    :class="
                      step.isCurrent
                        ? 'text-primary'
                        : step.isDone
                        ? 'text-foreground'
                        : 'text-muted-foreground'
                    "
                  >
                    {{ step.label }}
                  </span>
                  <p class="text-[11px] text-muted-foreground leading-tight">
                    {{ step.description }}
                  </p>
                </div>
              </div>
            </div>
          </div>

          <!-- Información sobre avances o datos complementarios requeridos (Requisito 7) -->
          <div
            v-if="app.decisionNotes && app.status !== 'RECHAZADO'"
            class="p-3.5 rounded-xl border border-amber-300 bg-amber-50/70 text-xs text-amber-950 space-y-1.5"
          >
            <div class="flex items-center gap-2 font-bold text-amber-900">
              <Info class="size-4 text-amber-600" />
              <span>Avance de Evaluación / Datos Requeridos</span>
            </div>
            <p class="text-[11px] text-amber-900 leading-relaxed">
              {{ app.decisionNotes }}
            </p>
          </div>

          <!-- Acciones según estado -->
          <!-- Si fue APROBADO: Botón para firmar contrato electrónico (FE-CP-09) -->
          <div
            v-if="app.status === 'APROBADO' || app.status === 'APROBADO_CON_CONDICIONES'"
            class="p-3.5 rounded-xl border border-success/30 bg-success/10 space-y-2 mt-2"
          >
            <div class="flex items-center gap-2 text-success font-bold text-xs">
              <Sparkles class="size-4" />
              <span>¡Tu crédito está listo para desembolso!</span>
            </div>
            <p class="text-[11px] text-foreground">
              Solo falta firmar digitalmente el contrato y pagaré con tu código OTP para recibir los fondos en tu cuenta.
            </p>
            <Button as-child size="sm" class="w-full text-xs font-semibold bg-success hover:bg-success/90 text-white">
              <RouterLink :to="{ path: '/contrato', query: { applicationId: app.id } }">
                <FileCheck2 class="size-3.5 mr-1.5" />
                Firmar Contrato y Pagaré Digital (FE-CP-09)
              </RouterLink>
            </Button>
          </div>

          <!-- Si fue DESEMBOLSADO: Enlace a Mis Préstamos -->
          <div
            v-else-if="app.status === 'DESEMBOLSADO'"
            class="p-3 rounded-lg border bg-muted/20 flex items-center justify-between text-xs"
          >
            <span class="text-muted-foreground">Préstamo activo en calendario:</span>
            <Button as-child variant="outline" size="sm" class="h-7 text-xs">
              <RouterLink to="/prestamos">
                Ver Cuotas y Pagar
                <ArrowRight class="size-3 ml-1" />
              </RouterLink>
            </Button>
          </div>

          <!-- Si fue RECHAZADO: Mensaje explicativo -->
          <div
            v-else-if="app.status === 'RECHAZADO'"
            class="p-3 rounded-lg border border-destructive/30 bg-destructive/5 text-xs text-destructive flex items-start gap-2"
          >
            <ShieldAlert class="size-4 shrink-0 mt-0.5" />
            <div>
              <span class="font-bold block">Solicitud no aprobada</span>
              <p class="text-[11px] text-muted-foreground">
                {{ app.decisionNotes || 'No pudimos autorizar el crédito según las políticas vigentes de riesgo crediticio.' }}
              </p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  </div>
</template>
