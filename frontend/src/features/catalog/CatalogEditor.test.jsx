import { afterEach, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import CatalogEditor from './CatalogEditor'

const json = (value, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
afterEach(() => vi.unstubAllGlobals())

function editorFixture() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const state = { record: { id: 'category-1', name: 'Original', version: 'v1', isActive: true }, failRead: false, writes: [] }
  const onClose = vi.fn()
  vi.stubGlobal('fetch', vi.fn(async (url, options = {}) => {
    if (url === '/api/auth/csrf') return json({ token: 'csrf' })
    if (url === '/api/catalog/categories/category-1') {
      if (options.method === 'PUT') {
        const input = JSON.parse(options.body)
        state.writes.push(input)
        if (input.version !== state.record.version) return json({ detail: 'This record changed. Reload it before saving again.' }, 409)
        return json({ ...state.record, ...input, version: 'v3' })
      }
      if (state.failRead) throw new TypeError('offline')
      return json(state.record)
    }
    throw new Error(`Unexpected request: ${url}`)
  }))
  render(<QueryClientProvider client={client}><CatalogEditor kind="categories" id="category-1" categories={[]} onClose={onClose} onSaved={vi.fn()} /></QueryClientProvider>)
  return { state, client, onClose }
}

it('preserves the draft and original version on background refresh until the user reloads', async () => {
  const { state, client, onClose } = editorFixture()
  const user = userEvent.setup()
  const name = await screen.findByRole('textbox', { name: /^Name/ })
  await user.clear(name)
  await user.type(name, 'My draft')
  state.record = { ...state.record, name: 'Changed by colleague', version: 'v2' }
  await act(async () => { await client.refetchQueries({ queryKey: ['catalog', 'categories', 'category-1'] }); await new Promise(resolve => setTimeout(resolve, 0)) })
  expect(screen.getByRole('textbox', { name: /^Name/ })).toHaveValue('My draft')
  await user.click(screen.getByRole('button', { name: 'Save' }))
  await screen.findByRole('button', { name: 'Reload latest' })
  expect(state.writes[0]).toMatchObject({ name: 'My draft', version: 'v1' })
  state.failRead = true
  await user.click(screen.getByRole('button', { name: 'Reload latest' }))
  await waitFor(() => expect(screen.getByRole('textbox', { name: /^Name/ })).toBeEnabled())
  expect(screen.getByRole('textbox', { name: /^Name/ })).toHaveValue('My draft')
  expect(onClose).not.toHaveBeenCalled()
  state.failRead = false
  await user.click(screen.getByRole('button', { name: 'Reload latest' }))
  await waitFor(() => expect(screen.getByRole('textbox', { name: /^Name/ })).toHaveValue('Changed by colleague'))
  await user.click(screen.getByRole('button', { name: 'Save' }))
  await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1))
  expect(state.writes[1]).toMatchObject({ name: 'Changed by colleague', version: 'v2' })
})

it('keeps unsaved fields visible if a background detail request fails', async () => {
  const { state, client } = editorFixture()
  const user = userEvent.setup()
  const name = await screen.findByRole('textbox', { name: /^Name/ })
  await user.clear(name)
  await user.type(name, 'My draft')
  state.failRead = true
  await act(async () => { await client.refetchQueries({ queryKey: ['catalog', 'categories', 'category-1'] }); await new Promise(resolve => setTimeout(resolve, 0)) })
  expect(screen.getByRole('textbox', { name: /^Name/ })).toHaveValue('My draft')
})
