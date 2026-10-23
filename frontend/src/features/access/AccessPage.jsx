import { useQuery } from '@tanstack/react-query'
import { Alert, Button, Chip, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material'
import { apiRequest } from '../../lib/api'
export default function AccessPage() {
  const users = useQuery({ queryKey: ['users'], queryFn: () => apiRequest('/admin/users'), retry: false })
  return <><Typography component="h1" variant="h3" fontWeight={700} sx={{ mb: 2, letterSpacing: -1.5 }}>User access</Typography><Typography color="text.secondary" sx={{ mb: 4 }}>People who can access your workspace. Showing up to 50 accounts.</Typography>
    {users.isPending && <Typography role="status">Loading accounts…</Typography>}
    {users.error && <Alert severity="error" action={<Button onClick={() => users.refetch()}>Retry</Button>}>{users.error.message}</Alert>}
    {users.data && <TableContainer component={Paper} variant="outlined"><Table aria-label="Workspace users"><TableHead><TableRow><TableCell>Name</TableCell><TableCell>Email</TableCell><TableCell>Role</TableCell></TableRow></TableHead><TableBody>{users.data.map(user => <TableRow key={user.id}><TableCell>{user.displayName || '—'}</TableCell><TableCell>{user.email}</TableCell><TableCell><Stack direction="row" spacing={1}>{user.roles.map(role => <Chip key={role} size="small" label={role} variant="outlined" />)}</Stack></TableCell></TableRow>)}{users.data.length === 0 && <TableRow><TableCell colSpan={3}>No accounts found.</TableCell></TableRow>}</TableBody></Table></TableContainer>}
  </>
}
