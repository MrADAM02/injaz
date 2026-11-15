<script setup lang="ts">
/** Renders the pending useConfirm() request. Mounted once in app.vue. */
const store = useConfirmStore()
const { t } = useI18n()
const typed = ref('')

const open = computed({
  get: () => store.current !== null,
  set: (value) => {
    if (!value) store.settle(false)
  },
})

watch(
  () => store.current,
  () => {
    typed.value = ''
  },
)

const blocked = computed(() => {
  const required = store.current?.requireText
  return required !== undefined && typed.value.trim() !== required.trim()
})

function confirm() {
  if (!blocked.value) store.settle(true)
}
</script>

<template>
  <UiModal v-model:open="open" :title="store.current?.title ?? ''" size="sm">
    <form v-if="store.current" @submit.prevent="confirm">
      <p v-if="store.current.body" class="text-sm text-muted">{{ store.current.body }}</p>
      <UiInput
        v-if="store.current.requireText !== undefined"
        v-model="typed"
        class="mt-4"
        :label="store.current.requireTextLabel ?? ''"
        :placeholder="store.current.requireText"
        autocomplete="off"
        autofocus
      />
      <UiDialogActions>
        <UiButton variant="secondary" @click="store.settle(false)">{{ t('common.cancel') }}</UiButton>
        <UiButton type="submit" :variant="store.current.danger ? 'danger' : 'primary'" :disabled="blocked">
          {{ store.current.confirmLabel }}
        </UiButton>
      </UiDialogActions>
    </form>
  </UiModal>
</template>
