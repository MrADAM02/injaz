import type { FieldError } from '~/types/api'

/**
 * Client-side checks that mirror the API's rules and codes, so a form can flag problems before a round trip.
 * The API still validates everything; these only produce the same `validation.*` codes earlier.
 */
export type Rule = (value: string) => FieldError | null

export const required: Rule = (value) => (value.trim().length === 0 ? { code: 'required' } : null)

export const minLength = (min: number): Rule => (value) =>
  value.length > 0 && value.length < min ? { code: 'minLength', params: { min } } : null

export const maxLength = (max: number): Rule => (value) => (value.length > max ? { code: 'maxLength', params: { max } } : null)

export const email: Rule = (value) =>
  value.length > 0 && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim()) ? { code: 'email' } : null

export const httpUrl: Rule = (value) => {
  if (value.length === 0) return null
  try {
    const url = new URL(value)
    return url.protocol === 'http:' || url.protocol === 'https:' ? null : { code: 'url' }
  } catch {
    return { code: 'url' }
  }
}

/** Runs each field's rules and returns the first failure per field (same shape as the API's `errors`). */
export function validateFields(fields: Record<string, [string, ...Rule[]]>): Record<string, FieldError[]> {
  const errors: Record<string, FieldError[]> = {}
  for (const [name, [value, ...rules]] of Object.entries(fields)) {
    for (const rule of rules) {
      const error = rule(value)
      if (error) {
        errors[name] = [error]
        break
      }
    }
  }
  return errors
}

/** Same limits as the backend domain (User, Workspace, Board). */
export const limits = {
  displayName: 100,
  email: 254,
  passwordMin: 8,
  passwordMax: 128,
  avatarUrl: 2048,
  workspaceName: 100,
  boardTitle: 200,
  boardDescription: 2000,
} as const
