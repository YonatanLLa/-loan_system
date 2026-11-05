// Componentes propios del sistema de diseño. Los primitivos de shadcn-vue se importan
// por ruta: import { Button } from '@prestamos/ui/components/ui/button'
export { default as StatusBadge } from './components/StatusBadge.vue'
export { default as MoneyText } from './components/MoneyText.vue'
export { default as PageHeader } from './components/PageHeader.vue'
export { default as EmptyState } from './components/EmptyState.vue'
export { default as ChartCanvas } from './components/ChartCanvas.vue'
export { default as WelcomeCard } from './components/WelcomeCard.vue'
export { useBusyAction } from './composables/useBusyAction'
export { cn } from './lib/utils'
export {
  getStatusInfo,
  statusCatalog,
  toneClasses,
  type StatusInfo,
  type StatusKind,
  type StatusTone,
} from './status'
