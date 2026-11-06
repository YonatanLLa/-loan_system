export interface TableColumn {
  key: string
  label: string
  headerClass?: string
}

export type TableRowKey<Row> = keyof Row | ((row: Row, index: number) => string | number)
