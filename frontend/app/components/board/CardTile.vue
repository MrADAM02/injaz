<script setup lang="ts">
import type { RouteLocationRaw } from 'vue-router'
import type { CardSummary } from '~/types/api'
import { dueState, dueTone, priorityTone } from '~/utils/cards'

/** A card on the board (and in search results): labels, title, and badges for due/priority/checklist/comments/people. */
const props = defineProps<{ card: CardSummary; to: RouteLocationRaw; draggable?: boolean }>()

const { t } = useI18n()
const board = useBoardStore()
const format = useFormat()

const labels = computed(() => props.card.labelIds.map((id) => board.labelById.get(id)).filter((l) => l !== undefined))
const people = computed(() => props.card.assigneeIds.map((id) => board.memberById.get(id)).filter((m) => m !== undefined))
const due = computed(() => dueState(props.card.dueAt, props.card.completedAt))
const completed = computed(() => props.card.completedAt !== null)
const checklistDone = computed(() => props.card.checklistTotal > 0 && props.card.checklistDone === props.card.checklistTotal)
</script>

<template>
  <NuxtLink
    :to="props.to"
    draggable="false"
    class="no-callout group relative block rounded-xl border border-line bg-surface p-3 text-start shadow-xs transition-[box-shadow,border-color] hover:border-accent/40 hover:shadow-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
    :class="props.draggable ? 'cursor-grab active:cursor-grabbing' : ''"
  >
    <div v-if="labels.length" class="mb-2 flex flex-wrap gap-1">
      <BoardLabelChip v-for="label in labels" :key="label.id" :label="label" />
    </div>

    <p class="flex items-start gap-1.5 text-sm font-medium leading-snug" :class="{ 'text-muted line-through decoration-1': completed }">
      <UiIcon v-if="completed" name="checkCircle" :size="16" class="mt-0.5 text-success" />
      <span class="min-w-0 break-words text-match-parent" dir="auto">{{ props.card.title }}</span>
    </p>

    <div
      v-if="due || props.card.priority !== 'none' || props.card.hasDescription || props.card.checklistTotal || props.card.commentCount || people.length"
      class="mt-2 flex flex-wrap items-center gap-x-2 gap-y-1.5 text-xs text-muted"
    >
      <span v-if="due" class="inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 font-medium" :class="dueTone[due]">
        <UiIcon name="clock" :size="13" />
        {{ format.dateShort(props.card.dueAt) }}
        <span class="sr-only">{{ t(`due.state.${due}`) }}</span>
      </span>
      <span v-if="props.card.priority !== 'none'" class="inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 font-medium" :class="priorityTone[props.card.priority]">
        <UiIcon name="flag" :size="13" />
        {{ t(`priority.${props.card.priority}`) }}
      </span>
      <span v-if="props.card.hasDescription" :title="t('card.hasDescription')">
        <UiIcon name="description" :size="15" />
        <span class="sr-only">{{ t('card.hasDescription') }}</span>
      </span>
      <span
        v-if="props.card.checklistTotal"
        class="inline-flex items-center gap-1"
        :class="checklistDone ? 'rounded-md bg-green-100 px-1.5 py-0.5 font-medium text-green-900 dark:bg-green-950 dark:text-green-200' : ''"
        :title="t('card.checklist')"
      >
        <UiIcon name="checklist" :size="14" />
        <span dir="ltr">{{ props.card.checklistDone }}/{{ props.card.checklistTotal }}</span>
      </span>
      <span v-if="props.card.commentCount" class="inline-flex items-center gap-1" :title="t('card.comments')">
        <UiIcon name="comment" :size="14" />
        {{ props.card.commentCount }}
      </span>
      <span v-if="people.length" class="ms-auto flex -space-x-1.5">
        <UiAvatar v-for="person in people.slice(0, 3)" :key="person.userId" :name="person.displayName" :src="person.avatarUrl" size="xs" class="ring-2 ring-surface" />
        <span v-if="people.length > 3" class="flex size-6 items-center justify-center rounded-full bg-subtle text-[10px] font-semibold ring-2 ring-surface">+{{ people.length - 3 }}</span>
      </span>
    </div>
  </NuxtLink>
</template>
