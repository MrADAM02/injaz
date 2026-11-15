import { describe, expect, it } from 'vitest'
import type { Activity, Card, CardSummary } from '~/types/api'
import { activityText, type ActivityContext } from '~/utils/activity'
import { dueState, fromLocalInput, hasFilters, inDueBucket, matchesFilters, toLocalInput, toSummary, emptyFilters } from '~/utils/cards'
import { compareRanked, moveTo, neighbours, upsertRanked } from '~/utils/ordering'

const tile = (overrides: Partial<CardSummary> = {}): CardSummary => ({
  id: 'c1',
  listId: 'l1',
  title: 'Fix login',
  rank: 'a0',
  dueAt: null,
  priority: 'none',
  completedAt: null,
  hasDescription: false,
  labelIds: [],
  assigneeIds: [],
  checklistDone: 0,
  checklistTotal: 0,
  commentCount: 0,
  ...overrides,
})

describe('ordering', () => {
  it('compares by rank, then id, by code unit (like COLLATE "C")', () => {
    expect(compareRanked({ id: 'b', rank: 'a0' }, { id: 'a', rank: 'a1' })).toBeLessThan(0)
    // Upper-case sorts before lower-case in "C" collation, unlike a locale-aware compare.
    expect(compareRanked({ id: 'x', rank: 'Z' }, { id: 'y', rank: 'a' })).toBeLessThan(0)
    expect(compareRanked({ id: 'a', rank: 'm' }, { id: 'b', rank: 'm' })).toBeLessThan(0)
    expect(compareRanked({ id: 'a', rank: 'm' }, { id: 'a', rank: 'm' })).toBe(0)
  })

  it('upserts into rank order and replaces an existing id', () => {
    const items = [
      { id: '1', rank: 'a' },
      { id: '3', rank: 'c' },
    ]
    expect(upsertRanked(items, { id: '2', rank: 'b' }).map((i) => i.id)).toEqual(['1', '2', '3'])
    expect(upsertRanked(items, { id: '1', rank: 'd' }).map((i) => i.id)).toEqual(['3', '1'])
    expect(upsertRanked([], { id: '1', rank: 'a' })).toHaveLength(1)
  })

  it('moves an item to an index of the resulting array', () => {
    const items = ['a', 'b', 'c', 'd'].map((id) => ({ id }))
    expect(moveTo(items, 'a', 2).map((i) => i.id)).toEqual(['b', 'c', 'a', 'd'])
    expect(moveTo(items, 'd', 0).map((i) => i.id)).toEqual(['d', 'a', 'b', 'c'])
    expect(moveTo(items, 'b', 99).map((i) => i.id)).toEqual(['a', 'c', 'd', 'b'])
  })

  it('reports the neighbours of a drop position, omitting missing ends', () => {
    const items = ['a', 'b', 'c'].map((id) => ({ id }))
    expect(neighbours(items, 0)).toEqual({ beforeId: 'b' })
    expect(neighbours(items, 1)).toEqual({ afterId: 'a', beforeId: 'c' })
    expect(neighbours(items, 2)).toEqual({ afterId: 'b' })
    expect(neighbours([{ id: 'a' }], 0)).toEqual({})
  })
})

describe('due dates', () => {
  const now = new Date(2026, 10, 15, 12, 0) // Sun 15 Nov 2026, 12:00 local

  it('classifies a due date relative to now', () => {
    const at = (h: number) => new Date(now.getTime() + h * 3_600_000).toISOString()
    expect(dueState(null, null, now.getTime())).toBeNull()
    expect(dueState(at(-1), null, now.getTime())).toBe('overdue')
    expect(dueState(at(-1), at(-2), now.getTime())).toBe('done')
    expect(dueState(at(5), null, now.getTime())).toBe('soon')
    expect(dueState(at(48), null, now.getTime())).toBe('later')
  })

  it('matches the API buckets in the local time zone', () => {
    const due = (d: Date) => ({ dueAt: d.toISOString(), completedAt: null })
    const today = new Date(2026, 10, 15, 23, 30)
    const tomorrow = new Date(2026, 10, 16, 9, 0)
    const in7 = new Date(2026, 10, 22, 23, 59)
    const in8 = new Date(2026, 10, 23, 0, 0)
    const earlier = new Date(2026, 10, 15, 8, 0)

    expect(inDueBucket(due(today), 'today', now)).toBe(true)
    expect(inDueBucket(due(earlier), 'today', now)).toBe(true)
    expect(inDueBucket(due(tomorrow), 'today', now)).toBe(false)
    expect(inDueBucket(due(in7), 'week', now)).toBe(true)
    expect(inDueBucket(due(in8), 'week', now)).toBe(false)
    expect(inDueBucket(due(earlier), 'overdue', now)).toBe(true)
    expect(inDueBucket({ dueAt: earlier.toISOString(), completedAt: earlier.toISOString() }, 'overdue', now)).toBe(false)
    expect(inDueBucket({ dueAt: null, completedAt: null }, 'none', now)).toBe(true)
    expect(inDueBucket(due(today), 'none', now)).toBe(false)
  })

  it('round-trips datetime-local values', () => {
    const iso = new Date(2026, 10, 15, 17, 30).toISOString()
    expect(toLocalInput(iso)).toBe('2026-11-15T17:30')
    expect(fromLocalInput('2026-11-15T17:30')).toBe(iso)
    expect(fromLocalInput('')).toBeNull()
    expect(toLocalInput(null)).toBe('')
  })
})

describe('board filters', () => {
  it('is inactive when empty', () => {
    expect(hasFilters(emptyFilters)).toBe(false)
    expect(hasFilters({ ...emptyFilters, q: '  ' })).toBe(false)
    expect(hasFilters({ ...emptyFilters, labelIds: ['x'] })).toBe(true)
  })

  it('matches title text case-insensitively and any of the labels / assignees', () => {
    const card = tile({ title: 'Fix Login page', labelIds: ['red'], assigneeIds: ['u1'] })
    expect(matchesFilters(card, { ...emptyFilters, q: 'login' })).toBe(true)
    expect(matchesFilters(card, { ...emptyFilters, q: 'signup' })).toBe(false)
    expect(matchesFilters(card, { ...emptyFilters, labelIds: ['blue', 'red'] })).toBe(true)
    expect(matchesFilters(card, { ...emptyFilters, labelIds: ['blue'] })).toBe(false)
    expect(matchesFilters(card, { ...emptyFilters, assigneeIds: ['u2'] })).toBe(false)
    expect(matchesFilters(card, { ...emptyFilters, labelIds: ['red'], assigneeIds: ['u1'] })).toBe(true)
  })

  it('matches Arabic titles', () => {
    expect(matchesFilters(tile({ title: 'إصلاح صفحة الدخول' }), { ...emptyFilters, q: 'صفحة' }, 'ar')).toBe(true)
  })
})

describe('toSummary', () => {
  it('derives tile badges from the full card', () => {
    const card: Card = {
      id: 'c1',
      boardId: 'b1',
      listId: 'l1',
      title: 'T',
      description: 'notes',
      rank: 'a0',
      dueAt: null,
      priority: 'high',
      completedAt: null,
      isArchived: false,
      labelIds: ['x'],
      assigneeIds: [],
      checklist: [
        { id: 'i1', cardId: 'c1', text: 'a', isDone: true, rank: 'a' },
        { id: 'i2', cardId: 'c1', text: 'b', isDone: false, rank: 'b' },
      ],
      commentCount: 3,
      createdById: 'u1',
      createdAt: '2026-11-01T00:00:00Z',
      updatedAt: '2026-11-01T00:00:00Z',
    }
    expect(toSummary(card)).toMatchObject({ hasDescription: true, checklistDone: 1, checklistTotal: 2, commentCount: 3, priority: 'high' })
    expect(toSummary({ ...card, description: null }).hasDescription).toBe(false)
  })
})

describe('activityText', () => {
  const messages: Record<string, string> = {
    'activity.thisCard': 'this card',
    'activity.untitled': 'a card',
    'activity.types.unknown': 'made a change',
    'activity.types.card.moved': 'moved {title} from {fromList} to {toList}',
    'activity.types.card.dueChanged': 'set {title} to be due {date}',
    'activity.types.card.dueRemoved': 'removed the due date of {title}',
    'activity.types.card.priorityChanged': 'set the priority of {title} to {priority}',
    'activity.types.card.labelAdded': 'added the label {name} to {title}',
    'priority.urgent': 'Urgent',
    'colors.red': 'Red',
  }
  const ctx: ActivityContext = {
    t: (key, params) => (messages[key] ?? key).replace(/\{(\w+)\}/g, (_, name: string) => String(params?.[name] ?? '')),
    te: (key) => key in messages,
    formatDate: (iso) => `<${iso}>`,
  }
  const strip = (s: string) => s.replace(/[\u2066-\u2069]/g, '')
  const entry = (type: string, data: Record<string, unknown>): Activity => ({
    id: 'a1',
    type,
    boardId: 'b1',
    cardId: 'c1',
    actor: { userId: 'u1', displayName: 'Sara', avatarUrl: null },
    data,
    createdAt: '2026-11-15T10:00:00Z',
  })

  it('fills in names as they were and isolates them for bidi text', () => {
    const text = activityText(entry('card.moved', { title: 'Fix login', fromList: 'To do', toList: 'Done' }), ctx)
    expect(strip(text)).toBe('moved Fix login from To do to Done')
    expect(text).toContain('\u2068Fix login\u2069')
  })

  it('says "this card" inside the card’s own feed', () => {
    expect(strip(activityText(entry('card.moved', { title: 'X', fromList: 'A', toList: 'B' }), { ...ctx, inCard: true }))).toBe('moved this card from A to B')
  })

  it('handles due dates, priorities and unnamed labels', () => {
    expect(strip(activityText(entry('card.dueChanged', { title: 'X', to: '2026-11-20T00:00:00Z' }), ctx))).toBe('set X to be due <2026-11-20T00:00:00Z>')
    expect(strip(activityText(entry('card.dueChanged', { title: 'X', to: null }), ctx))).toBe('removed the due date of X')
    expect(strip(activityText(entry('card.priorityChanged', { title: 'X', to: 'urgent' }), ctx))).toBe('set the priority of X to Urgent')
    expect(strip(activityText(entry('card.labelAdded', { title: 'X', name: null, color: 'red' }), ctx))).toBe('added the label Red to X')
  })

  it('falls back for types it does not know', () => {
    expect(activityText(entry('card.teleported', {}), ctx)).toBe('made a change')
  })
})
