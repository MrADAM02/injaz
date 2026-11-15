import type { IconName } from '~/utils/icons'

/** One entry of a <UiMenu>. Falsy entries in a menu's list are skipped (handy for permission checks). */
export interface MenuItem {
  label: string
  icon?: IconName
  danger?: boolean
  disabled?: boolean
  action: () => unknown
}

export type MenuEntry = MenuItem | false | null | undefined
