import { defineStore } from 'pinia'

export type ToastTone = 'success' | 'error' | 'info'

export interface Toast {
  id: number
  message: string
  /** `message` is an i18n key, translated when rendered (so it follows a language switch). */
  translate: boolean
  tone: ToastTone
}

const lifetime = { success: 3500, info: 5000, error: 7000 } satisfies Record<ToastTone, number>

export const useToastStore = defineStore('toast', () => {
  const items = ref<Toast[]>([])
  let nextId = 1

  function push(message: string, tone: ToastTone = 'info', translate = false) {
    // Identical messages don't stack (e.g. several requests failing at once while offline).
    if (items.value.some((t) => t.message === message && t.tone === tone)) return
    const id = nextId++
    items.value = [...items.value.slice(-2), { id, message, translate, tone }]
    setTimeout(() => dismiss(id), lifetime[tone])
  }

  function dismiss(id: number) {
    items.value = items.value.filter((t) => t.id !== id)
  }

  return { items, push, dismiss }
})
