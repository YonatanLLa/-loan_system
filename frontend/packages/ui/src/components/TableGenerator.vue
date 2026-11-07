<script setup lang="ts" generic="Row extends object">
import { formatBusinessDate, formatInstant } from '@prestamos/utils'
import type { TableColumn, TableColumnAlign, TableRowKey } from '../generator'
import { cn } from '../lib/utils'
import MoneyText from './MoneyText.vue'
import StatusBadge from './StatusBadge.vue'

const props = withDefaults(
  defineProps<{
    columns: readonly TableColumn<Row>[]
    rows: readonly Row[]
    rowKey: TableRowKey<Row>
    emptyText?: string
    emptyClass?: string
    tableClass?: string
    headClass?: string
    headerCellClass?: string
    bodyClass?: string
    cellClass?: string
    rowClass?: string | ((row: Row) => string | Record<string, boolean>)
  }>(),
  {
    emptyClass: 'p-8 text-center text-xs text-muted-foreground',
    tableClass: 'w-full text-sm text-left',
    headClass: 'border-b bg-muted/50 text-xs font-medium text-muted-foreground',
    headerCellClass: 'px-4 py-3',
    bodyClass: 'divide-y divide-border',
    cellClass: 'px-4 py-3',
    rowClass: 'hover:bg-muted/40 transition-colors',
  },
)

defineSlots<
  {
    empty?(): unknown
    row?(props: { row: Row; index: number }): unknown
    cell?(props: {
      row: Row
      index: number
      value: unknown
      column: TableColumn<Row>
    }): unknown
  } & {
    [K in `cell-${string}`]?: (props: {
      row: Row
      index: number
      value: unknown
      column: TableColumn<Row>
    }) => unknown
  }
>()

const alignClassMap: Record<TableColumnAlign, string> = {
  left: 'text-left',
  center: 'text-center',
  right: 'text-right',
}

function getRowKey(row: Row, index: number): string | number {
  const { rowKey } = props
  if (typeof rowKey === 'function') return rowKey(row, index)
  return String(row[rowKey])
}

function getRowClass(row: Row): string | Record<string, boolean> {
  return typeof props.rowClass === 'function' ? props.rowClass(row) : props.rowClass
}

function getHeaderCellClass(column: TableColumn<Row>): string {
  return cn(
    props.headerCellClass,
    column.align ? alignClassMap[column.align] : undefined,
    column.headerClass,
  )
}

function getCellClass(row: Row, column: TableColumn<Row>): string {
  const customClass =
    typeof column.cellClass === 'function' ? column.cellClass(row) : column.cellClass

  const kindClass =
    column.kind === 'mono' || column.kind === 'money'
      ? 'font-mono'
      : column.kind === 'date' || column.kind === 'datetime'
        ? 'whitespace-nowrap'
        : undefined

  return cn(
    props.cellClass,
    column.align ? alignClassMap[column.align] : undefined,
    kindClass,
    customClass,
  )
}

function getCellValue(row: Row, column: TableColumn<Row>, index: number): unknown {
  if (column.value) return column.value(row, index)
  return (row as Record<string, unknown>)[column.key]
}

function formatCellDate(val: unknown): string {
  if (val === null || val === undefined || val === '') return '—'
  return formatBusinessDate(String(val))
}

function formatCellDateTime(val: unknown): string {
  if (val === null || val === undefined || val === '') return '—'
  return formatInstant(String(val))
}
</script>

<template>
  <div v-if="rows.length === 0 && (emptyText || $slots.empty)" :class="emptyClass">
    <slot name="empty">{{ emptyText }}</slot>
  </div>
  <div v-else class="overflow-x-auto">
    <table :class="tableClass">
      <thead :class="headClass">
        <tr>
          <th
            v-for="column in columns"
            :key="column.key"
            :class="getHeaderCellClass(column)"
          >
            {{ column.label }}
          </th>
        </tr>
      </thead>
      <tbody :class="bodyClass">
        <tr v-for="(row, index) in rows" :key="getRowKey(row, index)" :class="getRowClass(row)">
          <slot v-if="$slots.row" name="row" :row="row" :index="index" />
          <template v-else>
            <td
              v-for="column in columns"
              :key="column.key"
              :class="getCellClass(row, column)"
            >
              <slot
                v-if="$slots[`cell-${column.key}`]"
                :name="`cell-${column.key}`"
                :row="row"
                :index="index"
                :value="getCellValue(row, column, index)"
                :column="column"
              />
              <slot
                v-else-if="$slots.cell"
                name="cell"
                :row="row"
                :index="index"
                :value="getCellValue(row, column, index)"
                :column="column"
              />
              <MoneyText
                v-else-if="column.kind === 'money'"
                :amount="String(getCellValue(row, column, index) ?? '0')"
                :currency="column.currency"
              />
              <StatusBadge
                v-else-if="column.kind === 'status' && column.statusKind"
                :kind="column.statusKind"
                :status="String(getCellValue(row, column, index) ?? '')"
              />
              <template v-else-if="column.kind === 'date'">
                {{ formatCellDate(getCellValue(row, column, index)) }}
              </template>
              <template v-else-if="column.kind === 'datetime'">
                {{ formatCellDateTime(getCellValue(row, column, index)) }}
              </template>
              <template v-else>
                {{ getCellValue(row, column, index) ?? '—' }}
              </template>
            </td>
          </template>
        </tr>
      </tbody>
    </table>
  </div>
</template>
