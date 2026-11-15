import type { ConfirmRequest } from '~/stores/confirm'

/** Promise-based confirmation dialog: `if (await confirm({ title, … })) …`. Rendered by <UiConfirmHost>. */
export function useConfirm() {
  const store = useConfirmStore()
  return (request: ConfirmRequest) => store.ask(request)
}
