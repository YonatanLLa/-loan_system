import type { StatusKind } from './status'

export type TableColumnKind = 'text' | 'mono' | 'money' | 'date' | 'datetime' | 'status'
export type TableColumnAlign = 'left' | 'center' | 'right'

export interface TableColumn<Row = object> {
  key: string
  label: string
  headerClass?: string
  cellClass?: string | ((row: Row) => string | Record<string, boolean>)
  align?: TableColumnAlign
  kind?: TableColumnKind
  statusKind?: StatusKind
  currency?: string
  value?: (row: Row, index: number) => unknown
}

export type TableRowKey<Row> = keyof Row | ((row: Row, index: number) => string | number)

export interface SummaryItem {
  key?: string
  label: string
  value?: string | number | null
  money?: string | number | null
  currency?: string
  valueClass?: string
  labelClass?: string
  itemClass?: string
}

