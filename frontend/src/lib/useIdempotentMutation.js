import { useRef, useState } from 'react'
import { useMutation } from '@tanstack/react-query'

// A failed retry does not establish whether an earlier write committed.
export function useIdempotentMutation({ mutationFn, onSuccess }) {
  const attempt = useRef(null)
  const inFlight = useRef(false)
  const [uncertain, setUncertain] = useState(false)
  const mutation = useMutation({
    mutationFn,
    retry: false,
    onError: error => {
      if (error.status === 0 || error.status >= 500) {
        attempt.current.uncertain = true
        setUncertain(true)
      } else if (!attempt.current?.uncertain) {
        attempt.current = null
      }
    },
    onSuccess: async (...args) => {
      await onSuccess?.(...args)
      attempt.current = null
      setUncertain(false)
    },
    onSettled: () => { inFlight.current = false },
  })

  function submit(createRequest) {
    if (inFlight.current) return
    attempt.current ??= { request: createRequest(), uncertain: false }
    inFlight.current = true
    mutation.mutate(attempt.current.request)
  }

  return { ...mutation, submit, uncertain, locked: mutation.isPending || uncertain }
}
