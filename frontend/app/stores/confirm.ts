import { defineStore } from 'pinia'

export interface ConfirmRequest {
  title: string
  body?: string
  confirmLabel: string
  danger?: boolean
  /** When set, the confirm button stays disabled until this exact text is typed (destructive actions). */
  requireText?: string
  requireTextLabel?: string
}

/** One confirmation at a time; <UiConfirmHost> renders it and resolves the promise. */
export const useConfirmStore = defineStore('confirm', () => {
  const current = shallowRef<(ConfirmRequest & { resolve: (ok: boolean) => void }) | null>(null)

  function ask(request: ConfirmRequest): Promise<boolean> {
    current.value?.resolve(false)
    return new Promise((resolve) => {
      current.value = { ...request, resolve }
    })
  }

  function settle(ok: boolean) {
    current.value?.resolve(ok)
    current.value = null
  }

  return { current, ask, settle }
})
