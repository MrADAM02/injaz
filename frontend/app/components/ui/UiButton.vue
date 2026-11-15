<script setup lang="ts">
import type { RouteLocationRaw } from 'vue-router'
import type { IconName } from '~/utils/icons'

const props = withDefaults(
  defineProps<{
    variant?: 'primary' | 'secondary' | 'ghost' | 'danger' | 'danger-ghost'
    size?: 'md' | 'sm'
    type?: 'button' | 'submit'
    to?: RouteLocationRaw
    icon?: IconName
    loading?: boolean
    disabled?: boolean
    block?: boolean
    /** Accessible name for icon-only buttons. */
    label?: string
  }>(),
  { variant: 'primary', size: 'md', type: 'button' },
)

const variants = {
  primary: 'bg-accent text-on-accent hover:bg-accent-strong shadow-sm',
  secondary: 'bg-surface text-ink border border-line hover:bg-subtle',
  ghost: 'text-ink hover:bg-subtle',
  danger: 'bg-danger text-white hover:opacity-90 shadow-sm dark:text-canvas',
  'danger-ghost': 'text-danger hover:bg-danger-soft',
}

// 44px tap targets for the default size; the small size still keeps 36px.
const sizes = {
  md: 'min-h-11 px-4 text-sm gap-2 rounded-xl',
  sm: 'min-h-9 px-3 text-sm gap-1.5 rounded-lg',
}

const slots = useSlots()
const iconOnly = computed(() => !!props.icon && !slots.default)
const classes = computed(() => [
  'inline-flex items-center justify-center font-medium transition-colors select-none',
  'disabled:opacity-50 disabled:pointer-events-none',
  variants[props.variant],
  sizes[props.size],
  iconOnly.value ? (props.size === 'md' ? 'min-w-11 px-0' : 'min-w-9 px-0') : '',
  props.block ? 'w-full' : '',
])
</script>

<template>
  <NuxtLink v-if="props.to && !props.disabled" :to="props.to" :class="classes" :aria-label="props.label">
    <UiIcon v-if="props.icon" :name="props.icon" :size="18" />
    <slot />
  </NuxtLink>
  <button
    v-else
    :type="props.type"
    :class="classes"
    :disabled="props.disabled || props.loading"
    :aria-busy="props.loading || undefined"
    :aria-label="props.label"
  >
    <UiSpinner v-if="props.loading" :size="16" />
    <UiIcon v-else-if="props.icon" :name="props.icon" :size="18" />
    <slot />
  </button>
</template>
