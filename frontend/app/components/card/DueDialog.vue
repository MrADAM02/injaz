<script setup lang="ts">
import { fromLocalInput, toLocalInput } from '~/utils/cards'

/** Set or clear the due date (native date-time picker: the system wheel on phones). */
const open = defineModel<boolean>('open', { default: false })
const props = defineProps<{ dueAt: string | null; save: (dueAt: string | null) => Promise<boolean> }>()

const { t } = useI18n()
const value = ref('')
const pending = ref(false)
const id = useId()

watch(open, (isOpen) => {
  if (!isOpen) return
  if (props.dueAt) {
    value.value = toLocalInput(props.dueAt)
  } else {
    // Default: tomorrow at 17:00, a sensible starting point the picker can adjust.
    const tomorrow = new Date()
    tomorrow.setDate(tomorrow.getDate() + 1)
    tomorrow.setHours(17, 0, 0, 0)
    value.value = toLocalInput(tomorrow.toISOString())
  }
})

async function submit(next: string | null) {
  if (pending.value) return
  pending.value = true
  try {
    if (await props.save(next)) open.value = false
  } finally {
    pending.value = false
  }
}
</script>

<template>
  <UiModal v-model:open="open" :title="t('card.dueDate')" size="sm">
    <form class="flex flex-col gap-4" @submit.prevent="submit(fromLocalInput(value))">
      <div class="flex flex-col gap-1.5">
        <label :for="id" class="text-sm font-medium">{{ t('card.dueDate') }}</label>
        <input
          :id="id"
          v-model="value"
          type="datetime-local"
          required
          class="block min-h-11 w-full rounded-xl border border-line bg-surface px-3.5 py-2 focus:border-accent focus:outline-none focus:ring-3 focus:ring-accent/20"
        >
      </div>
      <UiDialogActions>
        <UiButton v-if="props.dueAt" variant="danger-ghost" :disabled="pending" @click="submit(null)">{{ t('card.removeDue') }}</UiButton>
        <UiButton type="submit" :loading="pending" :disabled="!value">{{ t('common.save') }}</UiButton>
      </UiDialogActions>
    </form>
  </UiModal>
</template>
