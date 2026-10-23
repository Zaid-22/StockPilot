import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Alert, Box, Button, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material'
import { apiRequest } from '../../lib/api'

export default function ReportsPage() {
  const [initial] = useState(() => {
    const to = new Date(); const from = new Date(to); from.setUTCDate(from.getUTCDate() - 29)
    return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) }
  })
  const [dates, setDates] = useState(initial)
  const [range, setRange] = useState(initial)
  const query = useQuery({ queryKey: ['reports', range], queryFn: () => apiRequest(`/reports/summary?${new URLSearchParams(range)}`) })
  const data = query.data
  const valid = dates.from && dates.to && dates.from <= dates.to && new Date(dates.to) - new Date(dates.from) <= 365 * 86400000
  return <Stack spacing={3}>
    <Box><Typography component="h1" variant="h4" fontWeight={700}>Activity reports</Typography><Typography color="text.secondary">Receipts, shipments, and adjustments by UTC date.</Typography></Box>
    <Stack component="form" direction={{ xs: 'column', sm: 'row' }} spacing={2} onSubmit={e => { e.preventDefault(); setRange({ ...dates }) }}>
      <TextField label="From (UTC)" type="date" required value={dates.from} onChange={e => setDates(values => ({ ...values, from: e.target.value }))} slotProps={{ inputLabel: { shrink: true } }} />
      <TextField label="To (UTC)" type="date" required value={dates.to} onChange={e => setDates(values => ({ ...values, to: e.target.value }))} slotProps={{ inputLabel: { shrink: true } }} />
      <Button type="submit" variant="contained" disabled={!valid}>Apply range</Button><Button onClick={() => query.refetch()}>Refresh</Button>
    </Stack>
    {!valid && <Alert severity="warning">Choose an inclusive range of up to 366 days.</Alert>}
    {query.isPending && <Typography role="status">Loading reports…</Typography>}{query.error && <Alert severity="error">{query.error.message}</Alert>}
    {data && <>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr 1fr', lg: 'repeat(4, 1fr)' }, gap: 2 }}>{[
        ['Units received', data.unitsReceived.toLocaleString()], ['Units shipped', data.unitsShipped.toLocaleString()],
        ['Received order value', data.receivedOrderValue.toFixed(3)], ['Shipped order value', data.shippedOrderValue.toFixed(3)],
      ].map(([label, value]) => <Paper key={label} variant="outlined" sx={{ p: 3 }}><Typography color="text.secondary" variant="body2">{label}</Typography><Typography variant="h5" fontWeight={700} sx={{ mt: 1, overflowWrap: 'anywhere' }}>{value}</Typography></Paper>)}</Box>
      <Alert severity="info">Values use quantities actually received or shipped at their order prices. They exclude payments, tax, returns, and profit calculations. All prices must use one consistent currency.</Alert>
      <Paper variant="outlined"><TableContainer sx={{ maxHeight: 520 }}><Table stickyHeader aria-label="Daily stock activity"><TableHead><TableRow>{['Date (UTC)', 'Received', 'Shipped', 'Adjustments (net)'].map(label => <TableCell key={label}>{label}</TableCell>)}</TableRow></TableHead><TableBody>{data.dailyStock.map(day => <TableRow key={day.date}><TableCell>{day.date}</TableCell><TableCell>{day.received}</TableCell><TableCell>{day.shipped}</TableCell><TableCell>{day.adjusted}</TableCell></TableRow>)}</TableBody></Table></TableContainer></Paper>
      <Typography variant="caption" color="text.secondary">Updated {new Date(data.generatedAtUtc).toLocaleString()}. Current inventory balances are available on Overview.</Typography>
    </>}
  </Stack>
}
