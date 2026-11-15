<script setup lang="ts">
import { safeRedirect } from '~/utils/redirect'
import { email as emailRule, required, validateFields } from '~/utils/validate'

definePageMeta({ layout: 'auth', guestOnly: true })

const { t } = useI18n()
const auth = useAuthStore()
const route = useRoute()
usePageTitle(() => t('auth.login.title'))

const form = useFormState(['email', 'password'] as const)
const email = ref(typeof route.query.email === 'string' ? route.query.email : '')
const password = ref('')

async function submit() {
  const ok = await form.submit(
    async () => {
      await auth.login(email.value.trim(), password.value)
      return true
    },
    () => validateFields({ email: [email.value, required, emailRule], password: [password.value, required] }),
  )
  if (ok) await navigateTo(safeRedirect(route.query.redirect), { replace: true })
}
</script>

<template>
  <div class="rounded-2xl border border-line bg-surface p-6 shadow-sm sm:p-8">
    <h1 class="text-2xl font-bold">{{ t('auth.login.title') }}</h1>
    <p class="mt-1 text-sm text-muted">{{ t('auth.login.subtitle') }}</p>

    <form novalidate class="mt-6 flex flex-col gap-4" @submit.prevent="submit">
      <UiAlert v-if="form.formError.value">{{ form.formError.value }}</UiAlert>
      <UiInput
        v-model="email"
        type="email"
        inputmode="email"
        autocomplete="email"
        :label="t('auth.email')"
        :error="form.errors.value.email"
        required
        autofocus
      />
      <UiInput
        v-model="password"
        type="password"
        autocomplete="current-password"
        :label="t('auth.password')"
        :error="form.errors.value.password"
        required
      />
      <UiButton type="submit" block :loading="form.pending.value" class="mt-2">{{ t('auth.login.submit') }}</UiButton>
    </form>

    <p class="mt-6 text-center text-sm text-muted">
      {{ t('auth.login.noAccount') }}
      <NuxtLink :to="{ path: '/register', query: route.query }" class="font-medium text-accent hover:underline">{{ t('auth.login.toRegister') }}</NuxtLink>
    </p>
  </div>
</template>
