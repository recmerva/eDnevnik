const apiBase = import.meta.env.VITE_API_URL || 'http://localhost:5080'
const safeMethods = ['GET', 'HEAD', 'OPTIONS']

// CSRF token je vezan za prijavljenog korisnika, pa se posle prijave/odjave mora uzeti novi.
let csrfToken: string | null = null
export function resetCsrfToken() { csrfToken = null }

// Kada sesija istekne (401), aplikacija se vraća na ekran za prijavu.
let unauthorizedHandler: (() => void) | null = null
export function onUnauthorized(handler: (() => void) | null) { unauthorizedHandler = handler }

export class ApiError extends Error {
  readonly status: number
  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

async function getCsrfToken() {
  if (csrfToken) return csrfToken
  const response = await fetch(`${apiBase}/api/auth/csrf`, { credentials: 'include' }).catch(() => null)
  if (!response?.ok) throw new ApiError('Ne mogu da uspostavim bezbednu vezu sa serverom.', response?.status ?? 0)
  csrfToken = ((await response.json()) as { requestToken: string }).requestToken
  return csrfToken
}

export async function api<T>(path: string, options: { method?: string; body?: unknown } = {}, retry = true): Promise<T> {
  const method = (options.method ?? 'GET').toUpperCase()
  const headers = new Headers()
  if (options.body !== undefined) headers.set('Content-Type', 'application/json')
  if (!safeMethods.includes(method)) headers.set('X-CSRF-TOKEN', await getCsrfToken())

  let response: Response
  try {
    response = await fetch(`${apiBase}${path}`, {
      method,
      headers,
      credentials: 'include',
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    })
  } catch {
    throw new ApiError('Server nije dostupan. Proverite internet vezu ili pokušajte kasnije.', 0)
  }

  if (!response.ok) {
    const result = await response.json().catch(() => null) as { message?: string; code?: string } | null
    if (result?.code === 'csrf' && retry) {
      resetCsrfToken()
      return api<T>(path, options, false)
    }
    if (response.status === 401 && path !== '/api/auth/login') unauthorizedHandler?.()
    const fallback = response.status === 403
      ? 'Nemate pravo pristupa ovim podacima.'
      : response.status === 401 ? 'Sesija je istekla. Prijavite se ponovo.' : 'Zahtev nije uspeo. Pokušajte ponovo.'
    throw new ApiError(result?.message ?? fallback, response.status)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export const errorMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback
