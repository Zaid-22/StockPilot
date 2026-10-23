import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiMutation, apiRequest } from './api'

const json = (data, status = 200) => new Response(JSON.stringify(data), {
  status, headers: { 'Content-Type': 'application/json' },
})
afterEach(() => vi.unstubAllGlobals())

describe('API requests', () => {
  it('sends same-origin credentials and returns JSON', async () => {
    const fetch = vi.fn().mockResolvedValue(json({ id: 'user-1' }))
    vi.stubGlobal('fetch', fetch)
    expect(await apiRequest('/auth/me')).toEqual({ id: 'user-1' })
    expect(fetch).toHaveBeenCalledWith('/api/auth/me', expect.objectContaining({ credentials: 'same-origin' }))
  })
  it('fetches a fresh CSRF token before each mutation', async () => {
    const fetch = vi.fn()
      .mockResolvedValueOnce(json({ token: 'anonymous-token' }))
      .mockResolvedValueOnce(json({ id: 'user-1' }))
      .mockResolvedValueOnce(json({ token: 'signed-in-token' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetch)
    await apiMutation('/auth/login', { email: 'admin@example.test', password: 'example' })
    expect(await apiMutation('/auth/logout')).toBeNull()
    expect(fetch.mock.calls[1][1].headers['X-CSRF-TOKEN']).toBe('anonymous-token')
    expect(fetch.mock.calls[3][1].headers['X-CSRF-TOKEN']).toBe('signed-in-token')
  })
  it('does not submit a mutation if the CSRF request fails', async () => {
    const fetch = vi.fn().mockRejectedValue(new TypeError('Network error'))
    vi.stubGlobal('fetch', fetch)
    await expect(apiMutation('/auth/logout')).rejects.toMatchObject({ status: 0 })
    expect(fetch).toHaveBeenCalledTimes(1)
  })
  it('announces an expired session on a protected API response', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json({ title: 'Authentication required' }, 401)))
    const listener = vi.fn()
    window.addEventListener('stockpilot:session-expired', listener)
    try {
      await expect(apiRequest('/admin/users')).rejects.toMatchObject({ status: 401 })
      expect(listener).toHaveBeenCalledTimes(1)
    } finally { window.removeEventListener('stockpilot:session-expired', listener) }
  })
  it('preserves server validation messages', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json({ title: 'Invalid request', errors: { Email: ['Required'] } }, 400)))
    await expect(apiRequest('/auth/login')).rejects.toMatchObject({ status: 400, errors: { Email: ['Required'] } })
  })
  it('gives a usable message for rate limiting without a JSON body', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 429 })))
    await expect(apiRequest('/auth/login')).rejects.toThrow('Too many sign-in attempts')
  })
})
