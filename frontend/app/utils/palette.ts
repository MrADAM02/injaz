import type { PaletteColor } from '~/types/api'

/** The backend's named palette (Domain/Boards/Palette.cs). Full class strings so Tailwind can see them. */
export const paletteColors: readonly PaletteColor[] = ['blue', 'sky', 'green', 'lime', 'yellow', 'orange', 'red', 'pink', 'purple', 'gray']

export const defaultBoardColor: PaletteColor = 'blue'

/** Solid fills with white text that pass contrast in light and dark themes. */
const fill: Record<PaletteColor, string> = {
  green: 'bg-green-700',
  yellow: 'bg-yellow-700',
  orange: 'bg-orange-700',
  red: 'bg-red-700',
  purple: 'bg-purple-700',
  blue: 'bg-blue-700',
  sky: 'bg-sky-700',
  lime: 'bg-lime-700',
  pink: 'bg-pink-700',
  gray: 'bg-slate-600',
}

export function paletteFill(color: PaletteColor | string | null | undefined): string {
  return fill[(color ?? defaultBoardColor) as PaletteColor] ?? fill[defaultBoardColor]
}
