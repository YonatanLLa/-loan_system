<script setup lang="ts">
import { ref, watch } from 'vue'
import { LoaderCircle, UserPlus } from '@lucide/vue'

import { Button } from '@prestamos/ui/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@prestamos/ui/components/ui/card'
import { Input } from '@prestamos/ui/components/ui/input'
import { Label } from '@prestamos/ui/components/ui/label'

import type { CreateAdminUserPayload } from '../services/admin.service'

const props = defineProps<{
  open: boolean
  isBusy: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'submit', payload: CreateAdminUserPayload): void
}>()

const selectClass =
  'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-xs shadow-xs focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring'

const fullName = ref('')
const email = ref('')
const role = ref('ROLE_ANALYST')
const password = ref('Demo1234!')

watch(
  () => props.open,
  (val) => {
    if (val) {
      fullName.value = ''
      email.value = ''
      role.value = 'ROLE_ANALYST'
      password.value = 'Demo1234!'
    }
  },
)

const ROLE_PERMISSIONS: Record<string, string[]> = {
  ROLE_ADMIN: ['*'],
  ROLE_OPERATOR: ['loans.disburse', 'payments.register', 'treasury.manage', 'reports.view'],
  ROLE_ANALYST: ['applications.decide', 'customers.kyc', 'loans.view'],
  ROLE_COLLECTOR: ['collections.actions', 'collections.assign', 'loans.view'],
  ROLE_AUDITOR: ['audit.read', 'reports.view', 'loans.view'],
}

function handleSubmit() {
  if (!fullName.value.trim() || !email.value.trim() || !role.value) return
  emit('submit', {
    fullName: fullName.value.trim(),
    email: email.value.trim().toLowerCase(),
    role: role.value,
    password: password.value,
    permissions: ROLE_PERMISSIONS[role.value] || ['dashboard.view'],
  })
}
</script>

<template>
  <div
    v-if="open"
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs"
  >
    <Card class="w-full max-w-md shadow-2xl">
      <CardHeader class="border-b pb-3">
        <div class="flex items-center gap-2 text-primary">
          <UserPlus class="size-5" />
          <CardTitle class="text-lg">Nuevo Usuario del Backoffice</CardTitle>
        </div>
        <CardDescription class="text-xs">
          Invita a un colaborador interno y asígnale su rol operativo en el sistema.
        </CardDescription>
      </CardHeader>

      <form @submit.prevent="handleSubmit">
        <CardContent class="space-y-4 pt-4 text-xs">
          <div class="space-y-1.5">
            <Label for="userFullName" class="font-medium">Nombre completo</Label>
            <Input
              id="userFullName"
              v-model="fullName"
              placeholder="Ej. Valeria Mendoza Rojas"
              required
              class="h-9 text-xs"
            />
          </div>

          <div class="space-y-1.5">
            <Label for="userEmail" class="font-medium">Correo electrónico corporativo</Label>
            <Input
              id="userEmail"
              v-model="email"
              type="email"
              placeholder="ejemplo@fintech.pe"
              required
              class="h-9 text-xs"
            />
          </div>

          <div class="space-y-1.5">
            <Label for="userRole" class="font-medium">Rol y nivel de acceso</Label>
            <select id="userRole" v-model="role" :class="selectClass">
              <option value="ROLE_ANALYST">Analista de Admisión y Riesgos (ROLE_ANALYST)</option>
              <option value="ROLE_COLLECTOR">Gestor de Cobranzas (ROLE_COLLECTOR)</option>
              <option value="ROLE_OPERATOR">Operador de Tesorería (ROLE_OPERATOR)</option>
              <option value="ROLE_AUDITOR">Auditor de Cumplimiento (ROLE_AUDITOR)</option>
              <option value="ROLE_ADMIN">Administrador General (ROLE_ADMIN)</option>
            </select>
          </div>

          <div class="space-y-1.5">
            <Label for="userPassword" class="font-medium">Contraseña temporal de acceso</Label>
            <Input
              id="userPassword"
              v-model="password"
              type="text"
              required
              class="h-9 text-xs font-mono"
            />
            <p class="text-[11px] text-muted-foreground">
              El usuario deberá actualizarla y registrar su autenticación en dos pasos (TOTP) al iniciar sesión.
            </p>
          </div>
        </CardContent>

        <div class="flex justify-end gap-2 border-t bg-muted/20 p-4">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            :disabled="isBusy"
            @click="emit('update:open', false)"
          >
            Cancelar
          </Button>
          <Button
            type="submit"
            size="sm"
            :disabled="isBusy || !fullName.trim() || !email.trim()"
          >
            <LoaderCircle v-if="isBusy" class="mr-1.5 size-3.5 animate-spin" />
            Registrar Usuario
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>
