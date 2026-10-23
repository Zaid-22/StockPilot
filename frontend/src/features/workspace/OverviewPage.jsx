import { Link } from 'react-router-dom'
import { Box, Button, Chip, Paper, Stack, Typography } from '@mui/material'
import Inventory2OutlinedIcon from '@mui/icons-material/Inventory2Outlined'
import ShoppingBagOutlinedIcon from '@mui/icons-material/ShoppingBagOutlined'
import LocalShippingOutlinedIcon from '@mui/icons-material/LocalShippingOutlined'
import { useAuth } from '../auth/authContext'

const modules = [
  { title: 'Product catalog', description: 'A home for your products, categories, and stock details.', Icon: Inventory2OutlinedIcon, path: "/catalog/products" },
  { title: 'Purchasing', description: 'Keep supplier orders and incoming deliveries organized.', Icon: ShoppingBagOutlinedIcon },
  { title: 'Sales', description: 'Follow customer orders from confirmation to shipment.', Icon: LocalShippingOutlinedIcon },
]
export default function OverviewPage() {
  const { user } = useAuth()
  return <>
    <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: 2 }}>GOOD TO SEE YOU</Typography>
    <Typography component="h1" variant="h3" sx={{ fontWeight: 700, letterSpacing: -1.5, mt: 1, mb: 2 }}>Your workspace is ready.</Typography>
    <Typography color="text.secondary" sx={{ maxWidth: 650, mb: 5 }}>Welcome, {user.displayName || user.email}. Your account is connected. This is where your team will manage its everyday operations.</Typography>
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'repeat(3, 1fr)' }, gap: 2.5 }}>
      {modules.map(({ title, description, Icon, path }) => <Paper key={title} variant="outlined" sx={{ p: 3.5 }}><Icon sx={{ mb: 3, color: 'primary.main', fontSize: 30 }} /><Typography variant="h6" fontWeight={700}>{title}</Typography><Typography variant="body2" color="text.secondary" sx={{ mt: 1, mb: 3, lineHeight: 1.8 }}>{description}</Typography>{path ? <Button component={Link} to={path} variant="outlined">Open catalog</Button> : <Chip label="Coming soon" size="small" sx={{ bgcolor: '#f0f3ed' }} />}</Paper>)}
    </Box>
    <Paper variant="outlined" sx={{ p: 3.5, mt: 3, bgcolor: '#edf3e8', borderColor: '#dae4d2' }}><Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" spacing={2}><Box><Typography fontWeight={700}>Your account</Typography><Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>{user.email}</Typography></Box><Stack direction="row" spacing={1} alignItems="center">{user.roles.map(role => <Chip key={role} label={role} color="primary" variant="outlined" />)}</Stack></Stack></Paper>
  </>
}
