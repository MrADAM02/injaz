import { readdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import ar from '../i18n/locales/ar.json'
import en from '../i18n/locales/en.json'

type Messages = { [key: string]: string | Messages }

function keys(messages: Messages, prefix = ''): string[] {
  return Object.entries(messages).flatMap(([key, value]) =>
    typeof value === 'string' ? [`${prefix}${key}`] : keys(value, `${prefix}${key}.`),
  )
}

function placeholders(message: string): string[] {
  return [...new Set([...message.matchAll(/\{(\w+)\}/g)].map((m) => m[1]!))].sort()
}

function lookup(messages: Messages, key: string): string {
  return key.split('.').reduce<Messages | string>((node, part) => (node as Messages)[part]!, messages) as string
}

describe('locale files', () => {
  it('have the same keys in Arabic and English', () => {
    expect(keys(ar).sort()).toEqual(keys(en).sort())
  })

  it('use the same placeholders in both languages', () => {
    for (const key of keys(en)) {
      expect(placeholders(lookup(ar, key)), key).toEqual(placeholders(lookup(en, key)))
    }
  })

  it('give Arabic plurals all six CLDR forms', () => {
    for (const key of keys(en).filter((k) => lookup(en, k).includes(' | '))) {
      expect(lookup(ar, key).split(' | '), key).toHaveLength(6)
    }
  })

  it('never contain unescaped vue-i18n special characters', () => {
    // `@` starts a linked message and must be written as {'@'}.
    for (const messages of [en, ar]) {
      for (const key of keys(messages)) {
        expect(lookup(messages, key), key).not.toMatch(/(?<!\{')@/)
      }
    }
  })

  it('translate every error code the API can return', () => {
    // Codes thrown in the backend (kept in sync by grepping the C# sources).
    const backend = fileURLToPath(new URL('../../backend/src', import.meta.url))
    const codes = new Set<string>()
    const walk = (dir: string) => {
      for (const entry of readdirSync(dir, { withFileTypes: true })) {
        const path = `${dir}/${entry.name}`
        if (entry.isDirectory()) {
          if (!['bin', 'obj', 'Migrations'].includes(entry.name)) walk(path)
        } else if (entry.name.endsWith('.cs')) {
          const source = readFileSync(path, 'utf8')
          for (const m of source.matchAll(/(?:Exception|ProblemCode|Code)\w*\(\s*"([a-z]+\.[a-zA-Z]+)"/g)) codes.add(m[1]!)
          for (const m of source.matchAll(/const string \w+ = "([a-z]+\.[a-zA-Z]+)"/g)) codes.add(m[1]!)
          for (const m of source.matchAll(/NotFoundException\("(\w+)"\)/g)) codes.add(`${m[1]}.notFound`)
        }
      }
    }
    walk(backend)

    const ignored = /^(board|list|card|label|checklist|comment)\.(created|renamed|archived|restored|deleted|updated|upserted|removed|moved|itemAdded|itemCompleted|itemReopened|itemDeleted|added|descriptionChanged|dueChanged|priorityChanged|completed|reopened|assigned|unassigned|labelAdded|labelRemoved)$/
    const missing = [...codes].filter((code) => !ignored.test(code) && !keys(en).includes(`errors.${code}`))
    expect(missing).toEqual([])
  })
})
