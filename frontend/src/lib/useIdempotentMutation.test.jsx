import { act, renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { expect, it, vi } from 'vitest'
import { ApiError } from './api'
import { useIdempotentMutation } from './useIdempotentMutation'

function setup(mutationFn) {
  const client = new QueryClient()
  return renderHook(() => useIdempotentMutation({ mutationFn }), {
    wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
  })
}

it.each([400, 403, 409, 429])('retains the original command across an uncertain failure followed by HTTP %s', async status => {
  const write = vi.fn().mockRejectedValueOnce(new ApiError('Lost', 0))
    .mockRejectedValueOnce(new ApiError('Retry rejected', status)).mockResolvedValue({ id: 'saved' })
  const { result } = setup(write)
  const original = { requestId: 'original', quantity: 5 }
  act(() => result.current.submit(() => original))
  await waitFor(() => expect(result.current.uncertain).toBe(true))
  act(() => result.current.submit(() => ({ requestId: 'replacement' })))
  await waitFor(() => expect(result.current.error?.status).toBe(status))
  expect(result.current.locked).toBe(true)
  act(() => result.current.submit(() => ({ requestId: 'another' })))
  await waitFor(() => expect(result.current.isSuccess).toBe(true))
  expect(write.mock.calls.map(([request]) => request)).toEqual([original, original, original])
  expect(result.current.locked).toBe(false)
})

it('allows a corrected command after an initial definitive rejection and a new command after success', async () => {
  const write = vi.fn().mockRejectedValueOnce(new ApiError('Invalid', 400)).mockResolvedValue({ id: 'saved' })
  const { result } = setup(write)
  act(() => result.current.submit(() => ({ requestId: 'invalid' })))
  await waitFor(() => expect(result.current.isError).toBe(true))
  expect(result.current.locked).toBe(false)
  act(() => result.current.submit(() => ({ requestId: 'corrected' })))
  await waitFor(() => expect(result.current.isSuccess).toBe(true))
  act(() => result.current.submit(() => ({ requestId: 'next' })))
  await waitFor(() => expect(write).toHaveBeenCalledTimes(3))
  expect(write.mock.calls.map(([request]) => request.requestId)).toEqual(['invalid', 'corrected', 'next'])
})
