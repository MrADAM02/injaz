<script setup lang="ts">
import type { Locale } from '~/types/api'

/** One tap toggles between the two languages; the button names the language it switches to, in that language. */
const props = withDefaults(defineProps<{ variant?: 'ghost' | 'secondary'; block?: boolean }>(), { variant: 'ghost' })
const { other, switchTo } = useLocaleSwitch()
const { t } = useI18n()
const busy = ref(false)

async function toggle() {
  if (!other.value || busy.value) return
  busy.value = true
  try {
    await switchTo(other.value.code as Locale)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <UiButton
    :variant="props.variant"
    size="sm"
    icon="globe"
    :block="props.block"
    :loading="busy"
    :class="props.block ? 'justify-start' : ''"
    :title="t('nav.language')"
    :lang="other?.language"
    @click="toggle"
  >
    {{ other?.name }}
  </UiButton>
</template>
