<script setup lang="ts">
import { computed } from 'vue'
import { formatMoney } from '@prestamos/utils'

// Muestra un monto que llegó de la API como string decimal. No calcula nada.
const props = withDefaults(defineProps<{ amount: string; currency?: string }>(), {
  currency: 'PEN',
})

const text = computed(() => formatMoney(props.amount, props.currency))
</script>

<template>
  <span class="tabular-nums whitespace-nowrap">{{ text }}</span>
</template>
