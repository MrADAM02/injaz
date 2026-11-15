import type { Workspace } from '~/types/api'
import { can, type Permission } from '~/utils/roles'

/**
 * The workspace in the route (`/w/:workspaceId/...`) from the store, plus permission checks against the
 * caller's role. `missing` means the list is loaded and it isn't there (deleted, or the caller was removed).
 */
export function useCurrentWorkspace() {
  const route = useRoute()
  const workspaces = useWorkspacesStore()

  const id = computed(() => String(route.params.workspaceId ?? ''))
  const workspace = computed<Workspace | undefined>(() => workspaces.byId(id.value))
  const missing = computed(() => workspaces.loaded && !workspace.value)
  const allows = (permission: Permission) => can(workspace.value?.myRole, permission)

  return { id, workspace, missing, allows }
}
