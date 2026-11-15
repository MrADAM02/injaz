/**
 * Wraps a left-to-right value (email, URL) in Unicode isolate marks (LRI … PDI) before it is interpolated
 * into a translated sentence, so Arabic text around it keeps its order and punctuation stays in place.
 */
export function isolateLtr(value: string | null | undefined): string {
  return `\u2066${value ?? ''}\u2069`
}
