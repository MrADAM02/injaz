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

// ---------- boards, lists, cards (phase 4–6 endpoints) ----------

export type CardPriority = 'none' | 'low' | 'medium' | 'high' | 'urgent'
export type DueBucket = 'overdue' | 'today' | 'week' | 'none'

/** A card tile: what `GET /api/boards/{id}`, search, My Tasks and `card.upserted` events carry. */
export interface CardSummary {
  id: string
  listId: string
  title: string
  rank: string
  dueAt: string | null
  priority: CardPriority
  completedAt: string | null
  hasDescription: boolean
  labelIds: string[]
  assigneeIds: string[]
  checklistDone: number
  checklistTotal: number
  commentCount: number
}

export interface BoardList {
  id: string
  boardId: string
  title: string
  rank: string
  isArchived: boolean
  cards: CardSummary[]
}

/** `GET /api/boards/{id}/lists/archived`: `cardCount` counts the open cards that come back on restore. */
export interface ArchivedList {
  id: string
  title: string
  cardCount: number
}

export interface Label {
  id: string
  name: string | null
  color: PaletteColor
}

export interface BoardMember {
  userId: string
  displayName: string
  avatarUrl: string | null
  role: WorkspaceRole
}

/** `GET /api/boards/{id}`: open lists in order, each with its open cards in order. */
export interface BoardDetail {
  board: BoardSummary
  myRole: WorkspaceRole
  lists: BoardList[]
  labels: Label[]
  members: BoardMember[]
}

export interface ChecklistItem {
  id: string
  cardId: string
  text: string
  isDone: boolean
  rank: string
}

/** `GET /api/cards/{id}`: the full card for the card dialog. */
export interface Card {
  id: string
  boardId: string
  listId: string
  title: string
  description: string | null
  rank: string
  dueAt: string | null
  priority: CardPriority
  completedAt: string | null
  isArchived: boolean
  labelIds: string[]
  assigneeIds: string[]
  checklist: ChecklistItem[]
  commentCount: number
  createdById: string
  createdAt: string
  updatedAt: string
}

export interface Actor {
  userId: string
  displayName: string
  avatarUrl: string | null
}

export interface Comment {
  id: string
  cardId: string
  author: Actor
  body: string
  createdAt: string
  editedAt: string | null
}

export interface Activity {
  id: string
  type: string
  boardId: string
  cardId: string | null
  actor: Actor
  /** Names and titles as they were when it happened (see the README's activity section). */
  data: Record<string, unknown>
  createdAt: string
}

/** Keyset page: pass `nextCursor` back as `?cursor=`; null means the end. */
export interface Page<T> {
  items: T[]
  nextCursor: string | null
}

export interface CardSearchResult {
  card: CardSummary
  listTitle: string
  isArchived: boolean
  updatedAt: string
}

export interface MyTask {
  card: CardSummary
  workspaceId: string
  workspaceName: string
  boardId: string
  boardTitle: string
  boardColor: PaletteColor
  listTitle: string
}

/** SignalR `boardEvent` (README › Real-time). */
export type BoardEvent = {
  boardId: string
  actorId: string
  occurredAt: string
} & (
  | { type: 'board.updated'; payload: BoardSummary }
  | { type: 'board.deleted'; payload: { id: string } }
  | { type: 'list.upserted'; payload: Omit<BoardList, 'cards'> & { cards?: CardSummary[] } }
  | { type: 'list.removed'; payload: { id: string } }
  | { type: 'card.upserted'; payload: CardSummary }
  | { type: 'card.removed'; payload: { id: string } }
  | { type: 'label.upserted'; payload: Label }
  | { type: 'label.removed'; payload: { id: string } }
)
