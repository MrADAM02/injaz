<script setup lang="ts">
import { limits, maxLength, required, validateFields } from '~/utils/validate'

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const workspaces = useWorkspacesStore()
const toast = useToast()
const form = useFormState(['name'] as const)
const name = ref('')

watch(open, (value) => {
  if (value) {
    name.value = ''
    form.reset()
  }
})

async function submit() {
  const created = await form.submit(
    () => workspaces.create(name.value.trim()),
    () => validateFields({ name: [name.value, required, maxLength(limits.workspaceName)] }),
  )
  if (created) {
    open.value = false
    toast.success(t('workspace.created'))
    await navigateTo(`/w/${created.id}`)
  }
}
</script>

<template>
  <UiModal v-model:open="open" :title="t('workspace.create')" :description="t('home.empty.body')">
    <form novalidate class="flex flex-col gap-4" @submit.prevent="submit">
      <UiAlert v-if="form.formError.value">{{ form.formError.value }}</UiAlert>
      <UiInput
        v-model="name"
        :label="t('workspace.name')"
        :placeholder="t('workspace.namePlaceholder')"
        :error="form.errors.value.name"
        :maxlength="limits.workspaceName"
        autocomplete="off"
        required
        autofocus
      />
      <UiDialogActions>
        <UiButton variant="secondary" @click="open = false">{{ t('common.cancel') }}</UiButton>
        <UiButton type="submit" :loading="form.pending.value">{{ t('common.create') }}</UiButton>
      </UiDialogActions>
    </form>
  </UiModal>
</template>
