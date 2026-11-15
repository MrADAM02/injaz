declare module '#app' {
  interface PageMeta {
    /** Reachable without signing in (e.g. the invitation preview). */
    public?: boolean
    /** Only for signed-out visitors (login, register); signed-in users go to `?redirect=` or home. */
    guestOnly?: boolean
  }
}

export {}
