<script setup lang="ts">
import type { DueBucket } from '~/types/api'
import { paletteChip, paletteSwatch } from '~/utils/palette'

/** Board filters (instant, over the loaded cards). A bottom sheet on phones; state lives in the URL. */
const open = defineModel<boolean>('open', { default: false })
const emit = defineEmits<{ searchArchived: [] }>()

const { t } = useI18n()
const board = useBoardStore()
const auth = useAuthStore()
const { filters, active, set, toggle, clear } = useBoardFilters()

const dueOptions: readonly DueBucket[] = ['overdue', 'today', 'week', 'none']

const q = computed({
  get: () => filters.value.q,
  set: (value: string) => set({ q: value }),
})

/** Me first, then everyone else in the API's order. */
const people = computed(() => {
  const me = auth.user?.id
  return [...board.members].sort((a, b) => Number(b.userId === me) - Number(a.userId === me))
})
</script>

<template>
  <UiModal v-model:open="open" :title="t('filters.title')" :description="t('filters.hint')">
    <div class="flex flex-col gap-5">
      <UiInput v-model="q" type="search" :label="t('filters.text')" :placeholder="t('filters.textPlaceholder')" autocomplete="off" inputmode="search" />

      <fieldset v-if="board.labels.length">
        <legend class="mb-2 text-sm font-medium">{{ t('filters.labels') }}</legend>
        <div class="flex flex-wrap gap-2">
          <button
            v-for="label in board.labels"
            :key="label.id"
            type="button"
            class="inline-flex min-h-9 items-center gap-2 rounded-lg px-3 text-sm font-medium ring-offset-2 ring-offset-surface transition"
            :class="[paletteChip(label.color), filters.labelIds.includes(label.id) ? 'ring-2 ring-ink' : 'opacity-80 hover:opacity-100']"
            :aria-pressed="filters.labelIds.includes(label.id)"
            @click="toggle('labelIds', label.id)"
          >
            <span class="size-2.5 rounded-full" :class="paletteSwatch(label.color)" aria-hidden="true" />
            <span dir="auto">{{ label.name || t(`colors.${label.color}`) }}</span>
          </button>
        </div>
      </fieldset>

      <fieldset v-if="people.length">
        <legend class="mb-2 text-sm font-medium">{{ t('filters.members') }}</legend>
        <div class="flex flex-wrap gap-2">
          <button
            v-for="member in people"
            :key="member.userId"
            type="button"
            class="inline-flex min-h-10 items-center gap-2 rounded-full border py-1 pe-3 ps-1 text-sm transition-colors"
            :class="filters.assigneeIds.includes(member.userId) ? 'border-accent bg-accent-soft text-accent-strong' : 'border-line hover:bg-subtle'"
            :aria-pressed="filters.assigneeIds.includes(member.userId)"
            @click="toggle('assigneeIds', member.userId)"
          >
            <UiAvatar :name="member.displayName" :src="member.avatarUrl" size="sm" />
            <span dir="auto">{{ member.userId === auth.user?.id ? t('filters.me') : member.displayName }}</span>
          </button>
        </div>
      </fieldset>

      <fieldset>
        <legend class="mb-2 text-sm font-medium">{{ t('filters.due') }}</legend>
        <div class="flex flex-wrap gap-2">
          <button
            v-for="bucket in dueOptions"
            :key="bucket"
            type="button"
            class="min-h-9 rounded-lg border px-3 text-sm transition-colors"
            :class="filters.due === bucket ? 'border-accent bg-accent-soft font-medium text-accent-strong' : 'border-line hover:bg-subtle'"
            :aria-pressed="filters.due === bucket"
            @click="set({ due: filters.due === bucket ? null : bucket })"
          >
            {{ t(`due.bucket.${bucket}`) }}
          </button>
        </div>
      </fieldset>

      <button type="button" class="inline-flex items-center gap-2 self-start text-sm font-medium text-accent-strong underline-offset-2 hover:underline" @click="emit('searchArchived')">
        <UiIcon name="search" :size="16" />
        {{ t('filters.deepSearch') }}
      </button>

      <UiDialogActions>
        <UiButton variant="secondary" :disabled="!active" @click="clear">{{ t('filters.clear') }}</UiButton>
        <UiButton @click="open = false">{{ t('filters.done') }}</UiButton>
      </UiDialogActions>
    </div>
  </UiModal>
</template>
