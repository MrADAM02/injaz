<script setup lang="ts">
/** Toast stack: above the bottom tab bar on phones, bottom end corner on larger screens. */
const store = useToastStore()
const { t } = useI18n()

const tones = {
  success: { icon: 'check', class: 'text-success' },
  error: { icon: 'alert', class: 'text-danger' },
  info: { icon: 'info', class: 'text-accent' },
} as const
</script>

<template>
  <div
    class="pointer-events-none fixed inset-x-0 bottom-[calc(4.5rem+env(safe-area-inset-bottom))] z-50 flex flex-col items-center gap-2 px-4 md:bottom-6 md:items-end md:px-6"
    aria-live="polite"
    aria-atomic="false"
  >
    <TransitionGroup
      enter-from-class="opacity-0 translate-y-2"
      enter-active-class="transition duration-200"
      leave-to-class="opacity-0"
      leave-active-class="transition duration-150"
    >
      <div
        v-for="toast in store.items"
        :key="toast.id"
        :role="toast.tone === 'error' ? 'alert' : 'status'"
        class="pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-xl border border-line bg-surface px-4 py-3 text-sm shadow-lg"
      >
        <UiIcon :name="tones[toast.tone].icon" :class="tones[toast.tone].class" class="mt-0.5" />
        <p class="min-w-0 flex-1">{{ toast.translate ? t(toast.message) : toast.message }}</p>
        <button
          type="button"
          class="-m-1 rounded-md p-1 text-muted hover:text-ink"
          :aria-label="t('common.dismiss')"
          @click="store.dismiss(toast.id)"
        >
          <UiIcon name="close" :size="16" />
        </button>
      </div>
    </TransitionGroup>
  </div>
</template>
