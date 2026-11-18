<script setup lang="ts">
import { paletteSwatch } from '~/utils/palette'

/** Toggle the card's assignees or labels; each tap saves right away (the API calls are idempotent). */
const open = defineModel<boolean>('open', { default: false })
const props = defineProps<{ kind: 'members' | 'labels'; selected: readonly string[]; pending: string | null }>()
const emit = defineEmits<{ toggle: [id: string, on: boolean]; manageLabels: [] }>()

const { t } = useI18n()
const board = useBoardStore()
const auth = useAuthStore()

const options = computed(() =>
  props.kind === 'members'
    ? [...board.members]
        .sort((a, b) => Number(b.userId === auth.user?.id) - Number(a.userId === auth.user?.id))
        .map((m) => ({ id: m.userId, label: m.userId === auth.user?.id ? `${m.displayName} (${t('common.you')})` : m.displayName, member: m, label_: null }))
    : board.labels.map((l) => ({ id: l.id, label: l.name || t(`colors.${l.color}`), member: null, label_: l })),
)
</script>

<template>
  <UiModal v-model:open="open" :title="props.kind === 'members' ? t('card.members') : t('card.labels')" size="sm">
    <p v-if="options.length === 0" class="py-2 text-sm text-muted">{{ props.kind === 'members' ? t('card.noMembers') : t('labels.none') }}</p>
    <ul class="-mx-2 flex flex-col">
      <li v-for="option in options" :key="option.id">
        <label class="flex min-h-12 cursor-pointer items-center gap-3 rounded-xl px-2 hover:bg-subtle">
          <input
            type="checkbox"
            class="size-5 shrink-0 rounded accent-(--color-accent)"
            :checked="props.selected.includes(option.id)"
            :disabled="props.pending === option.id"
            @change="emit('toggle', option.id, !props.selected.includes(option.id))"
          >
          <UiAvatar v-if="option.member" :name="option.member.displayName" :src="option.member.avatarUrl" size="sm" />
          <span v-else-if="option.label_" class="h-7 w-10 shrink-0 rounded-md" :class="paletteSwatch(option.label_.color)" aria-hidden="true" />
          <span class="min-w-0 flex-1 truncate text-match-parent text-sm" dir="auto">{{ option.label }}</span>
          <UiSpinner v-if="props.pending === option.id" :size="16" />
        </label>
      </li>
    </ul>
    <UiDialogActions>
      <UiButton v-if="props.kind === 'labels' && board.allows('editContent')" variant="secondary" icon="tag" @click="emit('manageLabels')">{{ t('labels.manage') }}</UiButton>
      <UiButton @click="open = false">{{ t('filters.done') }}</UiButton>
    </UiDialogActions>
  </UiModal>
</template>
