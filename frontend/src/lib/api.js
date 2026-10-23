export class ApiError extends Error {
  constructor(message, status, errors = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
  }
}

export async function apiRequest(path, { method = 'GET', body, headers = {} } = {}) {
  let response
  try {
    response = await fetch(`/api${path}`, {
      method, credentials: 'same-origin',
      headers: { Accept: 'application/json', ...(body ? { 'Content-Type': 'application/json' } : {}), ...headers },
      ...(body ? { body: JSON.stringify(body) } : {}),
    })
  } catch {
    throw new ApiError('Unable to reach StockPilot. Check your connection and try again.', 0)
  }
  const data = response.headers.get('content-type')?.includes('json') ? await response.json() : null
  if (!response.ok) {
    if (response.status === 401 && !path.startsWith('/auth/')) window.dispatchEvent(new Event('stockpilot:session-expired'))
    const fallback = response.status === 429 ? 'Too many sign-in attempts. Please wait a minute.' : 'The request could not be completed. Please try again.'
    throw new ApiError(data?.detail || data?.title || fallback, response.status, data?.errors)
  }
  return data
}

// Fetch a token for the current identity before each write. An anonymous token
// becomes invalid after login; keeping it forever would break logout.
export async function apiMutation(path, body) {
  const { token } = await apiRequest('/auth/csrf')
  return apiRequest(path, { method: 'POST', body, headers: { 'X-CSRF-TOKEN': token } })
}
