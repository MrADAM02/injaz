<script setup lang="ts">
import type { Label, PaletteColor } from '~/types/api'
import { paletteColors, paletteSwatch } from '~/utils/palette'

/** Create, rename, recolor and delete the board's labels. A name is optional; the color alone also works. */
const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const board = useBoardStore()
const toast = useToast()
const confirm = useConfirm()
const problem = useProblem()

const editing = ref<string | 'new' | null>(null)
const name = ref('')
const color = ref<PaletteColor>('green')
const pending = ref(false)
const error = ref<string | null>(null)
const canEdit = computed(() => board.allows('editContent'))

watch(open, (value) => {
  if (value) editing.value = null
})

function edit(label: Label | null) {
  editing.value = label?.id ?? 'new'
  name.value = label?.name ?? ''
  color.value = label?.color ?? paletteColors.find((c) => !board.labels.some((l) => l.color === c)) ?? 'green'
  error.value = null
}

async function save() {
  if (pending.value || !editing.value) return
  pending.value = true
  error.value = null
  const value = name.value.trim() || null
  try {
    if (editing.value === 'new') await board.createLabel(value, color.value)
    else await board.updateLabel(editing.value, { name: value, color: color.value })
    editing.value = null
  } catch (e) {
    error.value = problem.fields(e).name ?? problem.message(e)
  } finally {
    pending.value = false
  }
}

async function remove(label: Label) {
  const ok = await confirm({
    title: t('labels.deleteConfirm', { name: label.name || t(`colors.${label.color}`) }),
    body: t('labels.deleteBody'),
    confirmLabel: t('common.delete'),
    danger: true,
  })
  if (!ok) return
  try {
    await board.deleteLabel(label.id)
    if (editing.value === label.id) editing.value = null
  } catch (e) {
    toast.error(e)
  }
}
</script>

<template>
  <UiModal v-model:open="open" :title="t('labels.title')" :description="t('labels.hint')">
    <div class="flex flex-col gap-3">
      <p v-if="board.labels.length === 0 && editing !== 'new'" class="text-sm text-muted">{{ t('labels.none') }}</p>

      <ul class="flex flex-col gap-2">
        <li v-for="label in board.labels" :key="label.id">
          <div v-if="editing !== label.id" class="flex items-center gap-2">
            <span class="flex min-h-10 min-w-0 flex-1 items-center gap-2 rounded-lg px-3 text-sm font-medium text-white" :class="paletteSwatch(label.color)">
              <span class="truncate" dir="auto">{{ label.name || t(`colors.${label.color}`) }}</span>
            </span>
            <template v-if="canEdit">
              <UiButton variant="ghost" size="sm" icon="edit" :label="t('labels.edit')" @click="edit(label)" />
              <UiButton variant="danger-ghost" size="sm" icon="trash" :label="t('common.delete')" @click="remove(label)" />
            </template>
          </div>
          <form v-else class="flex flex-col gap-3 rounded-xl border border-line p-3" novalidate @submit.prevent="save">
            <BoardLabelForm v-model:name="name" v-model:color="color" :error="error" />
            <div class="flex justify-end gap-2">
              <UiButton variant="secondary" size="sm" @click="editing = null">{{ t('common.cancel') }}</UiButton>
              <UiButton type="submit" size="sm" :loading="pending">{{ t('common.save') }}</UiButton>
            </div>
          </form>
        </li>
      </ul>

      <form v-if="editing === 'new'" class="flex flex-col gap-3 rounded-xl border border-line p-3" novalidate @submit.prevent="save">
        <BoardLabelForm v-model:name="name" v-model:color="color" :error="error" />
        <div class="flex justify-end gap-2">
          <UiButton variant="secondary" size="sm" @click="editing = null">{{ t('common.cancel') }}</UiButton>
          <UiButton type="submit" size="sm" :loading="pending">{{ t('common.create') }}</UiButton>
        </div>
      </form>
      <UiButton v-else-if="canEdit" variant="secondary" icon="plus" class="self-start" @click="edit(null)">{{ t('labels.new') }}</UiButton>
    </div>
  </UiModal>
</template>

