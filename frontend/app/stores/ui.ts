import { defineStore } from 'pinia'

/** App-shell state shared between the layout and pages. */
export const useUiStore = defineStore('ui', () => {
  const drawerOpen = ref(false)
  const createWorkspaceOpen = ref(false)
  return { drawerOpen, createWorkspaceOpen }
})
