<script setup lang="ts">
import type { Locale, User } from '~/types/api'
import { httpUrl, limits, maxLength, minLength, required, validateFields } from '~/utils/validate'

const { t, locales } = useI18n()
const api = useApi()
const auth = useAuthStore()
const toast = useToast()
const { switchTo } = useLocaleSwitch()
usePageTitle(() => t('profile.title'))

const user = computed(() => auth.user)

// ----- details -----
const displayName = ref(user.value?.displayName ?? '')
const avatarUrl = ref(user.value?.avatarUrl ?? '')
const details = useFormState(['displayName', 'avatarUrl'] as const)
const dirty = computed(
  () => !!user.value && (displayName.value.trim() !== user.value.displayName || avatarUrl.value.trim() !== (user.value.avatarUrl ?? '')),
)

async function saveDetails() {
  const updated = await details.submit(
    // An empty avatarUrl removes the picture (the API treats "" as clear).
    () => api.patch<User>('/api/me', { displayName: displayName.value.trim(), avatarUrl: avatarUrl.value.trim() }),
    () =>
      validateFields({
        displayName: [displayName.value, required, maxLength(limits.displayName)],
        avatarUrl: [avatarUrl.value.trim(), httpUrl, maxLength(limits.avatarUrl)],
      }),
  )
  if (updated) {
    auth.setUser(updated)
    toast.success(t('profile.saved'))
  }
}

// ----- language -----
const language = computed({
  get: () => (user.value?.locale ?? 'ar') as Locale,
  set: (value: Locale) => void switchTo(value),
})
const languageOptions = computed(() => locales.value.map((l) => ({ value: l.code as Locale, label: l.name ?? l.code })))

// ----- password -----
const currentPassword = ref('')
const newPassword = ref('')
const password = useFormState(['currentPassword', 'newPassword'] as const)

async function changePassword() {
  const ok = await password.submit(
    async () => {
      await auth.changePassword(currentPassword.value, newPassword.value)
      return true
    },
    () =>
      validateFields({
        currentPassword: [currentPassword.value, required],
        newPassword: [newPassword.value, required, minLength(limits.passwordMin), maxLength(limits.passwordMax)],
      }),
  )
  if (ok) {
    currentPassword.value = ''
    newPassword.value = ''
    toast.success(t('profile.password.changed'))
  }
}
</script>

<template>
  <div v-if="user" class="mx-auto flex max-w-2xl flex-col gap-6">
    <div class="flex items-center gap-4">
      <UiAvatar :name="displayName || user.displayName" :src="avatarUrl || null" size="lg" />
      <div class="min-w-0">
        <h1 class="truncate text-2xl font-bold">{{ user.displayName }}</h1>
        <p class="truncate text-sm text-muted" dir="ltr">{{ user.email }}</p>
      </div>
    </div>

    <UiCard :title="t('profile.details')">
      <form novalidate class="flex flex-col gap-4" @submit.prevent="saveDetails">
        <UiAlert v-if="details.formError.value">{{ details.formError.value }}</UiAlert>
        <UiInput
          v-model="displayName"
          autocomplete="name"
          :label="t('auth.displayName')"
          :error="details.errors.value.displayName"
          :maxlength="limits.displayName"
          required
        />
        <UiInput :model-value="user.email" type="email" :label="t('auth.email')" :hint="t('profile.emailHint')" readonly />
        <UiInput
          v-model="avatarUrl"
          type="url"
          inputmode="url"
          autocomplete="off"
          :label="`${t('profile.avatarUrl')} (${t('common.optional')})`"
          :hint="t('profile.avatarHint')"
          :error="details.errors.value.avatarUrl"
          :maxlength="limits.avatarUrl"
        />
        <div class="flex justify-end">
          <UiButton type="submit" :disabled="!dirty" :loading="details.pending.value">{{ t('common.save') }}</UiButton>
        </div>
      </form>
    </UiCard>

    <UiCard :title="t('profile.language')">
      <UiSelect v-model="language" :label="t('profile.language')" hide-label :options="languageOptions" />
    </UiCard>

    <UiCard :title="t('profile.password.title')">
      <form novalidate class="flex flex-col gap-4" @submit.prevent="changePassword">
        <UiAlert v-if="password.formError.value">{{ password.formError.value }}</UiAlert>
        <!-- Lets password managers associate the change with the right account. -->
        <input type="text" :value="user.email" autocomplete="username" class="hidden" readonly aria-hidden="true" tabindex="-1">
        <UiInput
          v-model="currentPassword"
          type="password"
          autocomplete="current-password"
          :label="t('profile.password.current')"
          :error="password.errors.value.currentPassword"
          required
        />
        <UiInput
          v-model="newPassword"
          type="password"
          autocomplete="new-password"
          :label="t('profile.password.new')"
          :hint="t('auth.passwordHint')"
          :error="password.errors.value.newPassword"
          :maxlength="limits.passwordMax"
          required
        />
        <div class="flex justify-end">
          <UiButton type="submit" :loading="password.pending.value">{{ t('profile.password.submit') }}</UiButton>
        </div>
      </form>
    </UiCard>

    <UiButton variant="secondary" icon="logout" class="md:hidden" block @click="auth.logout()">{{ t('nav.signOut') }}</UiButton>
  </div>
</template>
