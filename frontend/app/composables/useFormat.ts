/** Locale-aware formatting via Intl. Arabic uses Latin digits so numbers match what people type. */
export function useFormat() {
  const { locale } = useI18n()
  const tag = computed(() => (locale.value === 'ar' ? 'ar-u-nu-latn' : 'en'))

  const dateFormat = computed(() => new Intl.DateTimeFormat(tag.value, { dateStyle: 'medium' }))
  const dateTimeFormat = computed(() => new Intl.DateTimeFormat(tag.value, { dateStyle: 'medium', timeStyle: 'short' }))
  const shortFormat = computed(() => new Intl.DateTimeFormat(tag.value, { month: 'short', day: 'numeric' }))
  const shortWithYear = computed(() => new Intl.DateTimeFormat(tag.value, { month: 'short', day: 'numeric', year: 'numeric' }))
  const relativeFormat = computed(() => new Intl.RelativeTimeFormat(tag.value, { numeric: 'auto' }))
  const collator = computed(() => new Intl.Collator(tag.value, { sensitivity: 'base' }))

  return {
    tag,
    collator,
    date: (iso: string | null | undefined) => (iso ? dateFormat.value.format(new Date(iso)) : ''),
    dateTime: (iso: string | null | undefined) => (iso ? dateTimeFormat.value.format(new Date(iso)) : ''),
    /** "Nov 15" this year, "Nov 15, 2027" otherwise: for due-date badges. */
    dateShort: (iso: string | null | undefined) => {
      if (!iso) return ''
      const date = new Date(iso)
      return (date.getFullYear() === new Date().getFullYear() ? shortFormat : shortWithYear).value.format(date)
    },
    /** "5 minutes ago", "yesterday"; older than a week falls back to the date. */
    relative: (iso: string | null | undefined) => {
      if (!iso) return ''
      const seconds = Math.round((Date.parse(iso) - Date.now()) / 1000)
      const abs = Math.abs(seconds)
      if (abs < 45) return relativeFormat.value.format(0, 'second')
      if (abs < 3600) return relativeFormat.value.format(Math.round(seconds / 60), 'minute')
      if (abs < 86_400) return relativeFormat.value.format(Math.round(seconds / 3600), 'hour')
      if (abs < 7 * 86_400) return relativeFormat.value.format(Math.round(seconds / 86_400), 'day')
      return dateFormat.value.format(new Date(iso))
    },
  }
}
