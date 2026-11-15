import type { Activity, CardPriority } from '~/types/api'
import { isolate } from './bidi'

/** What the feed needs from vue-i18n plus formatting; injected so this stays a pure, testable function. */
export interface ActivityContext {
  t: (key: string, params?: Record<string, unknown>) => string
  te: (key: string) => boolean
  formatDate: (iso: string) => string
  /** In a card's own feed the card is "this card" instead of its title. */
  inCard?: boolean
}

const str = (value: unknown): string => (typeof value === 'string' ? value : '')

/**
 * One feed line (without the actor, who is shown separately), e.g. `moved “Fix login” from To do to Done`.
 * Entries keep names as they were (`data`), so the line still reads right after renames and deletes.
 * Every interpolated name is bidi-isolated: Arabic sentences often contain English titles and vice versa.
 */
export function activityText(entry: Activity, ctx: ActivityContext): string {
  const data = entry.data ?? {}
  const title = ctx.inCard ? ctx.t('activity.thisCard') : isolate(str(data.title) || ctx.t('activity.untitled'))
  const params: Record<string, unknown> = {
    title,
    from: isolate(str(data.from)),
    to: isolate(str(data.to)),
    list: isolate(str(data.list)),
    fromList: isolate(str(data.fromList)),
    toList: isolate(str(data.toList)),
    name: isolate(str(data.name)),
    text: isolate(str(data.text)),
    excerpt: isolate(str(data.excerpt)),
  }

  let key = `activity.types.${entry.type}`
  switch (entry.type) {
    case 'card.dueChanged': {
      const to = str(data.to)
      if (!to) key = 'activity.types.card.dueRemoved'
      else params.date = isolate(ctx.formatDate(to))
      break
    }
    case 'card.priorityChanged':
      params.priority = ctx.t(`priority.${str(data.to) as CardPriority}`)
      break
    case 'card.labelAdded':
    case 'card.labelRemoved':
      // Labels may have no name; then their color is how people know them.
      params.name = isolate(str(data.name) || (str(data.color) ? ctx.t(`colors.${str(data.color)}`) : ''))
      break
  }

  return ctx.te(key) ? ctx.t(key, params) : ctx.t('activity.types.unknown')
}
