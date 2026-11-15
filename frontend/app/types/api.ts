// Shapes of the Injaz API (backend/src/Injaz.Application). Enums are camelCase strings; times are ISO-8601 UTC.

export type Locale = 'ar' | 'en'
export type WorkspaceRole = 'viewer' | 'member' | 'admin' | 'owner'
export type InvitationStatus = 'pending' | 'accepted' | 'revoked' | 'expired'
export type PaletteColor = 'green' | 'yellow' | 'orange' | 'red' | 'purple' | 'blue' | 'sky' | 'lime' | 'pink' | 'gray'

export interface User {
  id: string
  email: string
  displayName: string
  avatarUrl: string | null
  locale: Locale
  createdAt: string
}

/** Body of register / login / refresh / password change. The refresh token itself lives in an httpOnly cookie. */
export interface AuthSession {
  accessToken: string
  accessTokenExpiresAt: string
  user: User
}

export interface Workspace {
  id: string
  name: string
  myRole: WorkspaceRole
  memberCount: number
  createdAt: string
}

export interface Member {
  userId: string
  displayName: string
  email: string
  avatarUrl: string | null
  role: WorkspaceRole
  joinedAt: string
}

export interface Invitation {
  id: string
  email: string
  role: WorkspaceRole
  invitedById: string
  invitedByName: string
  createdAt: string
  expiresAt: string
}

export interface CreatedInvitation {
  invitation: Invitation
  token: string
  acceptUrl: string
}

export interface InvitationPreview {
  workspaceId: string
  workspaceName: string
  invitedByName: string
  email: string
  role: WorkspaceRole
  expiresAt: string
  status: InvitationStatus
}

export interface BoardSummary {
  id: string
  workspaceId: string
  title: string
  description: string | null
  color: PaletteColor
  isArchived: boolean
  createdAt: string
}

/** One validation failure for a field: a code from `validation.*` plus optional params (`{ max: 100 }`). */
export interface FieldError {
  code: string
  params?: Record<string, unknown>
}

/** RFC 9457 ProblemDetails as the API sends it. */
export interface ProblemDetails {
  status?: number
  title?: string
  detail?: string
  code?: string
  traceId?: string
  errors?: Record<string, FieldError[]>
}
