import { defineStore } from 'pinia'
import type { Workspace } from '~/types/api'
import { isApiError } from '~/utils/api-client'
import { singleFlight } from '~/utils/single-flight'

const lastKey = 'injaz.lastWorkspace'

/** The signed-in user's workspaces: a small, bounded list, loaded whole and patched locally after changes. */
export const useWorkspacesStore = defineStore('workspaces', () => {
  const { $api } = useNuxtApp()

  const items = ref<Workspace[]>([])
  const loaded = ref(false)

  const byId = (id: string | undefined) => items.value.find((w) => w.id === id)

  const load = singleFlight(async () => {
    items.value = await $api.get<Workspace[]>('/api/workspaces')
    loaded.value = true
  })

  async function ensureLoaded() {
    if (!loaded.value) await load()
  }

  function upsert(workspace: Workspace) {
    const index = items.value.findIndex((w) => w.id === workspace.id)
    if (index >= 0) items.value.splice(index, 1, workspace)
    else items.value.push(workspace)
  }

  function drop(id: string) {
    items.value = items.value.filter((w) => w.id !== id)
    if (lastWorkspaceId() === id) remember(null)
  }

  async function create(name: string) {
    const workspace = await $api.post<Workspace>('/api/workspaces', { name })
    upsert(workspace)
    return workspace
  }

  async function rename(id: string, name: string) {
    upsert(await $api.patch<Workspace>(`/api/workspaces/${id}`, { name }))
  }

  async function remove(id: string) {
    await $api.delete(`/api/workspaces/${id}`)
    drop(id)
  }

  async function leave(id: string, myUserId: string) {
    await $api.delete(`/api/workspaces/${id}/members/${myUserId}`)
    drop(id)
  }

  async function transferOwnership(id: string, userId: string) {
    await $api.post(`/api/workspaces/${id}/transfer-ownership`, { userId })
    await refreshOne(id)
  }

  /** Re-reads one workspace (role or member count changed); drops it if the caller lost access. */
  async function refreshOne(id: string) {
    try {
      upsert(await $api.get<Workspace>(`/api/workspaces/${id}`))
    } catch (error) {
      if (isApiError(error, 'workspace.notFound')) drop(id)
      else throw error
    }
  }

  function remember(id: string | null) {
    try {
      if (id) localStorage.setItem(lastKey, id)
      else localStorage.removeItem(lastKey)
    } catch {
      // Storage unavailable (private mode): only the convenience is lost.
    }
  }

  function lastWorkspaceId(): string | null {
    try {
      return localStorage.getItem(lastKey)
    } catch {
      return null
    }
  }

  function reset() {
    items.value = []
    loaded.value = false
  }

  return {
    items,
    loaded,
    byId,
    load,
    ensureLoaded,
    upsert,
    drop,
    create,
    rename,
    remove,
    leave,
    transferOwnership,
    refreshOne,
    remember,
    lastWorkspaceId,
    reset,
  }
})
