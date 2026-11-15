<script setup lang="ts">
import type { BoardSummary } from '~/types/api'
import { paletteFill } from '~/utils/palette'

const props = defineProps<{ board: BoardSummary }>()
const { t } = useI18n()
</script>

<template>
  <NuxtLink
    :to="`/b/${props.board.id}`"
    class="group relative flex h-28 flex-col justify-between overflow-hidden rounded-2xl p-4 text-white shadow-sm transition hover:shadow-md focus-visible:ring-4 focus-visible:ring-accent/30"
    :class="[paletteFill(props.board.color), { 'opacity-70': props.board.isArchived }]"
  >
    <span class="absolute inset-0 bg-linear-to-b from-white/10 to-black/15" aria-hidden="true" />
    <span class="relative line-clamp-2 text-base font-semibold leading-snug">{{ props.board.title }}</span>
    <span class="relative flex items-center justify-between gap-2 text-xs text-white/85">
      <span class="truncate">{{ props.board.description }}</span>
      <span v-if="props.board.isArchived" class="inline-flex shrink-0 items-center gap-1 rounded-full bg-black/25 px-2 py-0.5">
        <UiIcon name="archive" :size="12" />
        {{ t('boards.archived') }}
      </span>
    </span>
  </NuxtLink>
</template>
