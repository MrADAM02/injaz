import { pluralIndex } from '../app/utils/plural'

export default defineI18nConfig(() => ({
  fallbackLocale: 'en',
  // Arabic has six plural forms (zero/one/two/few/many/other); messages list them in that order.
  pluralRules: {
    ar: (choice: number, choicesLength: number) => pluralIndex('ar', choice, choicesLength),
  },
}))
