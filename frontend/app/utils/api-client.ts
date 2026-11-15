import type { FieldError, ProblemDetails } from '~/types/api'

/** Every failed API call surfaces as this: the ProblemDetails `code` is what the UI translates. */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly fieldErrors: Record<string, FieldError[]>
  readonly traceId?: string

  constructor(status: number, problem: ProblemDetails = {}, options?: { cause?: unknown }) {
    super(problem.detail ?? problem.title ?? `Request failed with status ${status}`, options)
    this.name = 'ApiError'
    this.status = status
    this.code = problem.code ?? fallbackCode(status)
    this.fieldErrors = problem.errors ?? {}
    this.traceId = problem.traceId
  }

  /** No response at all (offline, DNS, CORS, server down). */
  static network(cause: unknown): ApiError {
    return new ApiError(0, { code: 'network.error' }, { cause })
  }

  get hasFieldErrors(): boolean {
    return Object.keys(this.fieldErrors).length > 0
  }
}

function fallbackCode(status: number): string {
  if (status === 401) return 'auth.unauthorized'
  if (status === 403) return 'auth.forbidden'
  if (status === 429) return 'request.rateLimited'
  if (status >= 500) return 'server.error'
  return 'request.invalid'
}

export function isApiError(error: unknown, code?: string): error is ApiError {
  return error instanceof ApiError && (code === undefined || error.code === code)
}

type QueryValue = string | number | boolean | null | undefined | readonly (string | number)[]

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  query?: Record<string, QueryValue>
  /** Attach the bearer token and recover from a 401 by refreshing once (default: true). */
  auth?: boolean
  signal?: AbortSignal
}

export interface ApiClientOptions {
  /** Origin of the API, e.g. `https://api.injaz.app`; empty for same origin. Paths start with `/api/`. */
  baseURL: string
  fetch?: typeof globalThis.fetch
  /** A currently valid access token (the caller may refresh first if it is about to expire), or null. */
  getAccessToken: () => Promise<string | null>
  /** Forces a refresh; resolves false when the session is gone. Must be single-flight. */
  refreshAccessToken: () => Promise<boolean>
  /** Called when a request is still unauthorized after a refresh attempt. */
  onSessionExpired: () => void
  /** Extra headers per request (e.g. `X-Connection-Id` for real-time echo suppression). */
  headers?: () => Record<string, string>
}

export type ApiClient = ReturnType<typeof createApiClient>

export function createApiClient(options: ApiClientOptions) {
  const doFetch = options.fetch ?? ((input, init) => globalThis.fetch(input, init))

  async function send(path: string, request: RequestOptions, token: string | null): Promise<Response> {
    const headers: Record<string, string> = {
      Accept: 'application/json',
      // Required by the cookie endpoints (CSRF guard); harmless elsewhere.
      'X-Requested-With': 'XMLHttpRequest',
      ...options.headers?.(),
    }
    if (token) headers.Authorization = `Bearer ${token}`

    let body: string | undefined
    if (request.body !== undefined) {
      headers['Content-Type'] = 'application/json'
      body = JSON.stringify(request.body)
    }

    try {
      return await doFetch(options.baseURL + path + toQueryString(request.query), {
        method: request.method ?? 'GET',
        headers,
        body,
        // Needed so the browser stores/sends the refresh cookie cross-origin. The cookie's Path (/api/auth)
        // keeps it off every other request.
        credentials: 'include',
        signal: request.signal,
      })
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') throw error
      throw ApiError.network(error)
    }
  }

  async function request<T = void>(path: string, request: RequestOptions = {}): Promise<T> {
    const auth = request.auth ?? true
    let response = await send(path, request, auth ? await options.getAccessToken() : null)

    if (auth && response.status === 401) {
      if (await options.refreshAccessToken()) {
        response = await send(path, request, await options.getAccessToken())
      }
      if (response.status === 401) options.onSessionExpired()
    }

    return read<T>(response)
  }

  return {
    request,
    get: <T>(path: string, query?: RequestOptions['query'], init?: Omit<RequestOptions, 'method' | 'query' | 'body'>) =>
      request<T>(path, { ...init, query }),
    post: <T = void>(path: string, body?: unknown, init?: Omit<RequestOptions, 'method' | 'body'>) =>
      request<T>(path, { ...init, method: 'POST', body }),
    put: <T = void>(path: string, body?: unknown, init?: Omit<RequestOptions, 'method' | 'body'>) =>
      request<T>(path, { ...init, method: 'PUT', body }),
    patch: <T = void>(path: string, body?: unknown, init?: Omit<RequestOptions, 'method' | 'body'>) =>
      request<T>(path, { ...init, method: 'PATCH', body }),
    delete: <T = void>(path: string, init?: Omit<RequestOptions, 'method' | 'body'>) =>
      request<T>(path, { ...init, method: 'DELETE' }),
  }
}

async function read<T>(response: Response): Promise<T> {
  const text = await response.text()
  const json: unknown = text.length > 0 && isJson(response) ? safeParse(text) : undefined

  if (!response.ok) {
    throw new ApiError(response.status, (json ?? {}) as ProblemDetails)
  }

  return json as T
}

function isJson(response: Response): boolean {
  const type = response.headers.get('content-type') ?? ''
  return type.includes('application/json') || type.includes('+json')
}

function safeParse(text: string): unknown {
  try {
    return JSON.parse(text)
  } catch {
    return undefined
  }
}

export function toQueryString(query: RequestOptions['query']): string {
  if (!query) return ''
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value === null || value === undefined || value === '') continue
    if (Array.isArray(value)) {
      for (const item of value) params.append(key, String(item))
    } else {
      params.append(key, String(value))
    }
  }
  const qs = params.toString()
  return qs ? `?${qs}` : ''
}
