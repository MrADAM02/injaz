/** Wraps an async operation so concurrent callers share one in-flight run (e.g. a token refresh). */
export function singleFlight<T>(operation: () => Promise<T>): () => Promise<T> {
  let inflight: Promise<T> | null = null
  return () => {
    inflight ??= operation().finally(() => {
      inflight = null
    })
    return inflight
  }
}
