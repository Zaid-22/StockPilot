import { useCallback, useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { apiMutation, apiRequest } from '../../lib/api'
import { AuthContext } from './authContext'

export default function AuthProvider({ children }) {
  const queryClient = useQueryClient()
  const [notice, setNotice] = useState('')
  const expireSession = useCallback(async () => {
    const hadSession = Boolean(queryClient.getQueryData(['session']))
    const privateQueries = { predicate: (query) => query.queryKey[0] !== 'session' }
    await queryClient.cancelQueries(privateQueries)
    queryClient.removeQueries(privateQueries)
    queryClient.setQueryData(['session'], null)
    if (hadSession) setNotice('Your session has expired. Please sign in again.')
  }, [queryClient])
  const session = useQuery({
    queryKey: ['session'],
    queryFn: async () => {
      try { return await apiRequest('/auth/me') }
      catch (error) {
        if (error.status === 401) { await expireSession(); return null }
        throw error
      }
    },
    retry: false, staleTime: 30_000, refetchInterval: 60_000,
  })

  useEffect(() => {
    function expired() {
      void expireSession()
    }
    window.addEventListener('stockpilot:session-expired', expired)
    return () => window.removeEventListener('stockpilot:session-expired', expired)
  }, [expireSession])

  async function login(values) {
    const user = await apiMutation('/auth/login', values)
    await queryClient.cancelQueries()
    queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== 'session' })
    queryClient.setQueryData(['session'], user)
    setNotice('')
  }

  async function logout() {
    try { await apiMutation('/auth/logout') }
    catch (error) { if (error.status !== 401) throw error }
    await queryClient.cancelQueries()
    queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== 'session' })
    queryClient.setQueryData(['session'], null)
    setNotice('You have been signed out.')
  }

  return <AuthContext.Provider value={{ user: session.data, loading: session.isPending, error: session.error,
    retry: session.refetch, login, logout, notice }}>{children}</AuthContext.Provider>
}
