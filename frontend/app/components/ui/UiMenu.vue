<script setup lang="ts">
import type { MenuEntry, MenuItem } from '~/types/ui'
import type { IconName } from '~/utils/icons'

/**
 * An action menu on the Popover API (top layer, Esc and outside-click to close). A bottom sheet on phones; on
 * larger screens it hangs below the trigger, aligned to its inline-end edge (so it opens inwards in both
 * directions). Arrow keys move between items.
 */
const props = withDefaults(
  defineProps<{ items: readonly MenuEntry[]; label: string; title?: string; icon?: IconName; size?: 'md' | 'sm'; variant?: 'ghost' | 'secondary' }>(),
  { icon: 'more', size: 'sm', variant: 'ghost' },
)

const id = useId()
const trigger = useTemplateRef<HTMLElement>('trigger')
const menu = useTemplateRef<HTMLElement>('menu')
const open = ref(false)
const visible = computed(() => props.items.filter((item): item is MenuItem => !!item))

function place() {
  const el = menu.value
  const anchor = trigger.value
  if (!el || !anchor) return
  if (!window.matchMedia('(min-width: 640px)').matches) {
    el.style.removeProperty('top')
    el.style.removeProperty('inset-inline-end')
    return
  }
  const rect = anchor.getBoundingClientRect()
  const rtl = getComputedStyle(anchor).direction === 'rtl'
  // Distance from the trigger's inline-end edge to the viewport edge on that side.
  const end = rtl ? rect.x : window.innerWidth - (rect.x + rect.width)
  el.style.setProperty('inset-inline-end', `${Math.max(8, end)}px`)
  const height = el.offsetHeight
  const below = rect.bottom + 6
  el.style.top = `${below + height > window.innerHeight - 8 ? Math.max(8, rect.top - height - 6) : below}px`
}

function items(): HTMLElement[] {
  return Array.from(menu.value?.querySelectorAll<HTMLElement>('[role="menuitem"]:not([disabled])') ?? [])
}

function onToggle(event: Event) {
  open.value = (event as ToggleEvent).newState === 'open'
  if (open.value) {
    place()
    items()[0]?.focus()
  } else if (menu.value?.contains(document.activeElement)) {
    trigger.value?.querySelector('button')?.focus()
  }
}

function choose(item: MenuItem) {
  menu.value?.hidePopover()
  item.action()
}

function onKeydown(event: KeyboardEvent) {
  const all = items()
  const index = all.indexOf(document.activeElement as HTMLElement)
  const go = (next: number) => {
    event.preventDefault()
    all[(next + all.length) % all.length]?.focus()
  }
  if (event.key === 'ArrowDown') go(index + 1)
  else if (event.key === 'ArrowUp') go(index - 1)
  else if (event.key === 'Home') go(0)
  else if (event.key === 'End') go(all.length - 1)
  else if (event.key === 'Tab') menu.value?.hidePopover()
}
</script>

<template>
  <span ref="trigger" class="inline-flex">
    <UiButton
      :variant="props.variant"
      :size="props.size"
      :icon="props.icon"
      :label="props.label"
      :title="props.label"
      aria-haspopup="menu"
      :aria-expanded="open"
      :aria-controls="id"
      :popovertarget="id"
    />
  </span>
  <div
    :id="id"
    ref="menu"
    popover="auto"
    role="menu"
    :aria-label="props.title ?? props.label"
    class="m-0 inset-auto max-sm:inset-x-0 max-sm:bottom-0 max-sm:w-full max-sm:rounded-t-2xl max-sm:pb-[max(0.5rem,env(safe-area-inset-bottom))] min-w-52 max-w-[calc(100vw-1rem)] rounded-xl border border-line bg-surface p-1.5 text-ink shadow-xl open:animate-fade-in max-sm:open:animate-sheet-in"
    @toggle="onToggle"
    @keydown="onKeydown"
  >
    <p v-if="props.title" class="px-3 pb-1 pt-2 text-xs font-semibold text-muted sm:hidden">{{ props.title }}</p>
    <button
      v-for="(item, index) in visible"
      :key="index"
      type="button"
      role="menuitem"
      :disabled="item.disabled"
      class="flex min-h-11 w-full items-center gap-3 rounded-lg px-3 text-start text-sm transition-colors hover:bg-subtle focus:bg-subtle focus:outline-none disabled:opacity-50 sm:min-h-10"
      :class="item.danger ? 'text-danger' : ''"
      @click="choose(item)"
    >
      <UiIcon v-if="item.icon" :name="item.icon" :size="18" />
      <span class="min-w-0 flex-1 truncate">{{ item.label }}</span>
    </button>
  </div>
</template>
