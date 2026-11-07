<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { Menu, X, Zap } from '@lucide/vue'
import { ref } from 'vue'

const mobileMenuOpen = ref(false)
</script>

<template>
  <div class="min-h-screen bg-[#070F1E] text-slate-900 flex flex-col">

    <!-- Header público estilo CrediYa -->
    <header class="sticky top-0 left-0 w-screen z-50 bg-[#070F1E]/95 backdrop-blur-md text-white border-b border-white/10 shadow-lg">
      <div class="max-w-6xl mx-auto px-4 lg:px-8 py-3.5 flex items-center justify-between gap-4">

        <!-- Logo CrediYa -->
        <RouterLink to="/" class="flex items-center gap-2.5 shrink-0 group">
          <div class="h-9 w-9 rounded-xl bg-gradient-to-br from-blue-500 to-sky-400 flex items-center justify-center font-bold text-white shadow-md shadow-blue-500/25 group-hover:scale-105 transition-transform">
            <Zap class="w-5 h-5 fill-white text-white" />
          </div>
          <div>
            <p class="font-black text-lg leading-none text-white tracking-tight">
              Credi<span class="text-sky-400">Ya</span>
            </p>
            <p class="text-[9px] text-slate-400 font-bold tracking-widest uppercase leading-none mt-1">
              Finanzas Digitales
            </p>
          </div>
        </RouterLink>

        <!-- Navegación central desktop -->
        <nav class="hidden md:flex items-center gap-6 text-xs font-semibold text-slate-300">
          <a href="/#simulador" class="hover:text-white transition-colors">Simulador</a>
          <a href="/#como-funciona" class="hover:text-white transition-colors">¿Cómo funciona?</a>
          <a href="/#beneficios" class="hover:text-white transition-colors">Tasas y Beneficios</a>
          <a href="/#faq" class="hover:text-white transition-colors">Preguntas Frecuentes</a>
        </nav>

        <!-- Acciones desktop -->
        <div class="hidden sm:flex items-center gap-3">
          <RouterLink
            to="/login"
            class="px-3.5 py-2 rounded-xl text-xs font-semibold text-slate-200 hover:text-white hover:bg-white/5 transition-colors"
          >
            Iniciar Sesión
          </RouterLink>
          <RouterLink
            to="/registro"
            class="px-5 py-2.5 rounded-xl text-xs font-bold bg-brand-primary hover:bg-brand-primary-dark text-white transition-all shadow-md shadow-blue-600/30 hover:shadow-lg hover:-translate-y-0.5"
          >
            Acceso Clientes
          </RouterLink>
        </div>

        <!-- Hamburguesa mobile -->
        <button
          type="button"
          class="sm:hidden p-2 rounded-lg text-slate-300 hover:text-white hover:bg-white/10 transition-colors"
          @click="mobileMenuOpen = !mobileMenuOpen"
        >
          <X v-if="mobileMenuOpen" class="w-5 h-5" />
          <Menu v-else class="w-5 h-5" />
        </button>
      </div>

      <!-- Menú mobile desplegable -->
      <div v-if="mobileMenuOpen" class="sm:hidden border-t border-white/10 bg-[#0B172B] px-4 py-4 space-y-3">
        <nav class="flex flex-col space-y-2 text-sm font-medium text-slate-300 pb-2 border-b border-white/10">
          <a href="/#simulador" class="py-1 hover:text-white transition-colors" @click="mobileMenuOpen = false">Simulador</a>
          <a href="/#como-funciona" class="py-1 hover:text-white transition-colors" @click="mobileMenuOpen = false">¿Cómo funciona?</a>
          <a href="/#beneficios" class="py-1 hover:text-white transition-colors" @click="mobileMenuOpen = false">Tasas y Beneficios</a>
          <a href="/#faq" class="py-1 hover:text-white transition-colors" @click="mobileMenuOpen = false">Preguntas Frecuentes</a>
        </nav>
        <div class="flex flex-col gap-2 pt-1">
          <RouterLink
            to="/login"
            class="block px-4 py-2.5 rounded-xl text-sm font-medium text-white/80 hover:bg-white/10 text-center transition-colors"
            @click="mobileMenuOpen = false"
          >
            Iniciar Sesión
          </RouterLink>
          <RouterLink
            to="/registro"
            class="block px-4 py-2.5 rounded-xl text-sm font-bold bg-brand-primary hover:bg-brand-primary-dark text-white transition-colors text-center shadow-md shadow-blue-600/30"
            @click="mobileMenuOpen = false"
          >
            Acceso Clientes
          </RouterLink>
        </div>
      </div>
    </header>

    <!-- Contenido de la página -->
    <main class="flex-1">
      <RouterView />
    </main>

  </div>
</template>
