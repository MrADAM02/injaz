<script setup lang="ts">
import type { Locale } from '~/types/api'
import { safeRedirect } from '~/utils/redirect'
import { email as emailRule, limits, maxLength, minLength, required, validateFields } from '~/utils/validate'

definePageMeta({ layout: 'auth', guestOnly: true })

const { t, locale } = useI18n()
const auth = useAuthStore()
const route = useRoute()
usePageTitle(() => t('auth.register.title'))

const form = useFormState(['displayName', 'email', 'password'] as const)
const displayName = ref('')
const email = ref(typeof route.query.email === 'string' ? route.query.email : '')
const password = ref('')

async function submit() {
  const ok = await form.submit(
    async () => {
      await auth.register({
        displayName: displayName.value.trim(),
        email: email.value.trim(),
        password: password.value,
        locale: locale.value as Locale,
      })
      return true
    },
    () =>
      validateFields({
        displayName: [displayName.value, required, maxLength(limits.displayName)],
        email: [email.value, required, emailRule, maxLength(limits.email)],
        password: [password.value, required, minLength(limits.passwordMin), maxLength(limits.passwordMax)],
      }),
  )
  if (ok) await navigateTo(safeRedirect(route.query.redirect), { replace: true })
}
</script>

<template>
  <div class="rounded-2xl border border-line bg-surface p-6 shadow-sm sm:p-8">
    <h1 class="text-2xl font-bold">{{ t('auth.register.title') }}</h1>
    <p class="mt-1 text-sm text-muted">{{ t('auth.register.subtitle') }}</p>

    <form novalidate class="mt-6 flex flex-col gap-4" @submit.prevent="submit">
      <UiAlert v-if="form.formError.value">{{ form.formError.value }}</UiAlert>
      <UiInput
        v-model="displayName"
        autocomplete="name"
        :label="t('auth.displayName')"
        :error="form.errors.value.displayName"
        :maxlength="limits.displayName"
        required
        autofocus
      />
      <UiInput
        v-model="email"
        type="email"
        inputmode="email"
        autocomplete="email"
        :label="t('auth.email')"
        :error="form.errors.value.email"
        required
      />
      <UiInput
        v-model="password"
        type="password"
        autocomplete="new-password"
        :label="t('auth.password')"
        :hint="t('auth.passwordHint')"
        :error="form.errors.value.password"
        :maxlength="limits.passwordMax"
        required
      />
      <UiButton type="submit" block :loading="form.pending.value" class="mt-2">{{ t('auth.register.submit') }}</UiButton>
    </form>

    <p class="mt-6 text-center text-sm text-muted">
      {{ t('auth.register.haveAccount') }}
      <NuxtLink :to="{ path: '/login', query: route.query }" class="font-medium text-accent hover:underline">{{ t('auth.register.toLogin') }}</NuxtLink>
    </p>
  </div>
</template>
