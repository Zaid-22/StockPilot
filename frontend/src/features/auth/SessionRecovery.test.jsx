import { afterEach, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient } from '@tanstack/react-query'
import App from '../../App'

const json = (value, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks() })

function sessionFixture() {
  let client
  const mount = QueryClient.prototype.mount
  vi.spyOn(QueryClient.prototype, 'mount').mockImplementation(function () {
    client = this
    return mount.call(this)
  })
  const state = { sessionStatus: 200, records: [], requests: [] }
  const receipts = new Map()
  vi.stubGlobal('fetch', vi.fn(async (url, options = {}) => {
    if (url === '/api/auth/me') {
      if (state.sessionStatus === 0) throw new TypeError('offline')
      return json({ id: '1', email: 'staff@example.test', roles: ['Staff'] }, state.sessionStatus)
    }
    if (url === '/api/auth/csrf') return json({ token: 'csrf' })
    if (url === '/api/catalog/suppliers' && options.method === 'POST') {
      const input = JSON.parse(options.body)
      state.requests.push(input)
      if (receipts.has(input.requestId)) return json(receipts.get(input.requestId), 201)
      const saved = { ...input, id: 'supplier-1', version: 'v1' }
      receipts.set(input.requestId, saved)
      state.records.push(saved)
      // The server committed, but the response body was lost.
      return new Response('{', { status: 201, headers: { 'Content-Type': 'application/json' } })
    }
    if (url.startsWith('/api/catalog/suppliers?')) return json({ items: state.records, totalCount: state.records.length })
    throw new Error(`Unexpected request: ${url}`)
  }))
  window.history.replaceState(null, '', '/catalog/suppliers')
  render(<App />)
  return { state, client: () => client }
}

it.each([0, 503])('keeps the original creation request through a background session failure (%s)', async status => {
  const { state, client } = sessionFixture()
  const user = userEvent.setup()
  await user.click(await screen.findByRole('button', { name: 'Add supplier' }))
  const dialog = within(screen.getByRole('dialog'))
  await user.type(dialog.getByRole('textbox', { name: /^Name/ }), 'Example supplier')
  await user.click(dialog.getByRole('button', { name: 'Save' }))
  await dialog.findByRole('button', { name: 'Retry creation' })
  state.sessionStatus = status
  await act(async () => { await client().refetchQueries({ queryKey: ['session'] }); await new Promise(resolve => setTimeout(resolve, 0)) })
  expect(screen.getByRole('dialog')).toBeInTheDocument()
  expect(dialog.getByRole('textbox', { name: /^Name/ })).toHaveValue('Example supplier')
  expect(dialog.getByRole('textbox', { name: /^Name/ })).toBeDisabled()
  state.sessionStatus = 200
  await act(async () => { await client().refetchQueries({ queryKey: ['session'] }); await new Promise(resolve => setTimeout(resolve, 0)) })
  await user.click(dialog.getByRole('button', { name: 'Retry creation' }))
  await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  expect(state.requests).toHaveLength(2)
  expect(state.requests[1]).toEqual(state.requests[0])
  expect(state.records).toHaveLength(1)
})

it('still removes private views and cached data when the background check returns 401', async () => {
  const { state, client } = sessionFixture()
  const user = userEvent.setup()
  await user.click(await screen.findByRole('button', { name: 'Add supplier' }))
  state.sessionStatus = 401
  await act(async () => { await client().refetchQueries({ queryKey: ['session'] }); await new Promise(resolve => setTimeout(resolve, 0)) })
  expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  expect(client().getQueryData(['session'])).toBeNull()
  expect(client().getQueriesData({ queryKey: ['catalog'] })).toEqual([])
})
