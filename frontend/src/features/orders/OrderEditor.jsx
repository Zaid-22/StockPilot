import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Alert, Autocomplete, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField, Typography } from '@mui/material'
import { apiMutation, apiRequest } from '../../lib/api'
import { useIdempotentMutation } from '../../lib/useIdempotentMutation'

function CatalogPicker({ kind, label, value, onChange, disabled }) {
  const [q, setQ] = useState('')
  const query = useQuery({ queryKey: ['order-options', kind, q], queryFn: () => apiRequest(`/catalog/${kind}?isActive=true&pageSize=20&q=${encodeURIComponent(q)}`) })
  const items = query.data?.items || []
  const options = value && !items.some(x => x.id === value.id) ? [value, ...items] : items
  return <Box sx={{ minWidth: 200, flex: 1 }}><Autocomplete options={options} value={value} disabled={disabled} loading={query.isPending} filterOptions={x => x}
    isOptionEqualToValue={(a, b) => a.id === b.id} getOptionLabel={x => x.sku ? `${x.sku} · ${x.name}` : x.name}
    onInputChange={(_, text, reason) => { if (reason === 'input') setQ(text) }} onChange={(_, item) => onChange(item)}
    renderInput={params => <TextField {...params} label={label} required error={!!query.error} helperText={query.error?.message || 'Search to find an active record (up to 20 matches).'} />} /></Box>
}

export default function OrderEditor({ kind, onClose, onSaved }) {
  const purchase = kind === 'purchases'
  const [party, setParty] = useState(null)
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState([{ key: crypto.randomUUID(), product: null, quantity: '1', unitPrice: '0' }])
  const mutation = useIdempotentMutation({ mutationFn: body => apiMutation(`/orders/${kind}`, body), onSuccess: onSaved })
  const { uncertain, locked } = mutation
  const valid = party && lines.every(x => x.product && Number.isInteger(Number(x.quantity)) && Number(x.quantity) > 0 && Number(x.quantity) <= 1000000 && /^\d+(\.\d{1,3})?$/.test(x.unitPrice) && Number(x.unitPrice) <= 999999999.999)
  const update = (key, patch) => setLines(rows => rows.map(row => row.key === key ? { ...row, ...patch } : row))
  function submit(event) {
    event.preventDefault()
    mutation.submit(() => ({ requestId: crypto.randomUUID(), partyId: party.id, notes, lines: lines.map(x => ({ productId: x.product.id, quantity: Number(x.quantity), unitPrice: Number(x.unitPrice) })) }))
  }
  return <Dialog open fullWidth maxWidth="md" onClose={locked ? undefined : onClose}><Box component="form" onSubmit={submit}>
    <DialogTitle>New {purchase ? 'purchase' : 'sales'} order</DialogTitle><DialogContent><Stack spacing={3} sx={{ pt: 1 }}>
      {mutation.error && <Alert severity="error">{mutation.error.message}{uncertain && ' Result unknown. Retry this request before making changes.'}</Alert>}
      <CatalogPicker kind={purchase ? 'suppliers' : 'customers'} label={purchase ? 'Supplier' : 'Customer'} value={party} onChange={setParty} disabled={locked} />
      {lines.map((line, index) => <Stack key={line.key} spacing={2} direction={{ xs: 'column', md: 'row' }} alignItems="flex-start">
        <CatalogPicker kind="products" label={`Product ${index + 1}`} value={line.product} disabled={locked} onChange={product => update(line.key, { product, ...(!purchase && product ? { unitPrice: String(product.sellingPrice) } : {}) })} />
        <TextField label={`Quantity ${index + 1}`} type="number" required value={line.quantity} disabled={locked} onChange={e => update(line.key, { quantity: e.target.value })} slotProps={{ htmlInput: { min: 1, max: 1000000, step: 1 } }} sx={{ width: 130 }} />
        <TextField label={`Unit price ${index + 1}`} type="number" required value={line.unitPrice} disabled={locked} onChange={e => update(line.key, { unitPrice: e.target.value })} slotProps={{ htmlInput: { min: 0, max: 999999999.999, step: 0.001 } }} sx={{ width: 150 }} />
        <Button aria-label={`Remove line ${index + 1}`} disabled={locked || lines.length === 1} onClick={() => setLines(rows => rows.filter(x => x.key !== line.key))}>Remove</Button>
      </Stack>)}
      <Button disabled={locked || lines.length >= 50} onClick={() => setLines(rows => [...rows, { key: crypto.randomUUID(), product: null, quantity: '1', unitPrice: '0' }])}>Add line</Button>
      <TextField label="Notes" multiline value={notes} disabled={locked} onChange={e => setNotes(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }} />
      <Typography variant="body2" color="text.secondary">The order starts as a draft. Review its details before confirming. To correct draft lines, cancel the draft and create another.</Typography>
    </Stack></DialogContent><DialogActions><Button disabled={locked} onClick={onClose}>Cancel</Button><Button type="submit" variant="contained" disabled={mutation.isPending || !valid}>{uncertain ? 'Retry creation' : 'Create draft'}</Button></DialogActions>
  </Box></Dialog>
}
