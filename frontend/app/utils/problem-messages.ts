import { ApiError } from './api-client'

/** The subset of vue-i18n the error helpers need (kept minimal so they stay unit-testable). */
export interface Translator {
  t: (key: string, params?: Record<string, unknown>) => string
  te: (key: string) => boolean
}

/** A sentence for the whole failure: the translated ProblemDetails `code`, or a generic fallback. */
export function problemMessage(error: unknown, i18n: Translator): string {
  if (error instanceof ApiError) {
    const key = `errors.${error.code}`
    if (i18n.te(key)) return i18n.t(key)
    if (error.status >= 500) return i18n.t('errors.server.error')
  }
  return i18n.t('errors.generic')
}

/** First message per field from a 400 `validation.failed`, keyed by the API's camelCase field name. */
export function fieldMessages(error: unknown, i18n: Translator): Record<string, string> {
  if (!(error instanceof ApiError)) return {}
  const messages: Record<string, string> = {}
  for (const [field, errors] of Object.entries(error.fieldErrors)) {
    const first = errors[0]
    if (!first) continue
    const key = `validation.${first.code}`
    messages[field] = i18n.te(key) ? i18n.t(key, first.params ?? {}) : i18n.t('validation.invalidValue')
  }
  return messages
}
