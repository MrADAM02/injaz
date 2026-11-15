<script setup lang="ts">
import type { PaletteColor } from '~/types/api'
import { defaultBoardColor, paletteColors, paletteFill } from '~/utils/palette'
import { limits, maxLength, required, validateFields } from '~/utils/validate'

/** Rename the board, change its description or color. */
const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const board = useBoardStore()
const toast = useToast()
const form = useFormState(['title', 'description', 'color'] as const)

const title = ref('')
const description = ref('')
const color = ref<PaletteColor>(defaultBoardColor)

watch(open, (value) => {
  if (!value || !board.board) return
  title.value = board.board.title
  description.value = board.board.description ?? ''
  color.value = board.board.color
  form.reset()
})

async function submit() {
  const saved = await form.submit(
    () => board.updateBoard({ title: title.value.trim(), description: description.value.trim() || null, color: color.value }),
    () =>
      validateFields({
        title: [title.value, required, maxLength(limits.boardTitle)],
        description: [description.value, maxLength(limits.boardDescription)],
      }),
  )
  if (saved) {
    open.value = false
    toast.success(t('board.saved'))
  }
}
</script>

<template>
  <UiModal v-model:open="open" :title="t('board.edit')">
    <form novalidate class="flex flex-col gap-4" @submit.prevent="submit">
      <UiAlert v-if="form.formError.value">{{ form.formError.value }}</UiAlert>
      <UiInput v-model="title" :label="t('boards.title')" :error="form.errors.value.title" :maxlength="limits.boardTitle" autocomplete="off" required />
      <fieldset>
        <legend class="mb-2 text-sm font-medium">{{ t('colors.label') }}</legend>
        <div class="flex flex-wrap gap-2">
          <label v-for="option in paletteColors" :key="option" class="cursor-pointer" :title="t(`colors.${option}`)">
            <input v-model="color" type="radio" name="edit-board-color" :value="option" class="peer sr-only">
            <span
              class="flex size-11 items-center justify-center rounded-xl text-white ring-offset-2 ring-offset-surface transition peer-checked:ring-2 peer-checked:ring-ink peer-focus-visible:ring-2 peer-focus-visible:ring-accent"
              :class="paletteFill(option)"
            >
              <UiIcon v-if="color === option" name="check" :size="18" />
            </span>
            <span class="sr-only">{{ t(`colors.${option}`) }}</span>
          </label>
        </div>
      </fieldset>
      <UiTextarea
        v-model="description"
        :label="`${t('boards.description')} (${t('common.optional')})`"
        :error="form.errors.value.description"
        :maxlength="limits.boardDescription"
        :rows="3"
      />
      <UiDialogActions>
        <UiButton variant="secondary" @click="open = false">{{ t('common.cancel') }}</UiButton>
        <UiButton type="submit" :loading="form.pending.value">{{ t('common.save') }}</UiButton>
      </UiDialogActions>
    </form>
  </UiModal>
</template>
