import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from './App'

const admin = { id: '1', email: 'admin@example.test', displayName: 'Test Admin', roles: ['Admin'] }
const json = (data, status = 200) => new Response(JSON.stringify(data), {
  status, headers: { 'Content-Type': 'application/json' },
})

function server(initialUser = null, loginStatus = 200) {
  let user = initialUser
  const fetch = vi.fn(async (url, options = {}) => {
    if (url === '/api/auth/me') return json(user ?? {}, user ? 200 : 401)
    if (url === '/api/auth/csrf') return json({ token: 'test-csrf' })
    if (url === '/api/auth/login') {
      if (loginStatus !== 200) return json({ detail: 'Check your email and password.' }, loginStatus)
      user = admin
      return json(user)
    }
    if (url === '/api/auth/logout' && options.method === 'POST') {
      user = null
      return new Response(null, { status: 204 })
    }
    if (url === '/api/reports/summary') return json({ activeProducts: 0, unitsOnHand: 0, lowStockProducts: 0, openPurchaseOrders: 0, openSalesOrders: 0, lowStock: [], generatedAtUtc: '2026-10-23T10:00:00Z' })
    if (url === '/api/admin/users') return json([admin])
    throw new Error(`Unexpected request: ${url}`)
  })
  vi.stubGlobal('fetch', fetch)
  return fetch
}

beforeEach(() => window.history.replaceState(null, '', '/'))
afterEach(() => vi.unstubAllGlobals())

it('restores a signed-in session without showing login', async () => {
  server(admin)
  render(<App />)
  expect(await screen.findByRole('button', { name: 'Sign out' })).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Sign in' })).not.toBeInTheDocument()
})

it('signs in, opens the protected directory, and signs out', async () => {
  const fetch = server()
  const user = userEvent.setup()
  render(<App />)
  await user.type(await screen.findByLabelText('Email address'), admin.email)
  await user.type(screen.getByLabelText('Password'), 'Example-password-123!')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
  await user.click(await screen.findByRole('link', { name: 'User access' }))
  expect(await screen.findByRole('table', { name: 'Workspace users' })).toHaveTextContent(admin.email)
  await user.click(screen.getByRole('button', { name: 'Sign out' }))
  expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
  expect(fetch.mock.calls.some(([url]) => url === '/api/auth/logout')).toBe(true)
})

it('blocks a viewer who opens the admin URL directly', async () => {
  window.history.replaceState(null, '', '/access')
  const fetch = server({ ...admin, roles: ['Viewer'] })
  render(<App />)
  expect(await screen.findByRole('heading', { name: "You don't have access to this page." })).toBeInTheDocument()
  expect(fetch.mock.calls.some(([url]) => url === '/api/admin/users')).toBe(false)
})

it('shows failed login without leaving the form', async () => {
  server(null, 401)
  const user = userEvent.setup()
  render(<App />)
  await user.type(await screen.findByLabelText('Email address'), admin.email)
  await user.type(screen.getByLabelText('Password'), 'wrong-password')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Check your email and password.')
  expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled()
})

it('validates fields before making a login request', async () => {
  const fetch = server()
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Sign in' }))
  expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument()
  expect(fetch.mock.calls.some(([url]) => url === '/api/auth/login')).toBe(false)
})

it('offers retry when restoring the session fails', async () => {
  const fetch = vi.fn().mockRejectedValueOnce(new TypeError('offline'))
    .mockResolvedValue(json({}, 401))
  vi.stubGlobal('fetch', fetch)
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Retry' }))
  await waitFor(() => expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument())
})
