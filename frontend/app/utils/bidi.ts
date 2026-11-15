/**
 * Wraps a left-to-right value (email, URL) in Unicode isolate marks (LRI … PDI) before it is interpolated
 * into a translated sentence, so Arabic text around it keeps its order and punctuation stays in place.
 */
export function isolateLtr(value: string | null | undefined): string {
  return `\u2066${value ?? ''}\u2069`
}

/**
 * Wraps user content (titles, names) in first-strong isolate marks (FSI … PDI): each value takes the
 * direction of its own first letter, so an English title inside an Arabic sentence (or vice versa) reads right.
 */
export function isolate(value: string | null | undefined): string {
  return `\u2068${value ?? ''}\u2069`
}
