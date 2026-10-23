import { lazy, Suspense, useState } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Navigate, Outlet, Route, Routes, useLocation, Link } from 'react-router-dom'
import { Alert, Box, Button, CircularProgress, CssBaseline, ThemeProvider, Typography, createTheme } from '@mui/material'
import AuthProvider from './features/auth/AuthProvider'
import { useAuth } from './features/auth/authContext'
import LoginPage from './features/auth/LoginPage'
import WorkspaceLayout from './components/WorkspaceLayout'
const OverviewPage = lazy(() => import('./features/workspace/OverviewPage'))
const AccessPage = lazy(() => import('./features/access/AccessPage'))
const CatalogPage = lazy(() => import('./features/catalog/CatalogPage'))
const InventoryPage = lazy(() => import('./features/inventory/InventoryPage'))
const OrdersPage = lazy(() => import('./features/orders/OrdersPage'))

const ReportsPage = lazy(() => import('./features/reports/ReportsPage'))

const theme = createTheme({
  palette: { primary: { main: '#244f3d' }, background: { default: '#f7f8f4', paper: '#ffffff' }, text: { primary: '#21392e', secondary: '#68766e' } },
  typography: { fontFamily: 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif', button: { textTransform: 'none', fontWeight: 600 } },
  shape: { borderRadius: 12 },
  components: { MuiButton: { defaultProps: { disableElevation: true } }, MuiPaper: { styleOverrides: { outlined: { borderColor: '#e1e6dd' } } } },
})

function Protected({ admin = false }) {
  const { user } = useAuth()
  const location = useLocation()
  if (!user) return <Navigate to="/login" state={{ from: location.pathname }} replace />
  if (admin && !user.roles.includes('Admin')) return <Box sx={{ p: 5 }}><Typography component="h1" variant="h4">You don't have access to this page.</Typography><Button component={Link} to="/" sx={{ mt: 2 }}>Back to overview</Button></Box>
  return <Outlet />
}

function SessionRoutes() {
  const auth = useAuth()
  if (auth.loading) return <Box className="center-screen"><CircularProgress aria-label="Checking your session" /></Box>
  if (auth.error) return <Box className="center-screen"><Alert severity="error" action={<Button onClick={() => auth.retry()}>Retry</Button>}>{auth.error.message}</Alert></Box>
  return <Suspense fallback={<Box sx={{ p: 4 }}><CircularProgress aria-label="Loading page" /></Box>}><Routes>
    <Route path="/login" element={<LoginPage />} />
    <Route element={<Protected />}><Route element={<WorkspaceLayout />}><Route index element={<OverviewPage />} /><Route path="/catalog/:kind" element={<CatalogPage />} /><Route path="/inventory" element={<InventoryPage />} /><Route path="/reports" element={<ReportsPage />} /><Route path="/purchases" element={<OrdersPage key="purchases" kind="purchases" />} /><Route path="/sales" element={<OrdersPage key="sales" kind="sales" />} /><Route element={<Protected admin />}><Route path="/access" element={<AccessPage />} /></Route></Route></Route>
    <Route path="*" element={<Box sx={{ p: 6 }}><Typography component="h1" variant="h4">Page not found</Typography><Button component={Link} to="/">Return to workspace</Button></Box>} />
  </Routes></Suspense>
}

export default function App() {
  const [queryClient] = useState(() => new QueryClient({ defaultOptions: { queries: { retry: 1, staleTime: 30_000 } } }))
  return <ThemeProvider theme={theme}><CssBaseline /><QueryClientProvider client={queryClient}><BrowserRouter><AuthProvider><SessionRoutes /></AuthProvider></BrowserRouter></QueryClientProvider></ThemeProvider>
}
