import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from '../../App'
import { productSchema } from './catalog'

const categoryId = '24fb2123-22f4-453e-95d4-7d50669e2648'
const productId = 'a23801b1-8467-458a-81ac-fb32e65b73c1'
const version = '1b4ff1a9-21e7-42fc-9403-bf51d3496e4f'
const category = { id: categoryId, name: 'Electronics', isActive: true, version }
const product = { id: productId, name: 'Keyboard', sku: 'KEY-001', categoryId, categoryName: 'Electronics', sellingPrice: 25.125, reorderLevel: 3, quantityOnHand: 0, description: '', isActive: true, version }
const json = (value, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })

function server({ role = 'Staff', kind = 'products', initial = [], failSave = false, total = null } = {}) {
  let records = initial
  const fetch = vi.fn(async (raw, options = {}) => {
    const url = new URL(raw, 'http://localhost')
    if (url.pathname === '/api/auth/me') return json({ id: '1', email: 'staff@example.test', displayName: 'Staff', roles: [role] })
    if (url.pathname === '/api/auth/csrf') return json({ token: 'csrf' })
    if (url.pathname === '/api/catalog/settings') return json({ currency: 'JOD' })
    if (url.pathname === '/api/catalog/categories' && kind !== 'categories') return json({ items: [category], totalCount: 1, page: 1, pageSize: 100 })
    if (url.pathname.startsWith(`/api/catalog/${kind}`)) {
      if (options.method === 'POST' || options.method === 'PUT') {
        if (failSave) return json({ detail: 'This SKU is already used.' }, 409)
        const data = JSON.parse(options.body)
        const saved = { ...data, id: data.id || (kind === 'products' ? productId : categoryId), version, quantityOnHand: 0, categoryName: category.name }
        records = options.method === 'POST' ? [...records, saved] : records.map(r => r.id === saved.id || raw.endsWith(r.id) ? { ...saved, id: r.id } : r)
        return json(saved, options.method === 'POST' ? 201 : 200)
      }
      if (url.pathname.split('/').length === 5) return json(records.find(r => raw.endsWith(r.id)))
      return json({ items: records, totalCount: total ?? records.length, page: Number(url.searchParams.get('page')), pageSize: Number(url.searchParams.get('pageSize')) })
    }
    throw new Error(`Unexpected request: ${raw}`)
  })
  vi.stubGlobal('fetch', fetch)
  return fetch
}

beforeEach(() => window.history.replaceState(null, '', '/catalog/products'))
afterEach(() => vi.unstubAllGlobals())

it('creates a product and renders the server result with zero stock', async () => {
  const fetch = server()
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Add product' }))
  const dialog = within(screen.getByRole('dialog'))
  await user.type(dialog.getByRole('textbox', { name: /^Name/ }), 'Keyboard')
  await user.type(dialog.getByRole('textbox', { name: /^SKU/ }), ' key-001 ')
  await user.selectOptions(dialog.getByRole('combobox', { name: /^Category/ }), categoryId)
  await user.clear(dialog.getByLabelText(/^Selling price/))
  await user.type(dialog.getByLabelText(/^Selling price/), '25.125')
  await user.click(dialog.getByRole('button', { name: 'Save' }))
  expect(await screen.findByText('Product saved.')).toBeInTheDocument()
  expect(screen.getByRole('table', { name: 'Products' })).toHaveTextContent('KEY-001')
  const request = fetch.mock.calls.find(([url, options]) => url === '/api/catalog/products' && options.method === 'POST')
  expect(JSON.parse(request[1].body)).toMatchObject({ sku: 'KEY-001', sellingPrice: 25.125, categoryId, reorderLevel: 0 })
  expect(request[1].headers['X-CSRF-TOKEN']).toBe('csrf')
})

it('keeps the form and submitted values when the backend rejects a duplicate SKU', async () => {
  server({ initial: [product], failSave: true })
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Edit' }))
  const dialog = within(screen.getByRole('dialog'))
  await user.click(await dialog.findByRole('button', { name: 'Save' }))
  expect(await dialog.findByRole('alert')).toHaveTextContent('This SKU is already used.')
  expect(dialog.getByRole('textbox', { name: /^SKU/ })).toHaveValue('KEY-001')
})

it('creates a category without contact fields', async () => {
  window.history.replaceState(null, '', '/catalog/categories')
  server({ kind: 'categories' })
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Add category' }))
  const dialog = within(screen.getByRole('dialog'))
  expect(dialog.queryByLabelText('Email')).not.toBeInTheDocument()
  await user.type(dialog.getByRole('textbox', { name: /^Name/ }), 'Electronics')
  await user.click(dialog.getByRole('button', { name: 'Save' }))
  expect(await screen.findByText('Category saved.')).toBeInTheDocument()
  expect(screen.getByRole('table', { name: 'Categories' })).toHaveTextContent('Electronics')
})

it('deactivates a product only after confirmation and keeps its history visible', async () => {
  const fetch = server({ initial: [product] })
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Deactivate' }))
  expect(fetch.mock.calls.some(([, options]) => options.method === 'PUT')).toBe(false)
  await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirm' }))
  expect(await screen.findByText('Product deactivated.')).toBeInTheDocument()
  const table = await screen.findByRole('table', { name: 'Products' })
  expect(table).toHaveTextContent('Keyboard')
  expect(table).toHaveTextContent('Inactive')
})

it('gives viewers read access without edit controls', async () => {
  server({ initial: [product], role: 'Viewer' })
  render(<App />)
  expect(await screen.findByText('Keyboard')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Add product' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'View' })).toBeInTheDocument()
})

it('sends search and status filters to the API and retains them when paging', async () => {
  const fetch = server({ initial: [product], total: 30 })
  const user = userEvent.setup()
  render(<App />)
  await user.type(await screen.findByRole('textbox', { name: 'Search name or SKU' }), 'keyboard')
  await user.selectOptions(screen.getByRole('combobox', { name: 'Status' }), 'true')
  await user.click(screen.getByRole('button', { name: 'Search' }))
  await waitFor(() => expect(fetch.mock.calls.some(([url]) => url.includes('q=keyboard') && url.includes('isActive=true'))).toBe(true))
  await user.click(await screen.findByRole('button', { name: 'Go to next page' }))
  await waitFor(() => expect(fetch.mock.calls.some(([url]) => url.includes('page=2') && url.includes('q=keyboard'))).toBe(true))
})

it('rejects invalid price precision and fractional reorder quantities before submission', () => {
  const values = { name: 'Keyboard', sku: 'KEY-001', categoryId, description: '', sellingPrice: '1.2345', reorderLevel: '1.5', isActive: true }
  const result = productSchema.safeParse(values)
  expect(result.success).toBe(false)
  expect(result.error.issues.map(issue => issue.path[0])).toEqual(expect.arrayContaining(['sellingPrice', 'reorderLevel']))
})
