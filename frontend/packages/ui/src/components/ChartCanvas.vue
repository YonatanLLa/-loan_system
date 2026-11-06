<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, shallowRef, watch } from 'vue'
import { Chart, registerables, type ChartConfiguration } from 'chart.js'

// Envoltorio mínimo de Chart.js para los gráficos del dashboard (US-33).
Chart.register(...registerables)
Chart.defaults.responsive = true
Chart.defaults.maintainAspectRatio = false
Chart.defaults.font.family = '"Geist Variable", ui-sans-serif, system-ui, sans-serif'

const props = defineProps<{
  config: ChartConfiguration
  /** Texto alternativo para lectores de pantalla (FE-NF-07). */
  label: string
}>()

const canvas = ref<HTMLCanvasElement | null>(null)
const chart = shallowRef<Chart | null>(null)

onMounted(() => {
  if (canvas.value) chart.value = new Chart(canvas.value, props.config)
})

watch(
  () => props.config,
  (config) => {
    if (!chart.value) return
    chart.value.data = config.data
    chart.value.options = config.options ?? {}
    chart.value.update()
  },
  { deep: true },
)

onBeforeUnmount(() => chart.value?.destroy())
</script>

<template>
  <div class="relative h-64 w-full">
    <canvas ref="canvas" role="img" :aria-label="label" />
  </div>
</template>
