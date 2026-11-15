<script setup lang="ts">
import { directionalIcons, iconPaths, type IconName } from '~/utils/icons'

const props = withDefaults(defineProps<{ name: IconName; size?: number }>(), { size: 20 })
</script>

<template>
  <svg
    :width="props.size"
    :height="props.size"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.8"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
    focusable="false"
    class="shrink-0"
    :class="{ 'rtl:-scale-x-100': directionalIcons.has(props.name) }"
  >
    <path :d="iconPaths[props.name]" />
  </svg>
</template>
