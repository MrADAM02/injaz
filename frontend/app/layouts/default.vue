<script setup lang="ts">
/**
 * Signed-in shell. md+: fixed sidebar on the start edge. Phones: top bar + drawer (slides in from the start
 * edge, i.e. the right in Arabic) + bottom tab bar.
 */
const ui = useUiStore()
const workspaces = useWorkspacesStore()
const route = useRoute()
const toast = useToast()
const { t } = useI18n()

onMounted(() => {
  workspaces.ensureLoaded().catch((error: unknown) => toast.error(error))
})

watch(
  () => route.fullPath,
  () => {
    ui.drawerOpen = false
  },
)

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && ui.drawerOpen) ui.drawerOpen = false
}
onMounted(() => window.addEventListener('keydown', onKeydown))
onBeforeUnmount(() => window.removeEventListener('keydown', onKeydown))
</script>

<template>
  <div class="min-h-dvh md:ps-64">
    <aside class="fixed inset-y-0 inset-s-0 z-20 hidden w-64 border-e border-line bg-surface md:block">
      <LayoutSidebar />
    </aside>

    <!-- Drawer (phones) -->
    <Transition enter-from-class="opacity-0" enter-active-class="transition-opacity duration-200" leave-to-class="opacity-0" leave-active-class="transition-opacity duration-150">
      <div v-if="ui.drawerOpen" class="fixed inset-0 z-40 bg-backdrop md:hidden" aria-hidden="true" @click="ui.drawerOpen = false" />
    </Transition>
    <aside
      class="fixed inset-y-0 inset-s-0 z-50 w-72 max-w-[85vw] bg-surface pt-safe transition-[translate,transform,visibility] duration-200 ease-out md:hidden"
      :class="ui.drawerOpen ? 'translate-x-0 shadow-2xl' : 'invisible -translate-x-full rtl:translate-x-full'"
      :inert="!ui.drawerOpen"
      :aria-hidden="!ui.drawerOpen"
    >
      <LayoutSidebar>
        <template #close>
          <UiButton variant="ghost" icon="close" :label="t('nav.closeMenu')" @click="ui.drawerOpen = false" />
        </template>
      </LayoutSidebar>
    </aside>

    <LayoutTopbar />

    <!-- Full-bleed pages (the board) get exactly the space between the top bar and the tab bar. -->
    <main
      v-if="route.meta.fullBleed"
      class="flex h-[calc(100dvh-7.5rem-env(safe-area-inset-top)-env(safe-area-inset-bottom))] min-h-0 flex-col md:h-dvh"
    >
      <slot />
    </main>
    <main v-else class="mx-auto w-full max-w-6xl px-4 pb-[calc(5rem+env(safe-area-inset-bottom))] pt-4 sm:px-6 md:pb-10 md:pt-8 lg:px-10">
      <slot />
    </main>

    <LayoutBottomNav />
    <WorkspaceCreateDialog v-model:open="ui.createWorkspaceOpen" />
  </div>
</template>
