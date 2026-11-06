<script setup lang="ts">
import { computed } from 'vue'

import { Badge } from '@prestamos/ui/components/ui/badge'
import { cn } from '@prestamos/ui/lib/utils'
import { getStatusInfo, toneClasses, type StatusKind } from '@prestamos/ui/status'

const props = defineProps<{
  kind: StatusKind
  status: string
}>()

const info = computed(() => getStatusInfo(props.kind, props.status))
</script>

<template>
  <Badge variant="outline" :class="cn('font-medium', toneClasses[info.tone])">
    {{ info.label }}
  </Badge>
</template>
