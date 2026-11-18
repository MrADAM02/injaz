<script setup lang="ts">
/**
 * Native <dialog> (focus trap, Esc, top layer for free). A bottom sheet on phones, a centered card from `sm` up.
 * Content mounts only while open, so forms inside start fresh each time.
 */
const open = defineModel<boolean>('open', { default: false })

const props = withDefaults(
  defineProps<{
    title: string
    description?: string
    size?: 'sm' | 'md' | 'lg' | 'xl'
    dismissible?: boolean
    /** Phones: take the whole screen instead of a bottom sheet (long content such as a card). */
    fullscreen?: boolean
    /** Visually hide the header title (it still labels the dialog), e.g. when the body shows an editable title. */
    hideTitle?: boolean
  }>(),
  { size: 'md', dismissible: true },
)

const dialog = useTemplateRef<HTMLDialogElement>('dialog')
const titleId = useId()
const { t } = useI18n()

function sync() {
  const el = dialog.value
  if (!el) return
  if (open.value && !el.open) el.showModal()
  else if (!open.value && el.open) el.close()
}

watch(open, sync, { flush: 'post' })
onMounted(sync)

function onCancel(event: Event) {
  if (!props.dismissible) event.preventDefault()
}

function onBackdropClick(event: MouseEvent) {
  if (props.dismissible && event.target === dialog.value) open.value = false
}

const widths = { sm: 'sm:max-w-sm', md: 'sm:max-w-lg', lg: 'sm:max-w-2xl', xl: 'sm:max-w-4xl' }
const full = 'max-sm:mt-0 max-sm:h-dvh max-sm:max-h-dvh max-sm:rounded-none'
</script>

<template>
  <dialog
    ref="dialog"
    :aria-labelledby="titleId"
    class="m-0 mt-auto max-h-[92dvh] w-full max-w-none overflow-hidden rounded-t-2xl bg-surface p-0 text-ink shadow-2xl open:animate-sheet-in sm:m-auto sm:rounded-2xl"
    :class="[widths[props.size], props.fullscreen ? full : '']"
    @close="open = false"
    @cancel="onCancel"
    @click="onBackdropClick"
  >
    <div v-if="open" class="flex max-h-[92dvh] flex-col" :class="props.fullscreen ? 'max-sm:h-full max-sm:max-h-dvh max-sm:pt-safe' : ''">
      <div v-if="!props.fullscreen" class="mx-auto mt-2 h-1 w-10 shrink-0 rounded-full bg-line sm:hidden" aria-hidden="true" />
      <header class="flex shrink-0 items-start gap-3 px-5 pb-2 pt-3 sm:pt-5">
        <div class="min-w-0 flex-1">
          <h2 :id="titleId" class="text-match-parent text-lg font-semibold leading-snug" :class="{ 'sr-only': props.hideTitle }" dir="auto">{{ props.title }}</h2>
          <slot name="header" />
          <p v-if="props.description" class="mt-1 text-sm text-muted">{{ props.description }}</p>
        </div>
        <UiButton v-if="props.dismissible" variant="ghost" size="sm" icon="close" :label="t('common.close')" class="-me-2" @click="open = false" />
      </header>
      <div class="min-h-0 flex-1 overflow-y-auto px-5 pb-[max(1.25rem,env(safe-area-inset-bottom))]">
        <slot />
      </div>
    </div>
  </dialog>
</template>
