import type { Locale } from '~/types/api'

const storageKey = 'injaz.locale'
const isLocale = (value: unknown): value is Locale => value === 'ar' || value === 'en'

/**
 * Language preference. Signed out: whatever the visitor last picked on this device (Arabic by default).
 * Signed in: the profile's locale wins, so the choice follows the user across devices.
 */
export default defineNuxtPlugin({
  name: 'injaz:locale',
  dependsOn: ['injaz:api'],
  async setup(nuxtApp) {
    const i18n = nuxtApp.$i18n
    const auth = useAuthStore()

    let stored: string | null = null
    try {
      stored = localStorage.getItem(storageKey)
    } catch {
      // Storage unavailable: stay on the default.
    }
    if (isLocale(stored) && stored !== i18n.locale.value) await i18n.setLocale(stored)

    watch(
      () => auth.user?.locale,
      (locale) => {
        if (isLocale(locale) && locale !== i18n.locale.value) void i18n.setLocale(locale)
      },
    )

    nuxtApp.hook('i18n:localeSwitched', ({ newLocale }) => {
      try {
        localStorage.setItem(storageKey, newLocale)
      } catch {
        // Storage unavailable: the choice lasts for this page load only.
      }
    })
  },
})
