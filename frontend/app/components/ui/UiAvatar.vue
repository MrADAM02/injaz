<script setup lang="ts">
/** Profile picture, or initials on a color derived from the name when there is none (or it fails to load). */
const props = withDefaults(defineProps<{ name: string; src?: string | null; size?: 'sm' | 'md' | 'lg' }>(), { size: 'md' })

const failed = ref(false)
watch(
  () => props.src,
  () => {
    failed.value = false
  },
)

const initials = computed(() => {
  const words = props.name.trim().split(/\s+/).filter(Boolean)
  const letters = words.length > 1 ? [words[0], words[words.length - 1]] : words
  return letters.map((w) => Array.from(w ?? '')[0] ?? '').join('').toUpperCase() || '?'
})

const tones = ['bg-teal-600', 'bg-sky-600', 'bg-indigo-500', 'bg-fuchsia-600', 'bg-rose-600', 'bg-amber-600', 'bg-emerald-600', 'bg-violet-600']
const tone = computed(() => {
  let hash = 0
  for (const char of props.name) hash = (hash * 31 + char.charCodeAt(0)) | 0
  return tones[Math.abs(hash) % tones.length]
})

const sizes = { sm: 'size-7 text-[11px]', md: 'size-9 text-xs', lg: 'size-16 text-xl' }
</script>

<template>
  <img
    v-if="props.src && !failed"
    :src="props.src"
    :alt="props.name"
    class="shrink-0 rounded-full object-cover"
    :class="sizes[props.size]"
    referrerpolicy="no-referrer"
    @error="failed = true"
  >
  <span
    v-else
    class="inline-flex shrink-0 select-none items-center justify-center rounded-full font-semibold text-white"
    :class="[sizes[props.size], tone]"
    role="img"
    :aria-label="props.name"
  >{{ initials }}</span>
</template>
