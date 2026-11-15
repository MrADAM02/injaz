<script setup lang="ts">
import type { Comment, Page } from '~/types/api'
import { limits } from '~/utils/validate'

/** Comments, newest first (Markdown). Authors edit their own; authors and admins delete. */
const props = defineProps<{ cardId: string; canComment: boolean; canModerate: boolean; revision?: number }>()
const emit = defineEmits<{ countChange: [delta: number] }>()

const { t } = useI18n()
const api = useApi()
const auth = useAuthStore()
const toast = useToast()
const confirm = useConfirm()
const problem = useProblem()
const format = useFormat()
const id = useId()

const comments = usePaged<Comment>((cursor) => api.get<Page<Comment>>(`/api/cards/${props.cardId}/comments`, { cursor, limit: 20 }))
watch([() => props.cardId, () => props.revision], () => void comments.load(), { immediate: true })

const draft = ref('')
const posting = ref(false)
const postError = ref<string | null>(null)

async function post() {
  const body = draft.value.trim()
  if (!body || posting.value) return
  posting.value = true
  postError.value = null
  try {
    const comment = await api.post<Comment>(`/api/cards/${props.cardId}/comments`, { body })
    comments.items.value = [comment, ...comments.items.value]
    draft.value = ''
    emit('countChange', 1)
  } catch (e) {
    postError.value = problem.fields(e).body ?? problem.message(e)
  } finally {
    posting.value = false
  }
}

const editingId = ref<string | null>(null)
const editDraft = ref('')
const saving = ref(false)

function edit(comment: Comment) {
  editingId.value = comment.id
  editDraft.value = comment.body
}

async function save(comment: Comment) {
  const body = editDraft.value.trim()
  if (!body || saving.value) return
  saving.value = true
  try {
    const updated = await api.patch<Comment>(`/api/comments/${comment.id}`, { body })
    comments.items.value = comments.items.value.map((c) => (c.id === updated.id ? updated : c))
    editingId.value = null
  } catch (e) {
    toast.error(e)
  } finally {
    saving.value = false
  }
}

async function remove(comment: Comment) {
  const ok = await confirm({ title: t('comments.deleteConfirm'), confirmLabel: t('common.delete'), danger: true })
  if (!ok) return
  try {
    await api.delete(`/api/comments/${comment.id}`)
    comments.items.value = comments.items.value.filter((c) => c.id !== comment.id)
    emit('countChange', -1)
  } catch (e) {
    toast.error(e)
  }
}

function onKeydown(event: KeyboardEvent, submit: () => void) {
  if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
    event.preventDefault()
    submit()
  }
}
</script>

<template>
  <div class="flex flex-col gap-4">
    <form v-if="props.canComment" class="flex flex-col gap-2" novalidate @submit.prevent="post">
      <label :for="`${id}-new`" class="sr-only">{{ t('comments.write') }}</label>
      <textarea
        :id="`${id}-new`"
        v-model="draft"
        rows="2"
        :maxlength="limits.commentBody"
        :placeholder="t('comments.placeholder')"
        dir="auto"
        class="block w-full resize-y rounded-xl border border-line bg-surface px-3.5 py-2.5 focus:border-accent focus:outline-none focus:ring-3 focus:ring-accent/20"
        @keydown="onKeydown($event, post)"
      />
      <p v-if="postError" class="text-xs font-medium text-danger">{{ postError }}</p>
      <div class="flex items-center gap-2">
        <UiButton type="submit" size="sm" :loading="posting" :disabled="!draft.trim()">{{ t('comments.post') }}</UiButton>
        <span class="text-xs text-muted">{{ t('card.markdownHint') }}</span>
      </div>
    </form>

    <div v-if="comments.loading.value && !comments.loaded.value" class="flex justify-center py-4 text-muted"><UiSpinner :label="t('common.loading')" /></div>
    <UiAlert v-else-if="comments.error.value && !comments.loaded.value">{{ problem.message(comments.error.value) }}</UiAlert>
    <p v-else-if="comments.items.value.length === 0" class="text-sm text-muted">{{ t('comments.none') }}</p>

    <ol v-else class="flex flex-col gap-4">
      <li v-for="comment in comments.items.value" :key="comment.id" class="flex gap-3">
        <UiAvatar :name="comment.author.displayName" :src="comment.author.avatarUrl" size="sm" />
        <div class="min-w-0 flex-1">
          <p class="flex flex-wrap items-baseline gap-x-2 text-sm">
            <span class="font-semibold" dir="auto">{{ comment.author.displayName }}</span>
            <time class="text-xs text-muted" :datetime="comment.createdAt" :title="format.dateTime(comment.createdAt)">{{ format.relative(comment.createdAt) }}</time>
            <span v-if="comment.editedAt" class="text-xs text-muted">({{ t('comments.edited') }})</span>
          </p>

          <form v-if="editingId === comment.id" class="mt-1 flex flex-col gap-2" @submit.prevent="save(comment)">
            <textarea
              v-model="editDraft"
              rows="3"
              :maxlength="limits.commentBody"
              :aria-label="t('comments.edit')"
              dir="auto"
              class="block w-full resize-y rounded-xl border border-line bg-surface px-3.5 py-2.5 focus:border-accent focus:outline-none focus:ring-3 focus:ring-accent/20"
              @keydown="onKeydown($event, () => save(comment))"
              @keydown.esc.stop.prevent="editingId = null"
            />
            <div class="flex gap-2">
              <UiButton type="submit" size="sm" :loading="saving">{{ t('common.save') }}</UiButton>
              <UiButton variant="ghost" size="sm" @click="editingId = null">{{ t('common.cancel') }}</UiButton>
            </div>
          </form>
          <template v-else>
            <div class="mt-1 rounded-xl bg-subtle px-3 py-2">
              <UiMarkdown :source="comment.body" />
            </div>
            <div v-if="comment.author.userId === auth.user?.id || props.canModerate" class="mt-1 flex gap-3 text-xs">
              <button v-if="comment.author.userId === auth.user?.id" type="button" class="min-h-8 font-medium text-muted hover:text-ink" @click="edit(comment)">{{ t('common.edit') }}</button>
              <button type="button" class="min-h-8 font-medium text-muted hover:text-danger" @click="remove(comment)">{{ t('common.delete') }}</button>
            </div>
          </template>
        </div>
      </li>
    </ol>

    <UiButton v-if="comments.hasMore.value" variant="secondary" size="sm" class="self-center" :loading="comments.loadingMore.value" @click="comments.more()">{{ t('common.loadMore') }}</UiButton>
  </div>
</template>
