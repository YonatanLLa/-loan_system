<script setup lang="ts">
import { ref } from 'vue'

import { toApiError, type User } from '@prestamos/api-client'
import { WelcomeCard } from '@prestamos/ui'
import { useBusyAction } from '@prestamos/ui'

import { useUsersApi } from '@/app/users'

// Vista de bienvenida de prueba (el dashboard de KPIs, FE-BO-37, se construye después).
const user = ref<User | null>(null)
const error = ref<string | null>(null)

const { run, isBusy } = useBusyAction(async () => {
  error.value = null
  try {
    user.value = await useUsersApi().getCurrentUser()
  } catch (e) {
    error.value = toApiError(e).detail
  }
})
</script>

<template>
  <div class="grid min-h-[60vh] place-items-center">
    <WelcomeCard app-name="el Backoffice" :user="user" :loading="isBusy" :error="error" @load="run" />
  </div>
</template>
