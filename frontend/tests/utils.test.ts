import { describe, expect, it, vi } from 'vitest'
import type { Member } from '~/types/api'
import { ApiError } from '~/utils/api-client'
import { pluralIndex } from '~/utils/plural'
import { fieldMessages, problemMessage, type Translator } from '~/utils/problem-messages'
import { safeRedirect } from '~/utils/redirect'
import { can, canManageMember } from '~/utils/roles'
import { singleFlight } from '~/utils/single-flight'
import { email, httpUrl, maxLength, minLength, required, validateFields } from '~/utils/validate'

describe('singleFlight', () => {
  it('shares one run between concurrent callers and allows a new run afterwards', async () => {
    let release!: (value: number) => void
    const operation = vi.fn(() => new Promise<number>((resolve) => (release = resolve)))
    const run = singleFlight(operation)

    const a = run()
    const b = run()
    release(1)

    await expect(Promise.all([a, b])).resolves.toEqual([1, 1])
    expect(operation).toHaveBeenCalledTimes(1)

    const c = run()
    release(2)
    await expect(c).resolves.toBe(2)
    expect(operation).toHaveBeenCalledTimes(2)
  })

  it('clears the in-flight run after a failure', async () => {
    const operation = vi.fn().mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce('ok')
    const run = singleFlight(operation)

    await expect(run()).rejects.toThrow('offline')
    await expect(run()).resolves.toBe('ok')
  })
})

describe('safeRedirect', () => {
  it.each([
    ['/w/123?tab=members', '/w/123?tab=members'],
    ['/invite/abc', '/invite/abc'],
    [['/profile', '/other'], '/profile'],
  ])('keeps in-app path %j', (input, expected) => {
    expect(safeRedirect(input)).toBe(expected)
  })

  it.each(['https://evil.example', '//evil.example', '/\\evil.example', 'javascript:alert(1)', '', undefined, 42])(
    'rejects %j',
    (input) => {
      expect(safeRedirect(input)).toBe('/')
    },
  )
})

describe('roles', () => {
  it('mirrors the backend permission matrix', () => {
    expect(can('viewer', 'view')).toBe(true)
    expect(can('viewer', 'editContent')).toBe(false)
    expect(can('member', 'editContent')).toBe(true)
    expect(can('member', 'manageMembers')).toBe(false)
    expect(can('admin', 'manageMembers')).toBe(true)
    expect(can('admin', 'ownWorkspace')).toBe(false)
    expect(can('owner', 'ownWorkspace')).toBe(true)
    expect(can(undefined, 'view')).toBe(false)
  })

  const member = (userId: string, role: Member['role']): Member => ({
    userId,
    role,
    displayName: userId,
    email: `${userId}@test`,
    avatarUrl: null,
    joinedAt: '2026-01-01T00:00:00Z',
  })

  it('lets admins manage others but never the owner or themselves', () => {
    expect(canManageMember('admin', 'me', member('other', 'member'))).toBe(true)
    expect(canManageMember('admin', 'me', member('other', 'admin'))).toBe(true)
    expect(canManageMember('admin', 'me', member('boss', 'owner'))).toBe(false)
    expect(canManageMember('admin', 'me', member('me', 'admin'))).toBe(false)
    expect(canManageMember('member', 'me', member('other', 'viewer'))).toBe(false)
  })
})

describe('pluralIndex', () => {
  it('maps Arabic counts to the six CLDR forms', () => {
    expect([0, 1, 2, 3, 11, 100].map((n) => pluralIndex('ar', n, 6))).toEqual([0, 1, 2, 3, 4, 5])
  })

  it('falls back to the last form when a message has fewer', () => {
    expect(pluralIndex('ar', 11, 3)).toBe(2)
  })
})

describe('validate', () => {
  it('returns the first failing rule per field, with the API codes', () => {
    expect(
      validateFields({
        name: ['', required, maxLength(5)],
        title: ['too long', required, maxLength(5)],
        password: ['short', required, minLength(8)],
        email: ['nope', email],
        avatarUrl: ['ftp://x', httpUrl],
        ok: ['https://example.com/a.png', httpUrl],
      }),
    ).toEqual({
      name: [{ code: 'required' }],
      title: [{ code: 'maxLength', params: { max: 5 } }],
      password: [{ code: 'minLength', params: { min: 8 } }],
      email: [{ code: 'email' }],
      avatarUrl: [{ code: 'url' }],
    })
  })
})

describe('problem messages', () => {
  const messages: Record<string, string> = {
    'errors.generic': 'generic',
    'errors.server.error': 'server',
    'errors.auth.invalidCredentials': 'bad credentials',
    'validation.maxLength': 'max {max}',
    'validation.invalidValue': 'invalid',
  }
  const i18n: Translator = {
    te: (key) => key in messages,
    t: (key, params) => (messages[key] ?? key).replace(/\{(\w+)\}/g, (_, name: string) => String(params?.[name])),
  }

  it('translates the ProblemDetails code', () => {
    expect(problemMessage(new ApiError(401, { code: 'auth.invalidCredentials' }), i18n)).toBe('bad credentials')
  })

  it('falls back for unknown codes and non-API errors', () => {
    expect(problemMessage(new ApiError(503, { code: 'something.new' }), i18n)).toBe('server')
    expect(problemMessage(new ApiError(422, { code: 'something.new' }), i18n)).toBe('generic')
    expect(problemMessage(new Error('boom'), i18n)).toBe('generic')
  })

  it('maps field errors to messages with their params', () => {
    const error = new ApiError(400, {
      code: 'validation.failed',
      errors: { name: [{ code: 'maxLength', params: { max: 100 } }], role: [{ code: 'brandNew' }] },
    })
    expect(fieldMessages(error, i18n)).toEqual({ name: 'max 100', role: 'invalid' })
  })
})
