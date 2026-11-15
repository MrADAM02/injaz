<script setup lang="ts">
import type { BoardSummary, PaletteColor } from '~/types/api'
import { defaultBoardColor, paletteColors, paletteFill } from '~/utils/palette'
import { limits, maxLength, required, validateFields } from '~/utils/validate'

const open = defineModel<boolean>('open', { default: false })
const props = defineProps<{ workspaceId: string }>()
const emit = defineEmits<{ created: [board: BoardSummary] }>()

const { t } = useI18n()
const api = useApi()
const toast = useToast()
const form = useFormState(['title', 'description', 'color'] as const)

const title = ref('')
const description = ref('')
const color = ref<PaletteColor>(defaultBoardColor)

watch(open, (value) => {
  if (!value) return
  title.value = ''
  description.value = ''
  color.value = defaultBoardColor
  form.reset()
})

async function submit() {
  const board = await form.submit(
    () =>
      api.post<BoardSummary>(`/api/workspaces/${props.workspaceId}/boards`, {
        title: title.value.trim(),
        description: description.value.trim() || null,
        color: color.value,
      }),
    () =>
      validateFields({
        title: [title.value, required, maxLength(limits.boardTitle)],
        description: [description.value, maxLength(limits.boardDescription)],
      }),
  )
  if (board) {
    emit('created', board)
    open.value = false
    toast.success(t('boards.created'))
  }
}
</script>

<template>
  <UiModal v-model:open="open" :title="t('boards.create')">
    <form novalidate class="flex flex-col gap-4" @submit.prevent="submit">
      <div class="flex h-20 items-end rounded-xl p-3 text-white transition-colors" :class="paletteFill(color)" aria-hidden="true">
        <span class="line-clamp-2 font-semibold">{{ title || t('boards.titlePlaceholder') }}</span>
      </div>

      <UiAlert v-if="form.formError.value">{{ form.formError.value }}</UiAlert>

      <UiInput
        v-model="title"
        :label="t('boards.title')"
        :placeholder="t('boards.titlePlaceholder')"
        :error="form.errors.value.title"
        :maxlength="limits.boardTitle"
        autocomplete="off"
        required
        autofocus
      />

      <fieldset>
        <legend class="mb-2 text-sm font-medium">{{ t('colors.label') }}</legend>
        <div class="flex flex-wrap gap-2" role="radiogroup">
          <label v-for="option in paletteColors" :key="option" class="relative cursor-pointer" :title="t(`colors.${option}`)">
            <input v-model="color" type="radio" name="board-color" :value="option" class="peer sr-only">
            <span
              class="flex size-11 items-center justify-center rounded-xl text-white ring-offset-2 ring-offset-surface transition peer-checked:ring-2 peer-checked:ring-ink peer-focus-visible:ring-2 peer-focus-visible:ring-accent"
              :class="paletteFill(option)"
            >
              <UiIcon v-if="color === option" name="check" :size="18" />
            </span>
            <span class="sr-only">{{ t(`colors.${option}`) }}</span>
          </label>
        </div>
        <p v-if="form.errors.value.color" class="mt-1 text-xs font-medium text-danger">{{ form.errors.value.color }}</p>
      </fieldset>

      <UiTextarea
        v-model="description"
        :label="`${t('boards.description')} (${t('common.optional')})`"
        :error="form.errors.value.description"
        :maxlength="limits.boardDescription"
        :rows="2"
      />

      <UiDialogActions>
        <UiButton variant="secondary" @click="open = false">{{ t('common.cancel') }}</UiButton>
        <UiButton type="submit" :loading="form.pending.value">{{ t('common.create') }}</UiButton>
      </UiDialogActions>
    </form>
  </UiModal>
</template>
