<script setup lang="ts">
import { renderMarkdown } from '~/utils/markdown'

/** Sanitized Markdown (see utils/markdown.ts). Each paragraph picks its own direction from its text. */
const props = defineProps<{ source: string | null | undefined }>()
const html = computed(() => renderMarkdown(props.source))
</script>

<template>
  <!-- eslint-disable-next-line vue/no-v-html -- sanitized by DOMPurify in renderMarkdown -->
  <div class="markdown" v-html="html" />
</template>
