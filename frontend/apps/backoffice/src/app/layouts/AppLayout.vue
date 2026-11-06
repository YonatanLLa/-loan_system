<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ChevronsUpDown, HandCoins, LogOut } from '@lucide/vue'

import { Avatar, AvatarFallback } from '@prestamos/ui/components/ui/avatar'
import { Badge } from '@prestamos/ui/components/ui/badge'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@prestamos/ui/components/ui/dropdown-menu'
import { Separator } from '@prestamos/ui/components/ui/separator'
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarInset,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
  SidebarRail,
  SidebarTrigger,
} from '@prestamos/ui/components/ui/sidebar'

import { buildMenu } from '../navigation'
import { useSessionStore } from '../stores/session'

const route = useRoute()
const router = useRouter()
const session = useSessionStore()

const menu = computed(() => buildMenu(router.getRoutes(), session.can))

const initials = computed(() =>
  (session.user?.fullName ?? '')
    .split(' ')
    .filter(Boolean)
    .map((part) => part.charAt(0))
    .join('')
    .slice(0, 2)
    .toUpperCase(),
)

async function logout() {
  await session.logout()
  await router.replace({ name: 'login' })
}
</script>

<template>
  <SidebarProvider>
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" as-child>
              <RouterLink :to="{ name: 'dashboard' }">
                <div
                  class="flex aspect-square size-8 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground"
                >
                  <HandCoins class="size-4" aria-hidden="true" />
                </div>
                <div class="grid flex-1 text-left text-sm leading-tight">
                  <span class="truncate font-semibold">Sistema de Préstamos</span>
                  <span class="truncate text-xs text-muted-foreground">Backoffice</span>
                </div>
              </RouterLink>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent>
        <SidebarGroup v-for="section in menu" :key="section.group">
          <SidebarGroupLabel>{{ section.group }}</SidebarGroupLabel>
          <SidebarMenu>
            <SidebarMenuItem v-for="item in section.items" :key="item.routeName">
              <SidebarMenuButton
                as-child
                :is-active="route.name === item.routeName"
                :tooltip="item.label"
              >
                <RouterLink :to="{ name: item.routeName }">
                  <component :is="item.icon" aria-hidden="true" />
                  <span>{{ item.label }}</span>
                </RouterLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarGroup>
      </SidebarContent>

      <SidebarFooter>
        <SidebarMenu>
          <SidebarMenuItem>
            <DropdownMenu>
              <DropdownMenuTrigger as-child>
                <SidebarMenuButton size="lg" aria-label="Menú de usuario">
                  <Avatar class="size-8 rounded-lg">
                    <AvatarFallback class="rounded-lg">{{ initials }}</AvatarFallback>
                  </Avatar>
                  <div class="grid flex-1 text-left text-sm leading-tight">
                    <span class="truncate font-medium">{{ session.user?.fullName }}</span>
                    <span class="truncate text-xs text-muted-foreground">{{
                      session.user?.email
                    }}</span>
                  </div>
                  <ChevronsUpDown class="ml-auto" aria-hidden="true" />
                </SidebarMenuButton>
              </DropdownMenuTrigger>
              <DropdownMenuContent side="right" align="end" class="min-w-56">
                <DropdownMenuLabel class="text-xs text-muted-foreground">
                  {{ session.user?.role }}
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem @select="logout">
                  <LogOut aria-hidden="true" />
                  Cerrar sesión
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>

    <SidebarInset>
      <header class="flex h-14 shrink-0 items-center gap-2 border-b px-4">
        <SidebarTrigger class="-ml-1" />
        <Separator orientation="vertical" class="mr-2 h-4" />
        <span class="text-sm font-medium">{{ route.meta.title }}</span>
        <Badge v-if="session.isDemo" variant="outline" class="ml-auto">Modo demo</Badge>
      </header>
      <main class="flex-1 p-4 md:p-6">
        <RouterView />
      </main>
    </SidebarInset>
  </SidebarProvider>
</template>
