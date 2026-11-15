import { safeRedirect } from '~/utils/redirect'

/**
 * Restores the session once per page load, then gates routes. Pages are private by default:
 * `definePageMeta({ public: true })` opens one to everyone, and `guestOnly: true` (login, register)
 * sends signed-in users on to `?redirect=` or home.
 */
export default defineNuxtRouteMiddleware(async (to) => {
  const auth = useAuthStore()
  await auth.ensureInitialized()

  if (to.meta.guestOnly) {
    if (auth.isAuthenticated) return navigateTo(safeRedirect(to.query.redirect), { replace: true })
    return
  }

  if (!to.meta.public && !auth.isAuthenticated) {
    return navigateTo({ path: '/login', query: to.fullPath === '/' ? {} : { redirect: to.fullPath } }, { replace: true })
  }
})
