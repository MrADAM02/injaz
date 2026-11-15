import Sortable from 'sortablejs'

export interface SortableDrop {
  /** `data-sortable-id` of the dragged element. */
  id: string
  /** `data-sortable-container` of the source and target containers (e.g. list ids). */
  fromContainer: string
  toContainer: string
  /** Position among the target's draggable children once dropped. */
  newIndex: number
}

export interface SortableOptions {
  /** Containers with the same group exchange items (cards between lists). */
  group: string
  /** Only this part starts a drag (e.g. a column header); default: the whole item. */
  handle?: string
  disabled?: MaybeRefOrGetter<boolean>
  onStart?: () => void
  /** Called after every drag, dropped or not (before `onDrop`). */
  onEnd?: () => void
  onDrop: (drop: SortableDrop) => void
}

/**
 * SortableJS on a container whose children Vue renders (`data-sortable-id` on each). Sortable moves DOM nodes
 * itself; on drop we put the node back where Vue rendered it and let the store update re-render the new
 * order, so Vue's virtual DOM and the real one never disagree.
 *
 * Touch: a long press (200 ms) starts a drag and a normal swipe still scrolls. The fallback (non-native) drag
 * is used everywhere so mouse, touch and pen behave the same and edge auto-scroll works.
 */
export function useSortable(container: Readonly<Ref<HTMLElement | null>>, options: SortableOptions) {
  let sortable: Sortable | null = null

  onMounted(() => {
    const el = container.value
    if (!el) return
    sortable = Sortable.create(el, {
      group: options.group,
      handle: options.handle,
      draggable: '[data-sortable-id]',
      disabled: toValue(options.disabled) ?? false,
      animation: 150,
      delay: 200,
      delayOnTouchOnly: true,
      touchStartThreshold: 6,
      forceFallback: true,
      fallbackOnBody: true,
      fallbackTolerance: 4,
      ghostClass: 'sortable-ghost',
      chosenClass: 'sortable-chosen',
      dragClass: 'sortable-drag',
      scroll: true,
      bubbleScroll: true,
      scrollSensitivity: 72,
      scrollSpeed: 14,
      emptyInsertThreshold: 24,
      onStart: () => options.onStart?.(),
      onEnd: (event) => {
        const { item, from, to, oldIndex, newDraggableIndex } = event
        const moved = from !== to || event.oldIndex !== event.newIndex
        if (moved && oldIndex !== undefined) {
          item.remove()
          from.insertBefore(item, from.children[oldIndex] ?? null)
        }
        options.onEnd?.()
        if (!moved || newDraggableIndex === undefined) return
        const id = item.dataset.sortableId
        const fromContainer = from.dataset.sortableContainer
        const toContainer = to.dataset.sortableContainer
        if (id && fromContainer && toContainer) options.onDrop({ id, fromContainer, toContainer, newIndex: newDraggableIndex })
      },
    })
  })

  watch(
    () => toValue(options.disabled) ?? false,
    (disabled) => sortable?.option('disabled', disabled),
  )

  onBeforeUnmount(() => {
    sortable?.destroy()
    sortable = null
  })
}
