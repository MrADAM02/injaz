#!/usr/bin/env node
/**
 * Starts Vitest with a normalized working directory. On Windows, a cwd with a lowercase drive letter
 * (`c:\…`, which VS Code terminals and some shells report) makes Vitest load its runner twice and every
 * suite fails with "Vitest failed to find the current suite". Upper-casing the drive letter avoids it.
 */
import { spawnSync } from 'node:child_process'
import { join } from 'node:path'

let cwd = process.cwd()
if (process.platform === 'win32' && /^[a-z]:/.test(cwd)) cwd = cwd[0].toUpperCase() + cwd.slice(1)

// Resolve Vitest from the normalized path too, so every module is loaded under one spelling.
const vitest = join(cwd, 'node_modules', 'vitest', 'vitest.mjs')
const result = spawnSync(process.execPath, [vitest, ...process.argv.slice(2)], { cwd, stdio: 'inherit' })
process.exit(result.status ?? 1)
