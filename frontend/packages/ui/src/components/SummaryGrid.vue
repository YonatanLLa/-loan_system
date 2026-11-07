<script setup lang="ts">
import type { HTMLAttributes } from 'vue'
import { cn } from '../lib/utils'
import type { SummaryItem } from '../generator'
import MoneyText from './MoneyText.vue'

const props = withDefaults(
  defineProps<{
    items: readonly SummaryItem[]
    class?: HTMLAttributes['class']
    itemClass?: string
    labelClass?: string
    valueClass?: string
  }>(),
  {
    itemClass: 'p-3 rounded-lg border bg-muted/30 text-xs',
    labelClass: 'text-muted-foreground block text-[11px]',
    valueClass: 'font-bold text-foreground text-sm',
  },
)

defineSlots<{
  item?(props: { item: SummaryItem; index: number }): unknown
  label?(props: { item: SummaryItem; index: number }): unknown
  value?(props: { item: SummaryItem; index: number }): unknown
}>()
</script>

<template>
  <div :class="cn('grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3', props.class)">
    <div
      v-for="(item, index) in items"
      :key="item.key ?? item.label ?? index"
      :class="cn(itemClass, item.itemClass)"
    >
      <slot name="item" :item="item" :index="index">
        <slot name="label" :item="item" :index="index">
          <span :class="cn(labelClass, item.labelClass)">{{ item.label }}</span>
        </slot>
        <slot name="value" :item="item" :index="index">
          <span :class="cn('block', valueClass, item.valueClass)">
            <MoneyText
              v-if="item.money !== undefined && item.money !== null"
              :amount="String(item.money)"
              :currency="item.currency"
            />
            <template v-else>
              {{ item.value ?? '—' }}
            </template>
          </span>
        </slot>
      </slot>
    </div>
  </div>
</template>
