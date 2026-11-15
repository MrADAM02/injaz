<script setup lang="ts">
import type { PaletteColor } from '~/types/api'
import { paletteColors, paletteSwatch } from '~/utils/palette'
import { limits } from '~/utils/validate'

/** Name + color fields of a label (used by the labels dialog for both create and edit). */
const name = defineModel<string>('name', { required: true })
const color = defineModel<PaletteColor>('color', { required: true })
const props = defineProps<{ error?: string | null }>()
const { t } = useI18n()
const group = useId()
</script>

<template>
  <div class="flex flex-col gap-3">
    <UiInput v-model="name" :label="`${t('labels.name')} (${t('common.optional')})`" :maxlength="limits.labelName" :error="props.error" autocomplete="off" autofocus />
    <fieldset>
      <legend class="mb-2 text-sm font-medium">{{ t('labels.color') }}</legend>
      <div class="flex flex-wrap gap-2">
        <label v-for="option in paletteColors" :key="option" class="cursor-pointer" :title="t(`colors.${option}`)">
          <input v-model="color" type="radio" :name="group" :value="option" class="peer sr-only">
          <span
            class="flex size-10 items-center justify-center rounded-lg text-white ring-offset-2 ring-offset-surface transition peer-checked:ring-2 peer-checked:ring-ink peer-focus-visible:ring-2 peer-focus-visible:ring-accent"
            :class="paletteSwatch(option)"
          >
            <UiIcon v-if="color === option" name="check" :size="18" />
          </span>
          <span class="sr-only">{{ t(`colors.${option}`) }}</span>
        </label>
      </div>
    </fieldset>
  </div>
</template>
