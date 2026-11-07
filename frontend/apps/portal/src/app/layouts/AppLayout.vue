<script setup lang="ts">
import { Bell, BookOpen, CreditCard, FilePlus, House, LogOut, User, Wallet, Zap } from '@lucide/vue'

import { useSessionStore } from '@/app/stores/session'
import { useClientAuth } from '@/features/onboarding'

const session = useSessionStore()
const { logout } = useClientAuth()

const navItems = [
  { name: 'home', label: 'Inicio', icon: House, to: '/inicio' },
  { name: 'loans', label: 'Préstamos', icon: CreditCard, to: '/prestamos' },
  { name: 'apply', label: 'Solicitar', icon: FilePlus, to: '/solicitar' },
  { name: 'pay', label: 'Pagar', icon: Wallet, to: '/pagar' },
  { name: 'profile', label: 'Perfil', icon: User, to: '/perfil' },
]
</script>

<template>
  <div class="h-screen flex flex-col bg-background text-foreground overflow-hidden">

    <!-- Header: siempre ancho completo (100%), fuera del área de scroll -->
    <header class="shrink-0 z-40 w-full border-b border-brand-navy/20 bg-brand-navy text-white shadow-md px-4 lg:px-8 py-3">
      <div class="max-w-6xl mx-auto flex items-center justify-between gap-6">

        <!-- Logo CrediYa -->
        <RouterLink
          :to="session.isAuthenticated ? '/inicio' : '/'"
          class="flex items-center gap-2.5 shrink-0 group"
        >
          <div class="h-9 w-9 rounded-xl bg-gradient-to-br from-blue-500 to-sky-400 flex items-center justify-center font-bold text-white shadow-md shadow-blue-500/20 group-hover:scale-105 transition-transform">
            <Zap class="w-5 h-5 fill-white text-white" />
          </div>
          <div>
            <p class="font-black text-base leading-none text-white tracking-tight">
              Credi<span class="text-sky-400">Ya</span>
            </p>
            <p class="text-[9px] text-slate-400 font-bold tracking-widest uppercase leading-none mt-1">
              Portal Prestatario
            </p>
          </div>
        </RouterLink>

        <!-- Nav horizontal (solo desktop y autenticado) -->
        <nav v-if="session.isAuthenticated" class="hidden lg:flex items-center gap-6 flex-1 ml-4">
          <RouterLink
            v-for="item in navItems"
            :key="item.name"
            :to="item.to"
            class="text-sm font-medium text-slate-300 hover:text-white transition-colors py-1"
            active-class="!text-sky-400 font-bold"
          >
            {{ item.label }}
          </RouterLink>
        </nav>

        <!-- Acciones derechas (autenticado: Bell + Salir / público: Iniciar Sesión + Registro) -->
        <div v-if="session.isAuthenticated" class="flex items-center gap-2 shrink-0">
          <RouterLink
            to="/notificaciones"
            class="relative p-2 rounded-full hover:bg-white/10 text-white/80 hover:text-white transition-colors"
            title="Notificaciones"
          >
            <Bell class="w-5 h-5" />
            <span class="absolute top-1.5 right-1.5 w-2 h-2 rounded-full bg-brand-accent ring-2 ring-brand-navy"></span>
          </RouterLink>
          <button
            type="button"
            class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold text-white/80 hover:bg-white/10 hover:text-white transition-colors"
            title="Cerrar sesión"
            @click="logout"
          >
            <LogOut class="w-4 h-4" />
            <span class="hidden sm:inline">Salir</span>
          </button>
        </div>
        <div v-else class="flex items-center gap-2 shrink-0">
          <RouterLink
            to="/login"
            class="px-3 py-1.5 rounded-lg text-xs font-semibold text-white/85 hover:bg-white/10 hover:text-white transition-colors"
          >
            Iniciar Sesión
          </RouterLink>
          <RouterLink
            to="/registro"
            class="px-3 py-1.5 rounded-lg text-xs font-semibold bg-brand-primary text-white hover:bg-brand-primary-dark transition-colors shadow-sm"
          >
            Crear Cuenta
          </RouterLink>
        </div>
      </div>
    </header>

    <!-- Contenedor con scroll propio y reserva de canal para evitar layout-shifts -->
    <div id="app-scroll-container" class="flex-1 overflow-y-auto overflow-x-hidden [scrollbar-gutter:stable]">
      <!-- Main content -->
      <main class="app-content w-full max-w-6xl mx-auto px-4 lg:px-8 py-4 lg:py-6 pb-24 lg:pb-6">
        <RouterView />
      </main>

      <!-- Footer -->
      <footer class="border-t border-border px-4 lg:px-8 py-4 text-xs text-muted-foreground">
        <div class="max-w-6xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-3">
          <p class="text-[11px]">© 2026 Fintech Préstamos S.A.C. Todos los derechos reservados.</p>
          <RouterLink
            to="/libro-de-reclamaciones"
            class="inline-flex items-center gap-1.5 font-semibold text-primary hover:underline"
          >
            <BookOpen class="h-3.5 w-3.5" />
            <span>Libro de Reclamaciones Virtual</span>
          </RouterLink>
        </div>
      </footer>
    </div>

    <!-- Bottom nav bar (solo mobile y autenticado) -->
    <nav
      v-if="session.isAuthenticated"
      class="lg:hidden fixed bottom-0 left-0 right-0 z-50 border-t border-brand-navy/20 bg-brand-navy text-white py-2 px-4 shadow-xl"
    >
      <div class="flex items-center justify-around">
        <RouterLink
          v-for="item in navItems"
          :key="item.name"
          :to="item.to"
          class="flex flex-col items-center gap-1 text-[11px] font-medium text-white/70 hover:text-white transition-colors py-1 px-3 rounded-lg"
          active-class="!text-brand-accent !bg-white/10 font-semibold"
        >
          <component :is="item.icon" class="w-5 h-5" />
          <span>{{ item.label }}</span>
        </RouterLink>
      </div>
    </nav>

  </div>
</template>
