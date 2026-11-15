<script setup lang="ts">
import type { WorkspaceRole } from '~/types/api'

const props = defineProps<{ role: WorkspaceRole }>()
const { t } = useI18n()

const tones: Record<WorkspaceRole, string> = {
  owner: 'bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300',
  admin: 'bg-accent-soft text-accent-strong',
  member: 'bg-subtle text-ink',
  viewer: 'bg-subtle text-muted',
}
</script>

<template>
  <span class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium" :class="tones[props.role]">
    <UiIcon v-if="props.role === 'owner'" name="crown" :size="12" />
    {{ t(`roles.${props.role}`) }}
  </span>
</template>
