<script setup lang="ts">
/**
 * "Move to…": pick a list and a position. The dependable way to move cards on a small screen and the way to
 * do it from the keyboard; it uses the same move endpoint as drag and drop.
 */
const open = defineModel<boolean>('open', { default: false })
const props = defineProps<{ cardId: string; listId: string }>()
const emit = defineEmits<{ moved: [listId: string] }>()

const { t } = useI18n()
const board = useBoardStore()
const toast = useToast()

const target = ref('')
const position = ref<'top' | 'bottom'>('bottom')
const pending = ref(false)

watch(open, (isOpen) => {
  if (!isOpen) return
  target.value = props.listId
  position.value = 'bottom'
})

const listOptions = computed(() =>
  board.lists.map((list) => ({ value: list.id, label: list.id === props.listId ? `${list.title} (${t('card.currentList')})` : list.title })),
)
const positionOptions = computed(() => [
  { value: 'top' as const, label: t('card.positionTop') },
  { value: 'bottom' as const, label: t('card.positionBottom') },
])

async function submit() {
  const list = board.lists.find((l) => l.id === target.value)
  if (!list || pending.value) return
  const others = list.cards.filter((c) => c.id !== props.cardId).length
  pending.value = true
  try {
    await board.moveCard(props.cardId, list.id, position.value === 'top' ? 0 : others)
    emit('moved', list.id)
    open.value = false
    toast.success(t('card.movedTo', { list: list.title }))
  } catch (e) {
    toast.error(e)
  } finally {
    pending.value = false
  }
}
</script>

<template>
  <UiModal v-model:open="open" :title="t('card.moveTitle')" size="sm">
    <form class="flex flex-col gap-4" @submit.prevent="submit">
      <UiSelect v-model="target" :label="t('card.toList')" :options="listOptions" />
      <UiSelect v-model="position" :label="t('card.position')" :options="positionOptions" />
      <UiDialogActions>
        <UiButton variant="secondary" @click="open = false">{{ t('common.cancel') }}</UiButton>
        <UiButton type="submit" :loading="pending">{{ t('card.move') }}</UiButton>
      </UiDialogActions>
    </form>
  </UiModal>
</template>
