import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Box, Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, TextField, Typography } from '@mui/material'
import { apiMutation, apiRequest } from '../../lib/api'
import { useIdempotentMutation } from '../../lib/useIdempotentMutation'
import { useAuth } from '../auth/authContext'

function AdjustmentDialog({ balance, onClose }) {
  const cache = useQueryClient()
  const [quantity, setQuantity] = useState('')
  const [reason, setReason] = useState('')
  const mutation = useIdempotentMutation({ mutationFn: body => apiMutation('/inventory/adjustments', body), onSuccess: async () => {
    await cache.invalidateQueries()
    onClose()
  } })
  const { uncertain, locked } = mutation
  function submit(event) {
    event.preventDefault()
    mutation.submit(() => ({ requestId: crypto.randomUUID(), productId: balance.productId, version: balance.version, quantityChange: Number(quantity), reason: reason.trim() }))
  }
  return <Dialog open onClose={locked ? undefined : onClose} fullWidth maxWidth="sm">
    <Box component="form" onSubmit={submit}>
      <DialogTitle>Adjust {balance.name}</DialogTitle>
      <DialogContent><Stack spacing={2} sx={{ pt: 1 }}>
        <Typography>Current stock: {balance.quantityOnHand}. Enter a positive number to add stock or a negative number to remove it.</Typography>
        {mutation.error && <Alert severity="error">{mutation.error.message}{uncertain && ' Result unknown. Retry this request before making changes.'}</Alert>}
        <TextField label="Quantity change" type="number" required value={quantity} disabled={locked} onChange={e => setQuantity(e.target.value)} slotProps={{ htmlInput: { min: -1000000000, max: 1000000000, step: 1 } }} />
        <TextField label="Reason" required multiline value={reason} disabled={locked} onChange={e => setReason(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }} />
        <Typography variant="body2" color="text.secondary">This creates a permanent stock movement. Corrections require another adjustment.</Typography>
      </Stack></DialogContent>
      <DialogActions><Button onClick={onClose} disabled={locked}>Cancel</Button><Button type="submit" variant="contained" disabled={mutation.isPending || !Number.isInteger(Number(quantity)) || Number(quantity) === 0 || !reason.trim()}>{uncertain ? 'Retry adjustment' : 'Post adjustment'}</Button></DialogActions>
    </Box>
  </Dialog>
}

export default function InventoryPage() {
  const { user } = useAuth()
  const editable = user.roles.some(role => ['Admin', 'Staff'].includes(role))
  const [history, setHistory] = useState(false)
  const [search, setSearch] = useState('')
  const [q, setQ] = useState('')
  const [low, setLow] = useState(false)
  const [page, setPage] = useState(0)
  const [selected, setSelected] = useState(null)
  const params = new URLSearchParams({ page: page + 1, pageSize: 20, q, lowStockOnly: low })
  const query = useQuery({ queryKey: ['inventory', history, page, q, low], queryFn: () => apiRequest(`/inventory/${history ? 'movements' : 'balances'}?${params}`) })
  return <Stack spacing={3}>
    <Box><Typography component="h1" variant="h4" fontWeight={700}>Inventory</Typography><Typography color="text.secondary">Main warehouse · stock balances and movement history</Typography></Box>
    <Stack direction="row" spacing={1}><Button variant={!history ? 'contained' : 'outlined'} onClick={() => { setHistory(false); setPage(0) }}>Balances</Button><Button variant={history ? 'contained' : 'outlined'} onClick={() => { setHistory(true); setPage(0) }}>Movement history</Button></Stack>
    <Stack component="form" direction={{ xs: 'column', sm: 'row' }} spacing={2} onSubmit={e => { e.preventDefault(); setQ(search); setPage(0) }}>
      <TextField label="Search name or SKU" value={search} onChange={e => setSearch(e.target.value)} size="small" />
      {!history && <FormControlLabel control={<Checkbox checked={low} onChange={e => { setLow(e.target.checked); setPage(0) }} />} label="Low stock only" />}
      <Button type="submit">Search</Button><Button onClick={() => query.refetch()}>Refresh</Button>
    </Stack>
    {query.isPending && <Typography role="status">Loading inventory…</Typography>}
    {query.error && <Alert severity="error">{query.error.message}</Alert>}
    {query.data && <Paper variant="outlined"><TableContainer><Table aria-label={history ? 'Stock movements' : 'Stock balances'}>
      <TableHead><TableRow>{(history ? ['Date', 'Product', 'Type', 'Change', 'Stock after', 'Reason', 'Actor'] : ['SKU', 'Product', 'On hand', 'Reorder level', 'Status', 'Actions']).map(label => <TableCell key={label}>{label}</TableCell>)}</TableRow></TableHead>
      <TableBody>{query.data.items.map(row => history ? <TableRow key={row.id}><TableCell>{new Date(row.createdAtUtc).toLocaleString()}</TableCell><TableCell>{row.sku} · {row.productName}</TableCell><TableCell>{row.kind}</TableCell><TableCell>{row.quantityChange > 0 ? '+' : ''}{row.quantityChange}</TableCell><TableCell>{row.quantityAfter}</TableCell><TableCell>{row.reason}</TableCell><TableCell>{row.actorId}</TableCell></TableRow> :
        <TableRow key={row.productId}><TableCell>{row.sku}</TableCell><TableCell>{row.name}</TableCell><TableCell>{row.quantityOnHand}</TableCell><TableCell>{row.reorderLevel}</TableCell><TableCell>{!row.isActive ? 'Inactive' : row.quantityOnHand <= row.reorderLevel ? 'Low stock' : 'Available'}</TableCell><TableCell>{editable && row.isActive && <Button onClick={() => setSelected(row)}>Adjust</Button>}</TableCell></TableRow>)}
        {!query.data.items.length && <TableRow><TableCell colSpan={history ? 7 : 6}>No matching records.</TableCell></TableRow>}
      </TableBody></Table></TableContainer><TablePagination component="div" count={query.data.totalCount} page={page} rowsPerPage={20} rowsPerPageOptions={[20]} onPageChange={(_, next) => setPage(next)} /></Paper>}
    {selected && <AdjustmentDialog balance={selected} onClose={() => { setSelected(null); query.refetch() }} />}
  </Stack>
}
