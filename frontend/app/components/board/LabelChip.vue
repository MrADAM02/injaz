<script setup lang="ts">
import type { Label } from '~/types/api'
import { paletteChip, paletteSwatch } from '~/utils/palette'

/** A label: a soft chip with its name, or a short color bar when it has none (`compact` always a bar). */
const props = defineProps<{ label: Label; compact?: boolean }>()
const { t } = useI18n()
const name = computed(() => props.label.name || t(`colors.${props.label.color}`))
</script>

<template>
  <span
    v-if="props.compact || !props.label.name"
    class="inline-block h-2 w-9 rounded-full"
    :class="paletteSwatch(props.label.color)"
    :title="name"
    role="img"
    :aria-label="name"
  />
  <span
    v-else
    class="inline-flex max-w-full items-center truncate rounded-md px-2 py-0.5 text-xs font-medium"
    :class="paletteChip(props.label.color)"
    dir="auto"
  >{{ props.label.name }}</span>
</template>
