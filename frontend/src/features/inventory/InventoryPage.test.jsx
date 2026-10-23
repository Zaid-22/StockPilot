import { afterEach, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from '../../App'

const row = { productId: 'product', sku: 'K-1', name: 'Keyboard', isActive: true, quantityOnHand: 2, reorderLevel: 3, version: 'version' }
const json = (body, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
function server(role = 'Staff', uncertain = false) {
  let writes = 0
  const fetch = vi.fn(async (url, options = {}) => {
    if (url === '/api/auth/me') return json({ email: 'staff@test', roles: [role] })
    if (url === '/api/auth/csrf') return json({ token: 'csrf' })
    if (url.startsWith('/api/inventory/balances')) return json({ items: [row], totalCount: 1 })
    if (url === '/api/inventory/adjustments') {
      writes++
      if (uncertain && writes === 1) throw new Error('Lost response')
      return json({ quantityOnHand: 7, version: 'new' })
    }
    throw new Error(`Unexpected request ${url} ${options.method}`)
  })
  vi.stubGlobal('fetch', fetch)
  window.history.replaceState(null, '', '/inventory')
  return fetch
}
afterEach(() => vi.unstubAllGlobals())

it('posts a signed adjustment with the balance version and a reason', async () => {
  const fetch = server(); const user = userEvent.setup(); render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Adjust' }))
  const dialog = within(screen.getByRole('dialog'))
  await user.type(dialog.getByLabelText(/Quantity change/), '5')
  await user.type(dialog.getByLabelText(/Reason/), 'Shelf count')
  await user.click(dialog.getByRole('button', { name: 'Post adjustment' }))
  const call = fetch.mock.calls.find(([url]) => url === '/api/inventory/adjustments')
  expect(JSON.parse(call[1].body)).toMatchObject({ productId: 'product', version: 'version', quantityChange: 5, reason: 'Shelf count' })
  expect(JSON.parse(call[1].body).requestId).toBeTruthy()
})

it('reuses the same request ID after a lost response and locks uncertain input', async () => {
  const fetch = server('Staff', true); const user = userEvent.setup(); render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Adjust' }))
  const dialog = within(screen.getByRole('dialog'))
  await user.type(dialog.getByLabelText(/Quantity change/), '5')
  await user.type(dialog.getByLabelText(/Reason/), 'Shelf count')
  await user.click(dialog.getByRole('button', { name: 'Post adjustment' }))
  expect(await dialog.findByRole('alert')).toHaveTextContent('Result unknown')
  expect(dialog.getByLabelText(/Quantity change/)).toBeDisabled()
  await user.click(dialog.getByRole('button', { name: 'Retry adjustment' }))
  const requests = fetch.mock.calls.filter(([url]) => url === '/api/inventory/adjustments')
  expect(requests).toHaveLength(2)
  expect(requests[0][1].body).toBe(requests[1][1].body)
})

it('keeps inventory read-only for viewers', async () => {
  server('Viewer'); render(<App />)
  expect(await screen.findByText('Keyboard')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Adjust' })).not.toBeInTheDocument()
})
