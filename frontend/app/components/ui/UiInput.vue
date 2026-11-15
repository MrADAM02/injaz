<script setup lang="ts">
/**
 * Labelled text input with hint and error text wired up for screen readers.
 * `type="password"` gets a show/hide toggle. Email/URL/password fields are always LTR, even in Arabic.
 */
const model = defineModel<string>({ default: '' })

const props = withDefaults(
  defineProps<{
    label: string
    type?: 'text' | 'email' | 'password' | 'url' | 'search'
    error?: string | null
    hint?: string
    placeholder?: string
    autocomplete?: string
    inputmode?: 'text' | 'email' | 'url' | 'search'
    required?: boolean
    disabled?: boolean
    readonly?: boolean
    autofocus?: boolean
    maxlength?: number
    /** Visually hide the label (it stays available to assistive tech). */
    hideLabel?: boolean
  }>(),
  { type: 'text' },
)

const id = useId()
const revealed = ref(false)
const input = useTemplateRef<HTMLInputElement>('input')
const { t } = useI18n()

const inputType = computed(() => (props.type === 'password' && revealed.value ? 'text' : props.type))
const ltr = computed(() => props.type === 'email' || props.type === 'url' || props.type === 'password')
const describedBy = computed(() => (props.error ? `${id}-error` : props.hint ? `${id}-hint` : undefined))

onMounted(() => {
  if (props.autofocus && window.matchMedia('(pointer: fine)').matches) input.value?.focus()
})

defineExpose({ focus: () => input.value?.focus() })
</script>

<template>
  <div class="flex flex-col gap-1.5">
    <label :for="id" class="text-sm font-medium" :class="{ 'sr-only': props.hideLabel }">
      {{ props.label }}
      <span v-if="props.required" class="text-danger" aria-hidden="true">*</span>
    </label>
    <!-- Email/URL/password values are LTR even in Arabic; the whole field (incl. the eye toggle) follows. -->
    <div class="relative" :dir="ltr ? 'ltr' : undefined">
      <input
        :id="id"
        ref="input"
        v-model="model"
        :type="inputType"
        :placeholder="props.placeholder"
        :autocomplete="props.autocomplete"
        :inputmode="props.inputmode"
        :disabled="props.disabled"
        :readonly="props.readonly"
        :maxlength="props.maxlength"
        :required="props.required"
        :aria-invalid="props.error ? 'true' : undefined"
        :aria-describedby="describedBy"
        class="block min-h-11 w-full rounded-xl border bg-surface px-3.5 py-2 text-ink placeholder:text-muted/70 transition-colors read-only:bg-subtle read-only:text-muted disabled:opacity-60 focus:border-accent focus:outline-none focus:ring-3 focus:ring-accent/20"
        :class="[props.error ? 'border-danger' : 'border-line', props.type === 'password' ? 'pe-12' : '']"
      >
      <button
        v-if="props.type === 'password'"
        type="button"
        class="absolute inset-y-0 inset-e-0 flex w-11 items-center justify-center rounded-e-xl text-muted hover:text-ink"
        :aria-label="revealed ? t('auth.hidePassword') : t('auth.showPassword')"
        :aria-pressed="revealed"
        @click="revealed = !revealed"
      >
        <UiIcon :name="revealed ? 'eyeOff' : 'eye'" :size="18" />
      </button>
    </div>
    <p v-if="props.hint && !props.error" :id="`${id}-hint`" class="text-xs text-muted">{{ props.hint }}</p>
    <p v-if="props.error" :id="`${id}-error`" class="text-xs font-medium text-danger">{{ props.error }}</p>
  </div>
</template>
