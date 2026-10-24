import { afterEach, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from '../../App'

const line = { id: 'line', productId: 'product', sku: 'KEY-1', productName: 'Keyboard', quantity: 10, processedQuantity: 0, unitPrice: 4.125, total: 41.25 }
const json = (data, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } })
function server({ role = 'Staff', status = 'Confirmed', lostResponse = false, truncatedCreation = false, kind = 'purchases' } = {}) {
  let order = { id: 'order', number: 'PO-123', kind: 'Purchase', partyName: 'Office supply', status, notes: '', createdAtUtc: '2026-10-23T10:00:00Z', version: 'v1', total: 41.25, lines: [line] }
  let attempts = 0
  const fetch = vi.fn(async (url, options = {}) => {
    if (url === '/api/auth/me') return json({ email: 'staff@test', roles: [role] })
    if (url === '/api/auth/csrf') return json({ token: 'csrf' })
    if (url.startsWith('/api/catalog/suppliers') || url.startsWith('/api/catalog/customers')) return json({ items: [{ id: 'party', name: 'Office supply' }] })
    if (url.startsWith('/api/catalog/products')) return json({ items: [{ id: 'product', sku: 'KEY-1', name: 'Keyboard', sellingPrice: 12.125 }] })
    if (options.method === 'POST' && url === `/api/orders/${kind}`) {
      attempts++
      if (truncatedCreation && attempts === 1) return new Response('{"id":', { status: 201, headers: { 'Content-Type': 'application/json' } })
      return json(order, 201)
    }
    if (options.method === 'POST' && url.startsWith(`/api/orders/${kind}/order/`)) {
      attempts++
      if (lostResponse && attempts === 1) throw new Error('Lost response')
      const body = JSON.parse(options.body)
      order = { ...order, status: url.endsWith('/confirm') ? 'Confirmed' : url.endsWith('/cancel') ? 'Cancelled' : 'Partial', version: 'v2', lines: [{ ...line, processedQuantity: body.lines?.[0]?.quantity || 0 }] }
      return json(order)
    }
    if (url === `/api/orders/${kind}/order`) return json(order)
    if (url.startsWith(`/api/orders/${kind}?`)) return json({ items: [order], totalCount: 1 })
    throw new Error(`Unexpected request ${url}`)
  })
  vi.stubGlobal('fetch', fetch)
  window.history.replaceState(null, '', kind === 'purchases' ? '/purchases' : '/sales')
  return fetch
}
afterEach(() => vi.unstubAllGlobals())

it('creates a purchase draft from selected supplier and product', async () => {
  const fetch = server(); const user = userEvent.setup(); render(<App />)
  await user.click(await screen.findByRole('button', { name: 'New order' }))
  const dialog = within(screen.getByRole('dialog'))
  await user.click(dialog.getByRole('combobox', { name: /^Supplier/ }))
  await user.click(await screen.findByRole('option', { name: 'Office supply' }))
  await user.click(dialog.getByRole('combobox', { name: /^Product 1/ }))
  await user.click(await screen.findByRole('option', { name: 'KEY-1 · Keyboard' }))
  await user.clear(dialog.getByLabelText(/^Unit price 1/)); await user.type(dialog.getByLabelText(/^Unit price 1/), '4.125')
  await user.click(dialog.getByRole('button', { name: 'Create draft' }))
  const request = fetch.mock.calls.find(([url, options]) => url === '/api/orders/purchases' && options.method === 'POST')
  expect(JSON.parse(request[1].body)).toMatchObject({ partyId: 'party', lines: [{ productId: 'product', quantity: 1, unitPrice: 4.125 }] })
})

it('retries a truncated creation response with the original request and locked fields', async () => {
  const fetch = server({ truncatedCreation: true }); const user = userEvent.setup(); render(<App />)
  await user.click(await screen.findByRole('button', { name: 'New order' }))
  const dialog = within(screen.getByRole('dialog'))
  await user.click(dialog.getByRole('combobox', { name: /^Supplier/ }))
  await user.click(await screen.findByRole('option', { name: 'Office supply' }))
  await user.click(dialog.getByRole('combobox', { name: /^Product 1/ }))
  await user.click(await screen.findByRole('option', { name: 'KEY-1 · Keyboard' }))
  await user.click(dialog.getByRole('button', { name: 'Create draft' }))
  expect(await dialog.findByRole('alert')).toHaveTextContent('Result unknown')
  expect(dialog.getByLabelText(/^Quantity 1/)).toBeDisabled()
  expect(dialog.getByRole('button', { name: 'Cancel' })).toBeDisabled()
  await user.click(dialog.getByRole('button', { name: 'Retry creation' }))
  expect(await screen.findByRole('table', { name: 'Order lines' })).toBeInTheDocument()
  const attempts = fetch.mock.calls.filter(([url, options]) => url === '/api/orders/purchases' && options.method === 'POST')
  expect(attempts).toHaveLength(2)
  expect(attempts[0][1].body).toBe(attempts[1][1].body)
})

it('receives part of an order using the current version and delivery reference', async () => {
  const fetch = server(); const user = userEvent.setup(); render(<App />)
  await user.click(await screen.findByRole('button', { name: 'View' }))
  const dialog = within(screen.getByRole('dialog'))
  await user.click(await dialog.findByRole('button', { name: 'Receive stock' }))
  await user.type(dialog.getByLabelText(/^Delivery reference/), 'DEL-42')
  await user.clear(dialog.getByLabelText('KEY-1 quantity')); await user.type(dialog.getByLabelText('KEY-1 quantity'), '4')
  await user.click(dialog.getByRole('button', { name: 'Post receipt' }))
  expect(await dialog.findByText(/Office supply · Partial/)).toBeInTheDocument()
  const call = fetch.mock.calls.find(([url]) => url.endsWith('/receive'))
  expect(JSON.parse(call[1].body)).toMatchObject({ version: 'v1', reference: 'DEL-42', lines: [{ lineId: 'line', quantity: 4 }] })
})

it('reuses a lost confirmation request and requires explicit action confirmation', async () => {
  const fetch = server({ status: 'Draft', lostResponse: true }); const user = userEvent.setup(); render(<App />)
  await user.click(await screen.findByRole('button', { name: 'View' }))
  const dialog = within(screen.getByRole('dialog'))
  await user.click(await dialog.findByRole('button', { name: 'Confirm order' }))
  expect(fetch.mock.calls.some(([url]) => url.endsWith('/confirm'))).toBe(false)
  await user.click(dialog.getByRole('button', { name: 'Confirm action' }))
  expect(await dialog.findByRole('alert')).toHaveTextContent('Result unknown')
  await user.click(dialog.getByRole('button', { name: 'Retry request' }))
  const attempts = fetch.mock.calls.filter(([url]) => url.endsWith('/confirm'))
  expect(attempts).toHaveLength(2); expect(attempts[0][1].body).toBe(attempts[1][1].body)
})

it('shows order details without mutation controls for a viewer', async () => {
  server({ role: 'Viewer' }); const user = userEvent.setup(); render(<App />)
  await user.click(await screen.findByRole('button', { name: 'View' }))
  expect(await within(screen.getByRole('dialog')).findByRole('table', { name: 'Order lines' })).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Receive stock' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'New order' })).not.toBeInTheDocument()
})

it('ships sales quantities with a shipment reference and explains availability', async () => {
  const fetch = server({ kind: 'sales' }); const user = userEvent.setup(); render(<App />)
  expect(await screen.findByText(/Confirmation does not reserve stock/)).toBeInTheDocument()
  await user.click(await screen.findByRole('button', { name: 'View' }))
  const dialog = within(screen.getByRole('dialog'))
  await user.click(await dialog.findByRole('button', { name: 'Ship order' }))
  await user.type(dialog.getByLabelText(/^Shipment reference/), 'SHIP-42')
  await user.clear(dialog.getByLabelText('KEY-1 quantity')); await user.type(dialog.getByLabelText('KEY-1 quantity'), '2')
  await user.click(dialog.getByRole('button', { name: 'Post shipment' }))
  expect(await dialog.findByText(/Office supply · Partial/)).toBeInTheDocument()
  const call = fetch.mock.calls.find(([url]) => url.endsWith('/ship'))
  expect(JSON.parse(call[1].body)).toMatchObject({ reference: 'SHIP-42', lines: [{ lineId: 'line', quantity: 2 }] })
})

it('uses the selected product selling price for a sales draft', async () => {
  server({ kind: 'sales' }); const user = userEvent.setup(); render(<App />)
  await user.click(await screen.findByRole('button', { name: 'New order' }))
  const dialog = within(screen.getByRole('dialog'))
  expect(dialog.getByRole('combobox', { name: /^Customer/ })).toBeInTheDocument()
  await user.click(dialog.getByRole('combobox', { name: /^Product 1/ }))
  await user.click(await screen.findByRole('option', { name: 'KEY-1 · Keyboard' }))
  expect(dialog.getByLabelText(/^Unit price 1/)).toHaveValue(12.125)
})
