import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Box, Button, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, TextField, Typography } from '@mui/material'
import { apiRequest } from '../../lib/api'
import { useAuth } from '../auth/authContext'
import OrderEditor from './OrderEditor'
import OrderDetails from './OrderDetails'

export default function OrdersPage({ kind = 'purchases' }) {
  const { user } = useAuth()
  const cache = useQueryClient()
  const editable = user.roles.some(x => ['Admin', 'Staff'].includes(x))
  const [create, setCreate] = useState(false)
  const [selected, setSelected] = useState(null)
  const [page, setPage] = useState(0)
  const [search, setSearch] = useState('')
  const [q, setQ] = useState('')
  const [status, setStatus] = useState('')
  const params = new URLSearchParams({ page: page + 1, pageSize: 20, q, ...(status ? { status } : {}) })
  const query = useQuery({ queryKey: ['orders', kind, page, q, status], queryFn: () => apiRequest(`/orders/${kind}?${params}`) })
  const title = kind === 'purchases' ? 'Purchase orders' : 'Sales orders'
  return <Stack spacing={3}>
    <Stack direction="row" justifyContent="space-between"><Box><Typography component="h1" variant="h4" fontWeight={700}>{title}</Typography><Typography color="text.secondary">Draft, confirm, and track partial deliveries.</Typography></Box>{editable && <Button variant="contained" onClick={() => setCreate(true)}>New order</Button>}</Stack>
    {kind === 'sales' && <Alert severity="info">Confirmation does not reserve stock. Availability is checked when you post a shipment.</Alert>}
    <Stack component="form" direction={{ xs: 'column', sm: 'row' }} spacing={2} onSubmit={e => { e.preventDefault(); setQ(search); setPage(0) }}>
      <TextField label="Search order or contact" size="small" value={search} onChange={e => setSearch(e.target.value)} />
      <TextField label="Status" select slotProps={{ select: { native: true } }} size="small" value={status} onChange={e => { setStatus(e.target.value); setPage(0) }}><option value="">All</option>{['Draft', 'Confirmed', 'Partial', 'Completed', 'Cancelled'].map(x => <option key={x}>{x}</option>)}</TextField>
      <Button type="submit">Search</Button><Button onClick={() => query.refetch()}>Refresh</Button>
    </Stack>
    {query.isPending && <Typography role="status">Loading orders…</Typography>}{query.error && <Alert severity="error">{query.error.message}</Alert>}
    {query.data && <Paper variant="outlined"><TableContainer><Table aria-label={title}><TableHead><TableRow>{['Order', 'Contact', 'Status', 'Created', 'Actions'].map(x => <TableCell key={x}>{x}</TableCell>)}</TableRow></TableHead><TableBody>{query.data.items.map(row => <TableRow key={row.id}><TableCell sx={{ overflowWrap: 'anywhere' }}>{row.number}</TableCell><TableCell>{row.partyName}</TableCell><TableCell>{row.status}</TableCell><TableCell>{new Date(row.createdAtUtc).toLocaleDateString()}</TableCell><TableCell><Button onClick={() => setSelected(row.id)}>View</Button></TableCell></TableRow>)}{!query.data.items.length && <TableRow><TableCell colSpan={5}>No matching orders.</TableCell></TableRow>}</TableBody></Table></TableContainer><TablePagination component="div" count={query.data.totalCount} page={page} rowsPerPage={20} rowsPerPageOptions={[20]} onPageChange={(_, value) => setPage(value)} /></Paper>}
    {create && <OrderEditor kind={kind} onClose={() => setCreate(false)} onSaved={async order => { setCreate(false); setSelected(order.id); await cache.invalidateQueries({ queryKey: ['orders'] }) }} />}
    {selected && <OrderDetails kind={kind} id={selected} editable={editable} onClose={() => setSelected(null)} />}
  </Stack>
}
