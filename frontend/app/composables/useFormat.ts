/** Locale-aware formatting via Intl. Arabic uses Latin digits so numbers match what people type. */
export function useFormat() {
  const { locale } = useI18n()
  const tag = computed(() => (locale.value === 'ar' ? 'ar-u-nu-latn' : 'en'))

  const dateFormat = computed(() => new Intl.DateTimeFormat(tag.value, { dateStyle: 'medium' }))
  const collator = computed(() => new Intl.Collator(tag.value, { sensitivity: 'base' }))

  return {
    tag,
    collator,
    date: (iso: string | null | undefined) => (iso ? dateFormat.value.format(new Date(iso)) : ''),
  }
}
