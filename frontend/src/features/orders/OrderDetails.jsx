import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material'
import { apiMutation, apiRequest } from '../../lib/api'

export default function OrderDetails({ kind, id, editable, onClose }) {
  const cache = useQueryClient()
  const [action, setAction] = useState('')
  const [quantities, setQuantities] = useState({})
  const [reference, setReference] = useState('')
  const [attempt, setAttempt] = useState(null)
  const purchase = kind === 'purchases'
  const query = useQuery({ queryKey: ['order', kind, id], queryFn: () => apiRequest(`/orders/${kind}/${id}`) })
  const mutation = useMutation({ mutationFn: request => apiMutation(request.path, request.body), onSuccess: async () => {
    setAction(''); setQuantities({}); setReference(''); setAttempt(null)
    await cache.invalidateQueries()
  } })
  const order = query.data
  const uncertain = mutation.error && (mutation.error.status === 0 || mutation.error.status >= 500)
  const locked = mutation.isPending || uncertain
  const fulfillment = action === 'receive' || action === 'ship'
  const positive = Object.entries(quantities).filter(([, value]) => Number(value) > 0)
  const valid = !fulfillment || (reference.trim() && positive.length && Object.values(quantities).every(value => Number.isInteger(Number(value)) && Number(value) >= 0))
  function submit(event) {
    event.preventDefault()
    const request = uncertain ? attempt : { path: `/orders/${kind}/${id}/${action}`, body: {
      requestId: crypto.randomUUID(), version: order.version,
      ...(fulfillment ? { reference, lines: positive.map(([lineId, quantity]) => ({ lineId, quantity: Number(quantity) })) } : {}),
    } }
    setAttempt(request); mutation.mutate(request)
  }
  return <Dialog open fullWidth maxWidth="md" onClose={locked ? undefined : onClose}><DialogTitle>{order?.number || 'Order details'}</DialogTitle><DialogContent>
    {query.isPending && <Typography role="status">Loading order…</Typography>}
    {query.error && <Alert severity="error">{query.error.message}<Button onClick={() => query.refetch()}>Retry</Button></Alert>}
    {order && <Stack spacing={2}>
      <Typography>{order.partyName} · {order.status} · {new Date(order.createdAtUtc).toLocaleDateString()}</Typography>
      {order.notes && <Typography>{order.notes}</Typography>}
      <TableContainer><Table aria-label="Order lines"><TableHead><TableRow>{['Product', 'Ordered', purchase ? 'Received' : 'Shipped', 'Unit price', 'Line total'].map(x => <TableCell key={x}>{x}</TableCell>)}</TableRow></TableHead><TableBody>{order.lines.map(line => <TableRow key={line.id}><TableCell>{line.sku} · {line.productName}</TableCell><TableCell>{line.quantity}</TableCell><TableCell>{line.processedQuantity}</TableCell><TableCell>{line.unitPrice.toFixed(3)}</TableCell><TableCell>{line.total.toFixed(3)}</TableCell></TableRow>)}</TableBody></Table></TableContainer>
      <Typography fontWeight={700}>Order total: {order.total.toFixed(3)}</Typography>
      {mutation.error && <Alert severity="error">{mutation.error.message}{uncertain ? ' Result unknown. Retry this request before making changes.' : <Button onClick={async () => { await query.refetch(); mutation.reset(); setAction(''); setQuantities({}) }}>Reload order</Button>}</Alert>}
      {editable && !action && <Stack direction="row" spacing={1}>
        {order.status === 'Draft' && <Button variant="contained" onClick={() => setAction('confirm')}>Confirm order</Button>}
        {['Draft', 'Confirmed'].includes(order.status) && <Button color="error" onClick={() => setAction('cancel')}>Cancel order</Button>}
        {['Confirmed', 'Partial'].includes(order.status) && <Button variant="contained" onClick={() => setAction(purchase ? 'receive' : 'ship')}>{purchase ? 'Receive stock' : 'Ship order'}</Button>}
      </Stack>}
      {action && <Box component="form" onSubmit={submit}><Stack spacing={2}>
        <Typography fontWeight={700}>{fulfillment ? purchase ? 'Record received quantities' : 'Record shipped quantities' : action === 'confirm' ? 'Confirm this order?' : 'Cancel this order? This cannot be undone.'}</Typography>
        {fulfillment && <>
          <TextField label={purchase ? 'Delivery reference' : 'Shipment reference'} required value={reference} disabled={locked} onChange={e => setReference(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }} />
          {order.lines.filter(x => x.processedQuantity < x.quantity).map(line => <TextField key={line.id} label={`${line.sku} quantity`} type="number" value={quantities[line.id] ?? '0'} disabled={locked} onChange={e => setQuantities(values => ({ ...values, [line.id]: e.target.value }))} helperText={`${line.quantity - line.processedQuantity} outstanding; use 0 to skip this line.`} slotProps={{ htmlInput: { min: 0, max: line.quantity - line.processedQuantity, step: 1 } }} />)}
        </>}
        <Stack direction="row" spacing={1}><Button type="submit" variant="contained" disabled={mutation.isPending || !valid}>{uncertain ? 'Retry request' : fulfillment ? purchase ? 'Post receipt' : 'Post shipment' : 'Confirm action'}</Button><Button disabled={locked} onClick={() => setAction('')}>Back</Button></Stack>
      </Stack></Box>}
    </Stack>}
  </DialogContent><DialogActions><Button disabled={locked} onClick={onClose}>Close</Button></DialogActions></Dialog>
}
