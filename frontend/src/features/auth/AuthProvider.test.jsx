import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import AuthProvider from './AuthProvider'
import { useAuth } from './authContext'

function SessionProbe() {
  const { user, notice, retry } = useAuth()
  return <><span>{user ? user.email : 'signed out'}</span><span>{notice}</span><button onClick={() => retry()}>Refresh</button></>
}
afterEach(() => vi.unstubAllGlobals())

it('clears private cached data when a session refresh returns 401', async () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  client.setQueryData(['session'], { email: 'viewer@example.test', roles: ['Viewer'] })
  client.setQueryData(['users'], [{ email: 'private@example.test' }])
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', {
    status: 401, headers: { 'Content-Type': 'application/json' },
  })))
  const user = userEvent.setup()
  const view = render(<QueryClientProvider client={client}><AuthProvider><SessionProbe /></AuthProvider></QueryClientProvider>)
  await user.click(screen.getByRole('button', { name: 'Refresh' }))
  await waitFor(() => expect(screen.getByText('signed out')).toBeInTheDocument())
  expect(client.getQueryData(['users'])).toBeUndefined()
  expect(screen.getByText('Your session has expired. Please sign in again.')).toBeInTheDocument()
  view.unmount()
  client.clear()
})
