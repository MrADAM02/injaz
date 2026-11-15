/** Short, non-blocking feedback. `error()` accepts any thrown value and translates API errors. */
export function useToast() {
  const toasts = useToastStore()
  const problem = useProblem()

  return {
    success: (message: string) => toasts.push(message, 'success'),
    info: (message: string) => toasts.push(message, 'info'),
    error: (error: unknown) => toasts.push(typeof error === 'string' ? error : problem.message(error), 'error'),
  }
}
