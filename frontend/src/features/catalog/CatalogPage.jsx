import { useState } from 'react'
import { useParams, useSearchParams } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TablePagination, TextField, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import { apiMutation, apiRequest } from '../../lib/api'
import { useAuth } from '../auth/authContext'
import CatalogEditor from './CatalogEditor'
import { catalogs, categoryOptions } from './catalog'

export default function CatalogPage() {
  const { kind } = useParams()
  if (!Object.hasOwn(catalogs, kind)) return <Typography component="h1">Catalog not found</Typography>
  return <CatalogContent key={kind} kind={kind} />
}

function CatalogContent({ kind }) {
  const definition = catalogs[kind]
  const product = kind === 'products'
  const { user } = useAuth()
  const canEdit = user.roles.some(role => ['Admin', 'Staff'].includes(role))
  const queryClient = useQueryClient()
  const [params, setParams] = useSearchParams()
  const page = Math.min(100000, Math.max(1, Math.floor(Number(params.get('page')) || 1)))
  const pageSize = [10, 20, 50].includes(Number(params.get('pageSize'))) ? Number(params.get('pageSize')) : 20
  const [search, setSearch] = useState(params.get('q') ?? '')
  const [editor, setEditor] = useState(null)
  const [details, setDetails] = useState(null)
  const [confirm, setConfirm] = useState(null)
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  for (const key of ['q', 'isActive', 'categoryId']) if (params.get(key)) query.set(key, params.get(key))
  const records = useQuery({ queryKey: ['catalog', kind, query.toString()], queryFn: () => apiRequest(`/catalog/${kind}?${query}`), retry: false })
  const categories = useQuery({ queryKey: ['catalog', 'category-options'], queryFn: categoryOptions, enabled: product, retry: false })
  function filter(values) {
    const next = new URLSearchParams(params)
    next.set('page', '1')
    for (const [key, value] of Object.entries(values)) value ? next.set(key, String(value)) : next.delete(key)
    setParams(next)
  }
  async function changeStatus() {
    setBusy(true); setError('')
    try {
      await apiMutation(`/catalog/${kind}/${confirm.id}`, { ...confirm, isActive: !confirm.isActive }, 'PUT')
      await queryClient.invalidateQueries({ queryKey: ['catalog'] })
      setNotice(`${definition.singular[0].toUpperCase()}${definition.singular.slice(1)} ${confirm.isActive ? 'deactivated' : 'reactivated'}.`)
      setConfirm(null)
    } catch (failure) { setError(failure.message) }
    finally { setBusy(false) }
  }
  return <>
    <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" spacing={2} sx={{ mb: 3 }}>
      <Box><Typography component="h1" variant="h4" fontWeight={700}>{definition.title}</Typography><Typography color="text.secondary" sx={{ mt: 1 }}>{definition.description}</Typography></Box>
      {canEdit && <Button variant="contained" startIcon={<AddIcon />} onClick={() => { setError(''); setEditor({ id: null }) }} sx={{ alignSelf: 'flex-start' }} disabled={product && (categories.isPending || Boolean(categories.error))}>Add {definition.singular}</Button>}
    </Stack>
    {!canEdit && <Alert severity="info" sx={{ mb: 2 }}>You have read-only access to the catalog.</Alert>}
    {notice && <Alert severity="success" onClose={() => setNotice('')} sx={{ mb: 2 }}>{notice}</Alert>}
    {error && !confirm && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
    {product && categories.error && <Alert severity="error" sx={{ mb: 2 }} action={<Button onClick={() => categories.refetch()}>Retry</Button>}>Categories could not be loaded: {categories.error.message}</Alert>}
    <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
      <Stack component="form" direction={{ xs: 'column', md: 'row' }} spacing={2} onSubmit={event => { event.preventDefault(); filter({ q: search.trim() }) }}>
        <TextField label={product ? 'Search name or SKU' : 'Search name'} value={search} onChange={event => setSearch(event.target.value)} slotProps={{ htmlInput: { maxLength: 160 } }} size="small" sx={{ flex: 1 }} />
        <TextField select label="Status" size="small" value={params.get('isActive') ?? ''} slotProps={{ select: { native: true } }} onChange={event => filter({ isActive: event.target.value })} sx={{ minWidth: 140 }}><option value="">All statuses</option><option value="true">Active</option><option value="false">Inactive</option></TextField>
        {product && <TextField select label="Category filter" size="small" value={params.get('categoryId') ?? ''} slotProps={{ select: { native: true } }} onChange={event => filter({ categoryId: event.target.value })} sx={{ minWidth: 170 }}><option value="">All categories</option>{categories.data?.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</TextField>}
        <Button type="submit" variant="outlined">Search</Button>
      </Stack>
    </Paper>
    {records.isPending && <Typography role="status" sx={{ p: 3 }}>Loading {kind}…</Typography>}
    {records.error && <Alert severity="error" action={<Button onClick={() => records.refetch()}>Retry</Button>}>{records.error.message}</Alert>}
    {records.data && <Paper variant="outlined"><TableContainer><Table aria-label={definition.title}><TableHead><TableRow>{product && <TableCell>SKU</TableCell>}<TableCell>Name</TableCell>{product ? <><TableCell>Category</TableCell><TableCell align="right">Price</TableCell><TableCell align="right">Stock</TableCell></> : kind !== 'categories' && <TableCell>Email / phone</TableCell>}<TableCell>Status</TableCell><TableCell align="right">Actions</TableCell></TableRow></TableHead><TableBody>
      {records.data.items.map(record => <TableRow key={record.id} hover>{product && <TableCell sx={{ fontFamily: 'monospace' }}>{record.sku}</TableCell>}<TableCell>{record.name}</TableCell>{product ? <><TableCell>{record.categoryName}</TableCell><TableCell align="right">{Number(record.sellingPrice).toFixed(3)}</TableCell><TableCell align="right">{record.quantityOnHand}</TableCell></> : kind !== 'categories' && <TableCell>{record.email || '—'}<Typography variant="caption" display="block">{record.phone || '—'}</Typography></TableCell>}<TableCell><Chip label={record.isActive ? 'Active' : 'Inactive'} color={record.isActive ? 'success' : 'default'} size="small" variant="outlined" /></TableCell><TableCell align="right"><Stack direction="row" justifyContent="flex-end" spacing={1}><Button size="small" onClick={() => setDetails(record)}>View</Button>{canEdit && <><Button size="small" onClick={() => setEditor({ id: record.id })}>Edit</Button><Button size="small" onClick={() => { setError(''); setConfirm(record) }}>{record.isActive ? 'Deactivate' : 'Reactivate'}</Button></>}</Stack></TableCell></TableRow>)}
      {records.data.items.length === 0 && <TableRow><TableCell colSpan={product ? 7 : 4} sx={{ py: 5, textAlign: 'center' }}>No {kind} found. {canEdit ? `Add a ${definition.singular} or adjust your search.` : 'Try adjusting your search.'}{page > 1 && <Button onClick={() => filter({ page: 1 })}>Back to first page</Button>}</TableCell></TableRow>}
    </TableBody></Table></TableContainer><TablePagination component="div" count={records.data.totalCount} page={Math.min(page - 1, Math.max(0, Math.ceil(records.data.totalCount / pageSize) - 1))} rowsPerPage={pageSize} rowsPerPageOptions={[10, 20, 50]} onPageChange={(_, index) => filter({ page: index + 1 })} onRowsPerPageChange={event => filter({ pageSize: event.target.value })} /></Paper>}
    {editor && <CatalogEditor kind={kind} id={editor.id} categories={categories.data ?? []} onClose={() => setEditor(null)} onSaved={setNotice} />}
    <Dialog open={Boolean(confirm)} onClose={() => { if (!busy) setConfirm(null) }}><DialogTitle>{confirm?.isActive ? 'Deactivate' : 'Reactivate'} {definition.singular}?</DialogTitle><DialogContent>{error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}<Typography>{confirm?.name}</Typography><Typography color="text.secondary" sx={{ mt: 1 }}>The record and its history will be preserved.</Typography></DialogContent><DialogActions><Button onClick={() => setConfirm(null)} disabled={busy}>Cancel</Button><Button onClick={changeStatus} disabled={busy} variant="contained">{busy ? 'Saving…' : 'Confirm'}</Button></DialogActions></Dialog>
    <Dialog open={Boolean(details)} onClose={() => setDetails(null)} fullWidth maxWidth="sm"><DialogTitle>{details?.name}</DialogTitle><DialogContent><Stack spacing={2}>{product ? <><Typography>SKU: {details?.sku}</Typography><Typography>Category: {details?.categoryName}</Typography><Typography>Selling price: {details?.sellingPrice}</Typography><Typography>Stock: {details?.quantityOnHand} units · Reorder level: {details?.reorderLevel}</Typography><Typography sx={{ whiteSpace: 'pre-wrap' }}>{details?.description || 'No description.'}</Typography></> : kind !== 'categories' && <><Typography>Email: {details?.email || '—'}</Typography><Typography>Phone: {details?.phone || '—'}</Typography><Typography>Address: {details?.address || '—'}</Typography></>}<Typography>Status: {details?.isActive ? 'Active' : 'Inactive'}</Typography></Stack></DialogContent><DialogActions><Button onClick={() => setDetails(null)}>Close</Button></DialogActions></Dialog>
  </>
}
