<script setup lang="ts">
import { Bell, FilePlus, House, User, Wallet } from '@lucide/vue'

const navItems = [
  { name: 'home', label: 'Inicio', icon: House, to: '/' },
  { name: 'apply', label: 'Solicitar', icon: FilePlus, to: '/solicitar' },
  { name: 'pay', label: 'Pagar', icon: Wallet, to: '/pagar' },
  { name: 'profile', label: 'Perfil', icon: User, to: '/perfil' },
]
</script>

<template>
  <div class="min-h-screen bg-background text-foreground flex flex-col justify-between pb-20 sm:pb-0">
    <!-- Top Header -->
    <header class="sticky top-0 z-40 border-b border-border bg-card/80 backdrop-blur-md px-4 py-3">
      <div class="max-w-md mx-auto flex items-center justify-between">
        <div class="flex items-center gap-2">
          <div class="h-8 w-8 rounded-lg bg-primary flex items-center justify-center font-bold text-primary-foreground text-sm">
            FN
          </div>
          <div>
            <h1 class="font-bold text-sm leading-tight">Portal Prestatario</h1>
            <p class="text-[10px] text-muted-foreground leading-none">Fintech Digital</p>
          </div>
        </div>

        <RouterLink
          to="/notificaciones"
          class="relative p-2 rounded-full hover:bg-muted text-muted-foreground hover:text-foreground transition-colors"
          title="Notificaciones"
        >
          <Bell class="w-5 h-5" />
          <span class="absolute top-1.5 right-1.5 w-2 h-2 rounded-full bg-primary ring-2 ring-background"></span>
        </RouterLink>
      </div>
    </header>

    <!-- Main Content Area -->
    <main class="flex-1 max-w-md w-full mx-auto p-4">
      <RouterView />
    </main>

    <!-- Bottom Navigation Bar (Mobile First) -->
    <nav class="fixed bottom-0 left-0 right-0 z-50 border-t border-border bg-card/95 backdrop-blur-md py-2 px-4 shadow-lg sm:max-w-md sm:mx-auto sm:border-x">
      <div class="flex items-center justify-around">
        <RouterLink
          v-for="item in navItems"
          :key="item.name"
          :to="item.to"
          class="flex flex-col items-center gap-1 text-[11px] font-medium text-muted-foreground hover:text-primary transition-colors py-1 px-3 rounded-lg"
          active-class="text-primary font-semibold"
        >
          <component :is="item.icon" class="w-5 h-5" />
          <span>{{ item.label }}</span>
        </RouterLink>
      </div>
    </nav>
  </div>
</template>
