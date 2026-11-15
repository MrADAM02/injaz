const order: Intl.LDMLPluralRule[] = ['zero', 'one', 'two', 'few', 'many', 'other']
const rules = new Map<string, Intl.PluralRules>()

/**
 * Index into a `a | b | c …` vue-i18n message using CLDR plural categories, for languages with more forms
 * than English. Messages list the forms in CLDR order (zero, one, two, few, many, other); a message with fewer
 * forms falls back to its last one.
 */
export function pluralIndex(language: string, choice: number, choicesLength: number): number {
  let rule = rules.get(language)
  if (!rule) {
    rule = new Intl.PluralRules(language)
    rules.set(language, rule)
  }

  // Intl only reports "zero" for some languages (Arabic does); English-like messages use index 0 for zero anyway.
  const index = order.indexOf(rule.select(Math.abs(choice)))
  return Math.min(index, choicesLength - 1)
}
