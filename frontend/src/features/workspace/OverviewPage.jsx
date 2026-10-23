import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Alert, Box, Button, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material'
import { apiRequest } from '../../lib/api'
import { useAuth } from '../auth/authContext'

export default function OverviewPage() {
  const { user } = useAuth()
  const query = useQuery({ queryKey: ['summary'], queryFn: () => apiRequest('/reports/summary') })
  const data = query.data
  return <Stack spacing={3}>
    <Box><Typography variant="overline" color="text.secondary" sx={{ letterSpacing: 2 }}>OPERATIONS AT A GLANCE</Typography><Typography component="h1" variant="h3" sx={{ fontWeight: 700, letterSpacing: -1.5, mt: 1 }}>Your stock. Your next move.</Typography><Typography color="text.secondary" sx={{ mt: 2 }}>Welcome, {user.displayName || user.email}. Track availability, incoming deliveries, and customer shipments.</Typography></Box>
    <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap><Button component={Link} to="/inventory" variant="contained">Manage inventory</Button><Button component={Link} to="/purchases" variant="outlined">Purchasing</Button><Button component={Link} to="/sales" variant="outlined">Sales</Button><Button component={Link} to="/reports">View reports</Button></Stack>
    {query.isPending && <Typography role="status">Loading workspace summary…</Typography>}
    {query.error && <Alert severity="error" action={<Button onClick={() => query.refetch()}>Retry</Button>}>{query.error.message}</Alert>}
    {data && <>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr 1fr', lg: 'repeat(4, 1fr)' }, gap: 2 }}>
        {[['Active products', data.activeProducts], ['Units on hand', data.unitsOnHand], ['Low stock products', data.lowStockProducts], ['Open orders', data.openPurchaseOrders + data.openSalesOrders]].map(([label, value]) => <Paper key={label} variant="outlined" sx={{ p: 3 }}><Typography variant="body2" color="text.secondary">{label}</Typography><Typography variant="h3" sx={{ fontWeight: 700, mt: 1 }}>{value.toLocaleString()}</Typography></Paper>)}
      </Box>
      <Paper variant="outlined" sx={{ p: 3, bgcolor: '#edf3e8' }}><Typography variant="h6" fontWeight={700}>Keep deliveries moving</Typography><Typography sx={{ mt: 1 }}>{data.openPurchaseOrders} purchase orders awaiting receipt · {data.openSalesOrders} sales orders awaiting shipment</Typography><Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>Confirmed and partially fulfilled orders. Drafts are excluded.</Typography></Paper>
      <Paper variant="outlined"><Box sx={{ p: 3 }}><Typography variant="h6" fontWeight={700}>Stock needing attention</Typography><Typography variant="body2" color="text.secondary">Up to 10 active products at or below their reorder level.</Typography></Box><TableContainer><Table aria-label="Low stock products"><TableHead><TableRow><TableCell>Product</TableCell><TableCell>On hand</TableCell><TableCell>Reorder level</TableCell></TableRow></TableHead><TableBody>{data.lowStock.map(row => <TableRow key={row.productId}><TableCell>{row.sku} · {row.name}</TableCell><TableCell>{row.quantityOnHand}</TableCell><TableCell>{row.reorderLevel}</TableCell></TableRow>)}{!data.lowStock.length && <TableRow><TableCell colSpan={3}>No low-stock products.</TableCell></TableRow>}</TableBody></Table></TableContainer></Paper>
      <Typography variant="caption" color="text.secondary">Updated {new Date(data.generatedAtUtc).toLocaleString()} · Main warehouse</Typography>
    </>}
  </Stack>
}
