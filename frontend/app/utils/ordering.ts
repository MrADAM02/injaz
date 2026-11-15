/**
 * Client side of the API's fractional ordering (README › Ordering). The server owns the ranks; the client only
 * keeps arrays sorted the same way and reports a drop position as the ids of its neighbours.
 */
export interface Ranked {
  id: string
  rank: string
}

/**
 * The server orders by `(rank, id)` with `COLLATE "C"`, i.e. by code unit. JavaScript's `<` on strings
 * compares code units too (ranks are base-62 ASCII; ids are lowercase hex GUIDs), so this matches exactly.
 */
export function compareRanked(a: Ranked, b: Ranked): number {
  if (a.rank !== b.rank) return a.rank < b.rank ? -1 : 1
  if (a.id !== b.id) return a.id < b.id ? -1 : 1
  return 0
}

/** Inserts or replaces `item` (by id) and keeps the array sorted. Returns a new array. */
export function upsertRanked<T extends Ranked>(items: readonly T[], item: T): T[] {
  const rest = items.filter((existing) => existing.id !== item.id)
  const index = rest.findIndex((existing) => compareRanked(item, existing) < 0)
  if (index < 0) rest.push(item)
  else rest.splice(index, 0, item)
  return rest
}

/** Moves the item with `id` to `toIndex` (an index into the result). Returns a new array. */
export function moveTo<T extends { id: string }>(items: readonly T[], id: string, toIndex: number): T[] {
  const item = items.find((existing) => existing.id === id)
  if (!item) return [...items]
  const rest = items.filter((existing) => existing.id !== id)
  const index = Math.max(0, Math.min(toIndex, rest.length))
  rest.splice(index, 0, item)
  return rest
}

/**
 * The move request for the item now at `index` of `items` (its final order): the item just above it
 * (`afterId`) and just below it (`beforeId`). Either is omitted at the ends.
 */
export function neighbours(items: readonly { id: string }[], index: number): { afterId?: string; beforeId?: string } {
  const result: { afterId?: string; beforeId?: string } = {}
  const above = items[index - 1]
  const below = items[index + 1]
  if (above) result.afterId = above.id
  if (below) result.beforeId = below.id
  return result
}
