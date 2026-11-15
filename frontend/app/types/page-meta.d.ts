declare module '#app' {
  interface PageMeta {
    /** Reachable without signing in (e.g. the invitation preview). */
    public?: boolean
    /** Only for signed-out visitors (login, register); signed-in users go to `?redirect=` or home. */
    guestOnly?: boolean
    /** Fills the viewport edge to edge with no page padding (the board view scrolls inside itself). */
    fullBleed?: boolean
  }
}

export {}
