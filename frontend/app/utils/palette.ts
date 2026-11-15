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

/** Label chips: solid swatches (tile stripes, pickers) and readable soft chips (with the name). */
const swatch: Record<PaletteColor, string> = {
  green: 'bg-green-600',
  yellow: 'bg-yellow-500',
  orange: 'bg-orange-500',
  red: 'bg-red-600',
  purple: 'bg-purple-600',
  blue: 'bg-blue-600',
  sky: 'bg-sky-500',
  lime: 'bg-lime-500',
  pink: 'bg-pink-500',
  gray: 'bg-slate-500',
}

const chip: Record<PaletteColor, string> = {
  green: 'bg-green-100 text-green-900 dark:bg-green-950 dark:text-green-200',
  yellow: 'bg-yellow-100 text-yellow-900 dark:bg-yellow-950 dark:text-yellow-200',
  orange: 'bg-orange-100 text-orange-900 dark:bg-orange-950 dark:text-orange-200',
  red: 'bg-red-100 text-red-900 dark:bg-red-950 dark:text-red-200',
  purple: 'bg-purple-100 text-purple-900 dark:bg-purple-950 dark:text-purple-200',
  blue: 'bg-blue-100 text-blue-900 dark:bg-blue-950 dark:text-blue-200',
  sky: 'bg-sky-100 text-sky-900 dark:bg-sky-950 dark:text-sky-200',
  lime: 'bg-lime-100 text-lime-900 dark:bg-lime-950 dark:text-lime-200',
  pink: 'bg-pink-100 text-pink-900 dark:bg-pink-950 dark:text-pink-200',
  gray: 'bg-slate-200 text-slate-800 dark:bg-slate-800 dark:text-slate-200',
}

export function paletteSwatch(color: PaletteColor | string | null | undefined): string {
  return swatch[(color ?? 'gray') as PaletteColor] ?? swatch.gray
}

export function paletteChip(color: PaletteColor | string | null | undefined): string {
  return chip[(color ?? 'gray') as PaletteColor] ?? chip.gray
}
