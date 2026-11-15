#!/usr/bin/env node
/**
 * Fails when app code uses physical left/right utilities or CSS properties. Arabic (RTL) is the default
 * language, so layout must use logical ones: ms/me, ps/pe, inset-s/inset-e, border-s/e, rounded-s/e,
 * text-start/end, scroll-ms… (`rtl:`-prefixed classes and translate/scale transforms are fine: they are
 * deliberate, direction-aware exceptions).
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { extname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('..', import.meta.url))
const scanDirs = ['app']
const extensions = new Set(['.vue', '.ts', '.css'])

// Tailwind physical utilities (with optional variant prefixes like `md:` and a leading `-` for negatives).
const utility = String.raw`(?:ml|mr|pl|pr|left|right|border-l|border-r|rounded-l|rounded-r|rounded-tl|rounded-tr|rounded-bl|rounded-br|scroll-ml|scroll-mr|scroll-pl|scroll-pr|space-x-reverse|text-left|text-right|float-left|float-right|clear-left|clear-right)`
const tailwindPattern = new RegExp(String.raw`(?<![\w:-])(?:(?:[a-z0-9-]+|\[[^\]]+\]):)*-?${utility}(?:-[\w./[\]-]+)?(?![\w-])`, 'g')

// CSS declarations with physical sides.
const cssPattern = /\b(?:margin|padding|border)-(?:left|right)\b|(?<![\w-])(?:left|right)\s*:|text-align\s*:\s*(?:left|right)|float\s*:\s*(?:left|right)/g

function* files(dir) {
  for (const entry of readdirSync(dir)) {
    const path = join(dir, entry)
    if (statSync(path).isDirectory()) yield* files(path)
    else if (extensions.has(extname(path))) yield path
  }
}

const problems = []
for (const dir of scanDirs) {
  for (const file of files(join(root, dir))) {
    // Comments are prose ("the right edge"), not styles: blank them out but keep line numbers.
    const blank = (comment) => comment.replace(/[^\n]/g, ' ')
    const source = readFileSync(file, 'utf8')
      .replace(/<!--[\s\S]*?-->/g, blank)
      .replace(/\/\*[\s\S]*?\*\//g, blank)
      .replace(/(^|[^:'"`])\/\/.*$/gm, (match, before) => before + blank(match.slice(before.length)))
    const lines = source.split('\n')
    lines.forEach((line, index) => {
      if (line.includes('logical-css-ignore')) return
      for (const pattern of [tailwindPattern, cssPattern]) {
        for (const match of line.matchAll(pattern)) {
          if (match[0].startsWith('rtl:') || match[0].startsWith('ltr:')) continue
          problems.push(`${relative(root, file)}:${index + 1}  ${match[0].trim()}`)
        }
      }
    })
  }
}

if (problems.length > 0) {
  console.error('Physical left/right styles found; use logical properties (ms/me, ps/pe, inset-s/e, text-start/end…):\n')
  for (const problem of problems) console.error(`  ${problem}`)
  process.exit(1)
}

console.log('logical-css: OK')
