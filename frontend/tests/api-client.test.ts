import { describe, expect, it, vi } from 'vitest'
import { ApiError, createApiClient, toQueryString } from '~/utils/api-client'

type Handler = (url: string, init: RequestInit) => Response | Promise<Response>

function json(status: number, body: unknown, type = 'application/json') {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': type } })
}

function setup(handler: Handler, overrides: Partial<Parameters<typeof createApiClient>[0]> = {}) {
  const calls: { url: string; init: RequestInit }[] = []
  const fetch = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    calls.push({ url: String(input), init: init ?? {} })
    return handler(String(input), init ?? {})
  })
  let token: string | null = 'token-1'
  const options = {
    baseURL: 'https://api.test',
    fetch: fetch as unknown as typeof globalThis.fetch,
    getAccessToken: vi.fn(async () => token),
    refreshAccessToken: vi.fn(async () => {
      token = 'token-2'
      return true
    }),
    onSessionExpired: vi.fn(),
    ...overrides,
  }
  return { api: createApiClient(options), calls, options }
}

const header = (init: RequestInit, name: string) => (init.headers as Record<string, string>)[name]

describe('createApiClient', () => {
  it('sends JSON with the bearer token, CSRF header and credentials', async () => {
    const { api, calls } = setup(() => json(201, { id: 'w1' }))

    const result = await api.post<{ id: string }>('/api/workspaces', { name: 'Team' })

    expect(result).toEqual({ id: 'w1' })
    const [{ url, init }] = calls
    expect(url).toBe('https://api.test/api/workspaces')
    expect(init.method).toBe('POST')
    expect(init.body).toBe('{"name":"Team"}')
    expect(init.credentials).toBe('include')
    expect(header(init, 'Authorization')).toBe('Bearer token-1')
    expect(header(init, 'Content-Type')).toBe('application/json')
    expect(header(init, 'X-Requested-With')).toBe('XMLHttpRequest')
  })

  it('returns undefined for 204 No Content', async () => {
    const { api } = setup(() => new Response(null, { status: 204 }))
    await expect(api.delete('/api/workspaces/1')).resolves.toBeUndefined()
  })

  it('skips the token for public requests', async () => {
    const { api, calls, options } = setup(() => json(200, {}))
    await api.get('/api/invitations/abc', undefined, { auth: false })
    expect(header(calls[0]!.init, 'Authorization')).toBeUndefined()
    expect(options.getAccessToken).not.toHaveBeenCalled()
  })

  it('turns ProblemDetails into an ApiError with code and field errors', async () => {
    const { api } = setup(() =>
      json(
        400,
        { status: 400, code: 'validation.failed', traceId: 't-1', errors: { name: [{ code: 'maxLength', params: { max: 100 } }] } },
        'application/problem+json',
      ),
    )

    const error = await api.post('/api/workspaces', { name: 'x' }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    const apiError = error as ApiError
    expect(apiError.status).toBe(400)
    expect(apiError.code).toBe('validation.failed')
    expect(apiError.traceId).toBe('t-1')
    expect(apiError.fieldErrors).toEqual({ name: [{ code: 'maxLength', params: { max: 100 } }] })
    expect(apiError.hasFieldErrors).toBe(true)
  })

  it('falls back to a status-based code when the body is not ProblemDetails', async () => {
    const { api } = setup(() => new Response('Bad gateway', { status: 502, headers: { 'content-type': 'text/html' } }))
    await expect(api.get('/api/me')).rejects.toMatchObject({ status: 502, code: 'server.error' })
  })

  it('reports network failures as status 0 / network.error', async () => {
    const { api } = setup(() => {
      throw new TypeError('Failed to fetch')
    })
    await expect(api.get('/api/me')).rejects.toMatchObject({ status: 0, code: 'network.error' })
  })

  it('refreshes once on 401 and retries with the new token', async () => {
    const { api, calls, options } = setup((_, init) =>
      header(init, 'Authorization') === 'Bearer token-2' ? json(200, { ok: true }) : json(401, { code: 'auth.unauthorized' }),
    )

    await expect(api.get('/api/me')).resolves.toEqual({ ok: true })

    expect(options.refreshAccessToken).toHaveBeenCalledTimes(1)
    expect(calls.map((c) => header(c.init, 'Authorization'))).toEqual(['Bearer token-1', 'Bearer token-2'])
    expect(options.onSessionExpired).not.toHaveBeenCalled()
  })

  it('ends the session when the refresh fails', async () => {
    const { api, calls, options } = setup(() => json(401, { code: 'auth.unauthorized' }), {
      refreshAccessToken: vi.fn(async () => false),
    })

    await expect(api.get('/api/me')).rejects.toMatchObject({ status: 401, code: 'auth.unauthorized' })

    expect(calls).toHaveLength(1)
    expect(options.onSessionExpired).toHaveBeenCalledTimes(1)
  })

  it('does not try to refresh for public requests', async () => {
    const { api, options } = setup(() => json(401, { code: 'auth.invalidCredentials' }))

    await expect(api.post('/api/auth/login', {}, { auth: false })).rejects.toMatchObject({ code: 'auth.invalidCredentials' })

    expect(options.refreshAccessToken).not.toHaveBeenCalled()
    expect(options.onSessionExpired).not.toHaveBeenCalled()
  })

  it('adds per-request headers such as the real-time connection id', async () => {
    const { api, calls } = setup(() => json(200, {}), { headers: () => ({ 'X-Connection-Id': 'c-1' }) })
    await api.get('/api/boards/1')
    expect(header(calls[0]!.init, 'X-Connection-Id')).toBe('c-1')
  })
})

describe('toQueryString', () => {
  it('skips empty values and repeats arrays', () => {
    expect(toQueryString({ q: 'design', labelIds: ['a', 'b'], cursor: undefined, archived: false, empty: '', none: null })).toBe(
      '?q=design&labelIds=a&labelIds=b&archived=false',
    )
    expect(toQueryString({})).toBe('')
    expect(toQueryString(undefined)).toBe('')
  })
})
