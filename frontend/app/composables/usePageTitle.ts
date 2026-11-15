/** Sets the document title and the compact mobile top bar's title. */
export function usePageTitle(title: MaybeRefOrGetter<string | undefined>) {
  const { t } = useI18n()
  const shared = useState<string>('injaz:page-title', () => '')

  watchEffect(() => {
    shared.value = toValue(title) ?? ''
  })

  useHead({
    title: () => {
      const value = toValue(title)
      return value ? `${value} · ${t('app.name')}` : t('app.name')
    },
  })
}

export function useCurrentPageTitle() {
  return useState<string>('injaz:page-title', () => '')
}
