import type { Member, WorkspaceRole } from '~/types/api'

/** Mirrors backend Permission → minimum role (Application/Security/Permissions.cs). The API is the authority. */
export type Permission = 'view' | 'editContent' | 'deleteContent' | 'manageMembers' | 'manageWorkspace' | 'ownWorkspace'

const rank: Record<WorkspaceRole, number> = { viewer: 0, member: 1, admin: 2, owner: 3 }

const minimumRole: Record<Permission, WorkspaceRole> = {
  view: 'viewer',
  editContent: 'member',
  deleteContent: 'admin',
  manageMembers: 'admin',
  manageWorkspace: 'admin',
  ownWorkspace: 'owner',
}

export function can(role: WorkspaceRole | null | undefined, permission: Permission): boolean {
  return role != null && rank[role] >= rank[minimumRole[permission]]
}

/** Roles that can be given through invitations or role changes; ownership only moves via transfer. */
export const assignableRoles: readonly WorkspaceRole[] = ['admin', 'member', 'viewer']

/** Owner first, then by role, then by name (the API already returns this order; used after local edits). */
export function compareMembers(a: Member, b: Member, collator: Intl.Collator): number {
  return rank[b.role] - rank[a.role] || collator.compare(a.displayName, b.displayName)
}

/** Whether the caller may change this member's role or remove them (leaving yourself is separate). */
export function canManageMember(myRole: WorkspaceRole | null | undefined, myUserId: string | undefined, member: Member): boolean {
  return can(myRole, 'manageMembers') && member.role !== 'owner' && member.userId !== myUserId
}
