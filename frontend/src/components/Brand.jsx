import { Box, Stack, Typography } from '@mui/material'
import Inventory2OutlinedIcon from '@mui/icons-material/Inventory2Outlined'
export default function Brand({ light = false }) {
  return <Stack direction="row" spacing={1.5} alignItems="center">
    <Box sx={{ display: 'grid', placeItems: 'center', width: 39, height: 39, borderRadius: '12px', bgcolor: light ? '#d9edb5' : 'primary.main', color: light ? '#173d32' : '#fff' }}><Inventory2OutlinedIcon fontSize="small" /></Box>
    <Typography fontWeight={800} fontSize={24} letterSpacing={-0.8} color={light ? '#fff' : 'text.primary'}>StockPilot<span style={{ color: light ? '#d9edb5' : '#5d8b69' }}>.</span></Typography>
  </Stack>
}
