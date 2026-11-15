/**
 * The `?redirect=` target after sign-in, if it is safe: a same-app path. Rejects absolute and protocol-relative
 * URLs (`https://evil`, `//evil`, `/\evil`) so the login page can't be used as an open redirect.
 */
export function safeRedirect(value: unknown, fallback = '/'): string {
  const candidate = Array.isArray(value) ? value[0] : value
  if (typeof candidate !== 'string' || !candidate.startsWith('/')) return fallback
  if (candidate.startsWith('//') || candidate.startsWith('/\\')) return fallback
  return candidate
}
