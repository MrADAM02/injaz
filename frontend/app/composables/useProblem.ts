import { fieldMessages, problemMessage } from '~/utils/problem-messages'

/** Turns API errors into translated sentences (whole-request message, or per-field messages). */
export function useProblem() {
  const { t, te } = useI18n()
  const i18n = { t: (key: string, params?: Record<string, unknown>) => t(key, params ?? {}), te: (key: string) => te(key) }

  return {
    message: (error: unknown) => problemMessage(error, i18n),
    fields: (error: unknown) => fieldMessages(error, i18n),
  }
}
