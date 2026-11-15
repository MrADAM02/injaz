import type { Card, CardPriority, CardSummary, DueBucket } from '~/types/api'

export const priorities: readonly CardPriority[] = ['none', 'low', 'medium', 'high', 'urgent']

/** Badge colours for priorities (`none` shows nothing). Full class strings so Tailwind can see them. */
export const priorityTone: Record<CardPriority, string> = {
  none: '',
  low: 'bg-sky-100 text-sky-900 dark:bg-sky-950 dark:text-sky-200',
  medium: 'bg-yellow-100 text-yellow-900 dark:bg-yellow-950 dark:text-yellow-200',
  high: 'bg-orange-100 text-orange-900 dark:bg-orange-950 dark:text-orange-200',
  urgent: 'bg-red-100 text-red-900 dark:bg-red-950 dark:text-red-200',
}

export type DueState = 'done' | 'overdue' | 'soon' | 'later'

const day = 24 * 60 * 60 * 1000

/** How a due date reads right now: completed, past due, within a day, or later. */
export function dueState(dueAt: string | null, completedAt: string | null, now = Date.now()): DueState | null {
  if (!dueAt) return null
  if (completedAt) return 'done'
  const due = Date.parse(dueAt)
  if (due < now) return 'overdue'
  if (due - now <= day) return 'soon'
  return 'later'
}

export const dueTone: Record<DueState, string> = {
  done: 'bg-green-100 text-green-900 dark:bg-green-950 dark:text-green-200',
  overdue: 'bg-red-100 text-red-900 dark:bg-red-950 dark:text-red-200',
  soon: 'bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-200',
  later: 'bg-subtle text-muted',
}

/**
 * The same buckets as the API's `due` filter, evaluated in the browser's time zone:
 * overdue = past and open; today = this calendar day; week = start of today through the next 7 days.
 */
export function inDueBucket(card: Pick<CardSummary, 'dueAt' | 'completedAt'>, bucket: DueBucket, now = new Date()): boolean {
  if (bucket === 'none') return card.dueAt === null
  if (card.dueAt === null) return false
  const due = Date.parse(card.dueAt)
  if (bucket === 'overdue') return card.completedAt === null && due < now.getTime()

  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
  const days = bucket === 'today' ? 1 : 8
  const end = new Date(now.getFullYear(), now.getMonth(), now.getDate() + days).getTime()
  return due >= startOfToday && due < end
}

export interface CardFilters {
  q: string
  labelIds: readonly string[]
  assigneeIds: readonly string[]
  due: DueBucket | null
}

export const emptyFilters: CardFilters = { q: '', labelIds: [], assigneeIds: [], due: null }

export function hasFilters(filters: CardFilters): boolean {
  return filters.q.trim() !== '' || filters.labelIds.length > 0 || filters.assigneeIds.length > 0 || filters.due !== null
}

/** Board filters: title text, then *any of* the labels, *any of* the assignees, and the due bucket. */
export function matchesFilters(card: CardSummary, filters: CardFilters, locale?: string, now = new Date()): boolean {
  const q = filters.q.trim().toLocaleLowerCase(locale)
  if (q && !card.title.toLocaleLowerCase(locale).includes(q)) return false
  if (filters.labelIds.length > 0 && !card.labelIds.some((id) => filters.labelIds.includes(id))) return false
  if (filters.assigneeIds.length > 0 && !card.assigneeIds.some((id) => filters.assigneeIds.includes(id))) return false
  if (filters.due && !inDueBucket(card, filters.due, now)) return false
  return true
}

/** The tile for a full card (after a PATCH), so the board updates without refetching. */
export function toSummary(card: Card): CardSummary {
  return {
    id: card.id,
    listId: card.listId,
    title: card.title,
    rank: card.rank,
    dueAt: card.dueAt,
    priority: card.priority,
    completedAt: card.completedAt,
    hasDescription: card.description !== null,
    labelIds: [...card.labelIds],
    assigneeIds: [...card.assigneeIds],
    checklistDone: card.checklist.filter((item) => item.isDone).length,
    checklistTotal: card.checklist.length,
    commentCount: card.commentCount,
  }
}

/** `<input type="datetime-local">` value (local time, minutes) ⇄ ISO-8601 UTC. */
export function toLocalInput(iso: string | null): string {
  if (!iso) return ''
  const date = new Date(iso)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

export function fromLocalInput(value: string): string | null {
  if (!value) return null
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date.toISOString()
}

/** The browser's IANA time zone for the API's `tz` parameter ("today" is the user's day, not UTC's). */
export function browserTimeZone(): string | undefined {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone
  } catch {
    return undefined
  }
}
