import { defineStore } from 'pinia'
import type { BoardDetail, BoardEvent, BoardList, BoardMember, BoardSummary, Card, CardSummary, Label, PaletteColor, WorkspaceRole } from '~/types/api'
import { toSummary } from '~/utils/cards'
import { compareRanked, neighbours, upsertRanked } from '~/utils/ordering'
import { can, type Permission } from '~/utils/roles'
import { singleFlight } from '~/utils/single-flight'

export type ListState = Omit<BoardList, 'cards'> & { cards: CardSummary[] }

/** Why the board can no longer be shown (someone archived/deleted it, or we lost access). */
export type BoardClosed = 'archived' | 'deleted' | 'revoked'

/**
 * The board on screen, normalized for the board view: lists in rank order, each with its cards in rank order.
 * Our own changes are applied optimistically; other people's arrive as real-time events (see `receive`).
 * Every array is replaced rather than mutated in place, so Vue re-renders exactly what changed.
 */
export const useBoardStore = defineStore('board', () => {
  const { $api } = useNuxtApp()

  const id = ref<string | null>(null)
  const board = ref<BoardSummary | null>(null)
  const myRole = ref<WorkspaceRole | null>(null)
  const lists = ref<ListState[]>([])
  const labels = ref<Label[]>([])
  const members = ref<BoardMember[]>([])
  const error = ref<unknown>(null)
  const closed = ref<BoardClosed | null>(null)
  /** Bumped when someone else changes a card, so its open dialog knows to refetch. */
  const cardRevisions = ref<Record<string, number>>({})

  const labelById = computed(() => new Map(labels.value.map((label) => [label.id, label])))
  const memberById = computed(() => new Map(members.value.map((member) => [member.userId, member])))
  const allows = (permission: Permission) => can(myRole.value, permission)

  function findCard(cardId: string): { list: ListState; card: CardSummary; index: number } | null {
    for (const list of lists.value) {
      const index = list.cards.findIndex((card) => card.id === cardId)
      if (index >= 0) return { list, card: list.cards[index]!, index }
    }
    return null
  }

  // ---------- loading ----------

  function apply(detail: BoardDetail) {
    board.value = detail.board
    myRole.value = detail.myRole
    lists.value = detail.lists
      .map(({ cards, ...list }) => ({ ...list, cards: [...cards].sort(compareRanked) }))
      .sort(compareRanked)
    labels.value = detail.labels
    members.value = detail.members
  }

  async function load(boardId: string) {
    if (id.value !== boardId) reset()
    id.value = boardId
    error.value = null
    try {
      const detail = await $api.get<BoardDetail>(`/api/boards/${boardId}`)
      if (id.value === boardId) apply(detail)
    } catch (e) {
      if (id.value === boardId) error.value = e
      throw e
    }
  }

  /** Re-reads the whole board (after a reconnect, an unknown list, or a failed move). */
  const reload = singleFlight(async () => {
    if (!id.value) return
    try {
      await load(id.value)
    } catch {
      // `error` is set; the page shows it.
    }
  })

  function reset() {
    id.value = null
    board.value = null
    myRole.value = null
    lists.value = []
    labels.value = []
    members.value = []
    error.value = null
    closed.value = null
    cardRevisions.value = {}
    queued.length = 0
  }

  // ---------- local state changes (our own results and remote events both go through these) ----------

  function putList(list: Omit<BoardList, 'cards'> & { cards?: unknown }) {
    if (list.isArchived) return dropList(list.id)
    const { cards: _ignored, ...fields } = list
    const existing = lists.value.find((l) => l.id === list.id)
    lists.value = upsertRanked(lists.value, { ...fields, cards: existing?.cards ?? [] })
  }

  function dropList(listId: string) {
    lists.value = lists.value.filter((list) => list.id !== listId)
  }

  /** Places a tile by `listId` + `rank`. Returns false when its list isn't on screen (caller reloads). */
  function putCard(card: CardSummary): boolean {
    let found = false
    lists.value = lists.value.map((list) => {
      if (list.id === card.listId) {
        found = true
        return { ...list, cards: upsertRanked(list.cards, card) }
      }
      return list.cards.some((c) => c.id === card.id) ? { ...list, cards: list.cards.filter((c) => c.id !== card.id) } : list
    })
    return found
  }

  function dropCard(cardId: string) {
    lists.value = lists.value.map((list) =>
      list.cards.some((c) => c.id === cardId) ? { ...list, cards: list.cards.filter((c) => c.id !== cardId) } : list,
    )
  }

  /** Keeps the tile in step with the card dialog's copy (archived cards leave the board). */
  function syncCard(card: Card) {
    if (card.boardId !== id.value) return
    if (card.isArchived) dropCard(card.id)
    else if (!putCard(toSummary(card))) void reload()
  }

  /** Patches a tile in place (badge counts after checklist/comment changes) without moving it. */
  function patchCard(cardId: string, patch: (card: CardSummary) => Partial<CardSummary>) {
    const found = findCard(cardId)
    if (!found) return
    const next = { ...found.card, ...patch(found.card) }
    lists.value = lists.value.map((list) =>
      list.id === found.list.id ? { ...list, cards: list.cards.map((c) => (c.id === cardId ? next : c)) } : list,
    )
  }

  function putLabel(label: Label) {
    const index = labels.value.findIndex((l) => l.id === label.id)
    labels.value = index >= 0 ? labels.value.map((l) => (l.id === label.id ? label : l)) : [...labels.value, label]
  }

  function dropLabel(labelId: string) {
    labels.value = labels.value.filter((l) => l.id !== labelId)
    lists.value = lists.value.map((list) =>
      list.cards.some((c) => c.labelIds.includes(labelId))
        ? { ...list, cards: list.cards.map((c) => (c.labelIds.includes(labelId) ? { ...c, labelIds: c.labelIds.filter((x) => x !== labelId) } : c)) }
        : list,
    )
  }

  function bump(cardId: string) {
    cardRevisions.value = { ...cardRevisions.value, [cardId]: (cardRevisions.value[cardId] ?? 0) + 1 }
  }

  // ---------- real-time ----------

  /**
   * Remote events wait while a drag or a move request is in flight: applying them mid-drag would re-render
   * the lists under the pointer, and a tile we just moved still carries its old rank until the server answers.
   */
  let holds = 0
  const queued: BoardEvent[] = []

  function hold(): () => void {
    holds++
    let released = false
    return () => {
      if (released) return
      released = true
      holds--
      if (holds === 0) {
        for (const event of queued.splice(0)) applyEvent(event)
      }
    }
  }

  function receive(event: BoardEvent) {
    if (event.boardId !== id.value) return
    if (holds > 0) queued.push(event)
    else applyEvent(event)
  }

  function applyEvent(event: BoardEvent) {
    switch (event.type) {
      case 'board.updated':
        board.value = event.payload
        if (event.payload.isArchived) closed.value = 'archived'
        break
      case 'board.deleted':
        closed.value = 'deleted'
        break
      case 'list.upserted':
        // An unknown, open list may be a restored one that comes back with its cards: only a reload has those.
        if (!event.payload.isArchived && !lists.value.some((l) => l.id === event.payload.id)) {
          void reload()
        } else {
          putList(event.payload)
        }
        break
      case 'list.removed':
        dropList(event.payload.id)
        break
      case 'card.upserted':
        if (!putCard(event.payload)) void reload()
        bump(event.payload.id)
        break
      case 'card.removed':
        dropCard(event.payload.id)
        bump(event.payload.id)
        break
      case 'label.upserted':
        putLabel(event.payload)
        break
      case 'label.removed':
        dropLabel(event.payload.id)
        break
    }
  }

  // ---------- requests ----------

  const boardId = () => {
    if (!id.value) throw new Error('No board loaded')
    return id.value
  }

  async function updateBoard(patch: { title?: string; description?: string | null; color?: PaletteColor; isArchived?: boolean }) {
    board.value = await $api.patch<BoardSummary>(`/api/boards/${boardId()}`, patch)
    return board.value
  }

  async function deleteBoard() {
    await $api.delete(`/api/boards/${boardId()}`)
  }

  async function createList(title: string) {
    const list = await $api.post<BoardList>(`/api/boards/${boardId()}/lists`, { title })
    putList(list)
    return list
  }

  async function updateList(listId: string, patch: { title?: string; isArchived?: boolean }) {
    putList(await $api.patch<BoardList>(`/api/lists/${listId}`, patch))
  }

  /** Un-archives a list. The response has no cards, so the board reloads to bring them back with it. */
  async function restoreList(listId: string) {
    await $api.patch<BoardList>(`/api/lists/${listId}`, { isArchived: false })
    await reload()
  }

  async function deleteList(listId: string) {
    await $api.delete(`/api/lists/${listId}`)
    dropList(listId)
  }

  /** Optimistic: the column moves now; on failure the board reloads (and the error goes to the caller). */
  async function moveList(listId: string, toIndex: number) {
    const from = lists.value.findIndex((l) => l.id === listId)
    if (from < 0 || from === toIndex) return
    const release = hold()
    const next = [...lists.value]
    const [moved] = next.splice(from, 1)
    next.splice(Math.min(toIndex, next.length), 0, moved!)
    lists.value = next
    try {
      putList(await $api.post<BoardList>(`/api/lists/${listId}/move`, neighbours(next, next.indexOf(moved!))))
    } catch (e) {
      await reload()
      throw e
    } finally {
      release()
    }
  }

  async function createCard(listId: string, title: string, position: 'top' | 'bottom' = 'bottom') {
    const card = await $api.post<CardSummary>(`/api/lists/${listId}/cards`, { title, position })
    putCard(card)
    return card
  }

  /** Optimistic like `moveList`. `toIndex` is the position in the target list once the card is in it. */
  async function moveCard(cardId: string, toListId: string, toIndex: number) {
    const found = findCard(cardId)
    const target = lists.value.find((l) => l.id === toListId)
    if (!found || !target) return
    if (found.list.id === toListId && found.index === toIndex) return

    const release = hold()
    const moved: CardSummary = { ...found.card, listId: toListId }
    const targetCards = target.cards.filter((c) => c.id !== cardId)
    const index = Math.max(0, Math.min(toIndex, targetCards.length))
    targetCards.splice(index, 0, moved)
    lists.value = lists.value.map((list) => {
      if (list.id === toListId) return { ...list, cards: targetCards }
      if (list.id === found.list.id) return { ...list, cards: list.cards.filter((c) => c.id !== cardId) }
      return list
    })

    try {
      const updated = await $api.post<CardSummary>(`/api/cards/${cardId}/move`, { listId: toListId, ...neighbours(targetCards, index) })
      putCard(updated)
      return updated
    } catch (e) {
      await reload()
      throw e
    } finally {
      release()
    }
  }

  async function createLabel(name: string | null, color: PaletteColor) {
    const label = await $api.post<Label>(`/api/boards/${boardId()}/labels`, { name, color })
    putLabel(label)
    return label
  }

  async function updateLabel(labelId: string, patch: { name?: string | null; color?: PaletteColor }) {
    putLabel(await $api.patch<Label>(`/api/labels/${labelId}`, patch))
  }

  async function deleteLabel(labelId: string) {
    await $api.delete(`/api/labels/${labelId}`)
    dropLabel(labelId)
  }

  return {
    id,
    board,
    myRole,
    lists,
    labels,
    members,
    error,
    closed,
    cardRevisions,
    labelById,
    memberById,
    allows,
    findCard,
    load,
    reload,
    reset,
    hold,
    receive,
    putCard,
    dropCard,
    syncCard,
    patchCard,
    updateBoard,
    deleteBoard,
    createList,
    updateList,
    restoreList,
    deleteList,
    moveList,
    createCard,
    moveCard,
    createLabel,
    updateLabel,
    deleteLabel,
  }
})
