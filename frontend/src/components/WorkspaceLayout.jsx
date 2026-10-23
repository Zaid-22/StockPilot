import { useState } from 'react'
import { Link, Outlet, useLocation } from 'react-router-dom'
import { Alert, Avatar, Box, Button, Chip, Divider, Stack, Typography } from '@mui/material'
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined'
import PeopleOutlineIcon from '@mui/icons-material/PeopleOutline'
import LogoutIcon from '@mui/icons-material/Logout'
import Inventory2OutlinedIcon from '@mui/icons-material/Inventory2Outlined'
import { catalogs } from '../features/catalog/catalog'
import Brand from './Brand'
import { useAuth } from '../features/auth/authContext'

export default function WorkspaceLayout() {
  const { user, logout } = useAuth()
  const { pathname } = useLocation()
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  async function signOut() {
    setBusy(true); setError('')
    try { await logout() } catch (e) { setError(e.message) } finally { setBusy(false) }
  }
  return <Box className="workspace">
    <Box component="aside" className="sidebar">
      <Brand /><Typography variant="overline" sx={{ mt: 5, mb: 1, color: 'text.secondary', letterSpacing: 1.5 }}>WORKSPACE</Typography>
      <Button component={Link} to="/" startIcon={<DashboardOutlinedIcon />} variant={pathname === '/' ? 'contained' : 'text'} sx={{ justifyContent: 'flex-start', mb: 1 }}>Overview</Button>
      {Object.entries(catalogs).map(([kind, catalog]) => <Button key={kind} component={Link} to={`/catalog/${kind}`} startIcon={<Inventory2OutlinedIcon />} variant={pathname === `/catalog/${kind}` ? 'contained' : 'text'} sx={{ justifyContent: 'flex-start', mb: 1 }}>{catalog.title}</Button>)}
      {user.roles.includes('Admin') && <Button component={Link} to="/access" startIcon={<PeopleOutlineIcon />} variant={pathname === '/access' ? 'contained' : 'text'} sx={{ justifyContent: 'flex-start' }}>User access</Button>}
      <Box sx={{ flex: 1 }} /><Divider sx={{ my: 2 }} />
      <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 2 }}><Avatar sx={{ bgcolor: '#e0e9d7', color: 'primary.main' }}>{user.displayName?.[0] || user.email[0]}</Avatar><Box sx={{ minWidth: 0 }}><Typography variant="body2" fontWeight={700} noWrap>{user.displayName || user.email}</Typography><Typography variant="caption" color="text.secondary">{user.roles.join(' · ')}</Typography></Box></Stack>
      <Button onClick={signOut} disabled={busy} startIcon={<LogoutIcon />} sx={{ justifyContent: 'flex-start' }}>{busy ? 'Signing out…' : 'Sign out'}</Button>
    </Box>
    <Box className="workspace-main"><Box component="header" sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 5 }}><Typography color="text.secondary" variant="body2">Workspace / {pathname === '/access' ? 'User access' : catalogs[pathname.split('/')[2]]?.title || 'Overview'}</Typography><Chip label="Connected workspace" size="small" variant="outlined" color="primary" /></Box>
      {error && <Alert severity="error" sx={{ mb: 3 }}>{error}</Alert>}<Outlet />
    </Box>
  </Box>
}
