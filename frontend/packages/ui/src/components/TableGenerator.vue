<script setup lang="ts" generic="Row extends object">
import type { TableColumn, TableRowKey } from '../generator'

const props = withDefaults(
  defineProps<{
    columns: readonly TableColumn[]
    rows: readonly Row[]
    rowKey: TableRowKey<Row>
    tableClass?: string
    headClass?: string
    headerCellClass?: string
    bodyClass?: string
    rowClass?: string | ((row: Row) => string | Record<string, boolean>)
  }>(),
  {
    tableClass: 'w-full text-sm text-left',
    headClass: 'border-b bg-muted/50 text-xs font-medium text-muted-foreground',
    headerCellClass: 'px-4 py-3',
    bodyClass: 'divide-y divide-border',
    rowClass: 'hover:bg-muted/40 transition-colors',
  },
)

defineSlots<{
  row(props: { row: Row; index: number }): unknown
}>()

function getRowKey(row: Row, index: number): string | number {
  const { rowKey } = props
  if (typeof rowKey === 'function') return rowKey(row, index)
  return String(row[rowKey])
}

function getRowClass(row: Row): string | Record<string, boolean> {
  return typeof props.rowClass === 'function' ? props.rowClass(row) : props.rowClass
}
</script>

<template>
  <div class="overflow-x-auto">
    <table :class="tableClass">
      <thead :class="headClass">
        <tr>
          <th
            v-for="column in columns"
            :key="column.key"
            :class="[headerCellClass, column.headerClass]"
          >
            {{ column.label }}
          </th>
        </tr>
      </thead>
      <tbody :class="bodyClass">
        <tr v-for="(row, index) in rows" :key="getRowKey(row, index)" :class="getRowClass(row)">
          <slot name="row" :row="row" :index="index" />
        </tr>
      </tbody>
    </table>
  </div>
</template>
