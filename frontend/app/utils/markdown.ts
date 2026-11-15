import DOMPurify from 'dompurify'
import MarkdownIt from 'markdown-it'

/**
 * Card descriptions and comments are Markdown. Raw HTML is off in the parser, and the output is sanitized
 * anyway (defense in depth). Links open in a new tab without leaking the opener or the referrer.
 */
const md = new MarkdownIt({ html: false, linkify: true, breaks: true, typographer: false })

let hooked = false

function ensureHook() {
  if (hooked) return
  hooked = true
  DOMPurify.addHook('afterSanitizeAttributes', (node) => {
    if (node.tagName === 'A') {
      node.setAttribute('target', '_blank')
      node.setAttribute('rel', 'noopener noreferrer nofollow')
    }
  })
}

export function renderMarkdown(source: string | null | undefined): string {
  if (!source) return ''
  ensureHook()
  return DOMPurify.sanitize(md.render(source), {
    ALLOWED_URI_REGEXP: /^(?:https?:|mailto:|#)/i,
    ADD_ATTR: ['target'],
  })
}
