import type { Member, WorkspaceRole } from '~/types/api'

/** A workspace's member list (bounded, so loaded whole) with the role/remove actions. */
export function useMembers(workspaceId: MaybeRefOrGetter<string>) {
  const api = useApi()
  const workspaces = useWorkspacesStore()

  const members = ref<Member[]>([])
  const loading = ref(false)
  const error = ref<unknown>(null)

  const base = () => `/api/workspaces/${toValue(workspaceId)}/members`

  async function load() {
    loading.value = true
    error.value = null
    try {
      members.value = await api.get<Member[]>(base())
    } catch (e) {
      error.value = e
    } finally {
      loading.value = false
    }
  }

  async function changeRole(userId: string, role: WorkspaceRole) {
    const updated = await api.patch<Member>(`${base()}/${userId}`, { role })
    members.value = members.value.map((m) => (m.userId === userId ? updated : m))
    return updated
  }

  async function remove(userId: string) {
    await api.delete(`${base()}/${userId}`)
    members.value = members.value.filter((m) => m.userId !== userId)
    await workspaces.refreshOne(toValue(workspaceId)) // member count
  }

  return { members, loading, error, load, changeRole, remove }
}
