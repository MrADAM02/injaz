import { ApiError } from '~/utils/api-client'
import type { FieldError } from '~/types/api'

/**
 * Pending flag + error state for one form. Field errors for the listed `fields` show under their inputs;
 * anything else (a field the form doesn't render, or a non-validation error) becomes the form-level message.
 */
export function useFormState<F extends string>(fields: readonly F[]) {
  const problem = useProblem()

  const pending = ref(false)
  const errors = ref<Partial<Record<F, string>>>({})
  const formError = ref<string | null>(null)

  function reset() {
    errors.value = {}
    formError.value = null
  }

  function show(error: unknown) {
    const byField = problem.fields(error)
    const visible: Partial<Record<F, string>> = {}
    let hidden = false
    for (const [field, message] of Object.entries(byField)) {
      if ((fields as readonly string[]).includes(field)) visible[field as F] = message
      else hidden = true
    }
    errors.value = visible
    formError.value = Object.keys(byField).length === 0 || hidden ? problem.message(error) : null
  }

  /**
   * Runs `action` unless already pending. `check` returns client-side failures (same shape as the API's
   * `errors`); when it reports any, the action is skipped. Returns the action's result, or undefined on failure.
   */
  async function submit<T>(action: () => Promise<T>, check?: () => Record<string, FieldError[]>): Promise<T | undefined> {
    if (pending.value) return undefined
    reset()

    const local = check?.() ?? {}
    if (Object.keys(local).length > 0) {
      show(new ApiError(400, { code: 'validation.failed', errors: local }))
      return undefined
    }

    pending.value = true
    try {
      return await action()
    } catch (error) {
      show(error)
      return undefined
    } finally {
      pending.value = false
    }
  }

  return { pending, errors, formError, submit, reset, show }
}
