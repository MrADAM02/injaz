import type { Locale, User } from '~/types/api'

/** Switches the UI language; when signed in, also saves it to the profile. */
export function useLocaleSwitch() {
  const { locale, locales, setLocale } = useI18n()
  const auth = useAuthStore()
  const api = useApi()
  const toast = useToast()

  const other = computed(() => locales.value.find((l) => l.code !== locale.value))

  async function switchTo(next: Locale) {
    if (next === locale.value) return
    await setLocale(next)
    if (auth.user && auth.user.locale !== next) {
      try {
        auth.setUser(await api.patch<User>('/api/me', { locale: next }))
      } catch (error) {
        toast.error(error)
      }
    }
  }

  return { locale, other, switchTo }
}
