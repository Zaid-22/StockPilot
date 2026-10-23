import { afterEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from '../../App'

const report = { activeProducts: 3, unitsOnHand: 12, lowStockProducts: 1, openPurchaseOrders: 2, openSalesOrders: 1,
  unitsReceived: 5, unitsShipped: 2, receivedOrderValue: 20.625, shippedOrderValue: 24.25, generatedAtUtc: '2026-10-23T10:00:00Z',
  lowStock: [{ productId: 'p1', sku: 'KEY-1', name: 'Keyboard', quantityOnHand: 2, reorderLevel: 3 }],
  dailyStock: [{ date: '2026-10-23', received: 5, shipped: 2, adjusted: 0 }] }
const json = data => new Response(JSON.stringify(data), { headers: { 'Content-Type': 'application/json' } })
function server(path) {
  window.history.replaceState(null, '', path)
  const fetch = vi.fn(async url => {
    if (url === '/api/auth/me') return json({ email: 'staff@test', displayName: 'Zaid', roles: ['Staff'] })
    if (url.startsWith('/api/reports/summary')) return json(report)
    throw new Error(`Unexpected request ${url}`)
  })
  vi.stubGlobal('fetch', fetch)
  return fetch
}
afterEach(() => vi.unstubAllGlobals())

it('shows real summary metrics and low-stock products on overview', async () => {
  server('/'); render(<App />)
  expect(await screen.findByRole('table', { name: 'Low stock products' })).toHaveTextContent('KEY-1 · Keyboard')
  expect(screen.getByText('12')).toBeInTheDocument()
  expect(screen.getByText(/2 purchase orders awaiting receipt/)).toBeInTheDocument()
})

it('loads an explicit UTC range and displays fulfilled values', async () => {
  const fetch = server('/reports'); const user = userEvent.setup(); render(<App />)
  expect(await screen.findByText('20.625')).toBeInTheDocument()
  expect(screen.getByText('24.250')).toBeInTheDocument()
  const from = screen.getByLabelText(/From \(UTC\)/); const to = screen.getByLabelText(/To \(UTC\)/)
  await user.clear(from); await user.type(from, '2026-10-01')
  await user.clear(to); await user.type(to, '2026-10-23')
  await user.click(screen.getByRole('button', { name: 'Apply range' }))
  await waitFor(() => expect(fetch.mock.calls.some(([url]) => url.includes('from=2026-10-01&to=2026-10-23'))).toBe(true))
  expect(screen.getByRole('table', { name: 'Daily stock activity' })).toHaveTextContent('2026-10-23')
})
