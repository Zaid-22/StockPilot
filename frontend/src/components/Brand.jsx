import { Box, Stack, Typography } from '@mui/material'

export default function Brand({ light = false }) {
  return <Stack direction="row" spacing={1.25} alignItems="center" sx={{ flexShrink: 0 }}>
    <Box sx={{
      display: 'grid', placeItems: 'center', flexShrink: 0,
      width: 48, height: 48, borderRadius: '14px',
      bgcolor: light ? '#f1f6e9' : '#edf3e6',
      border: '1px solid', borderColor: light ? '#ffffff40' : '#dce7d2',
    }}>
      <Box component="img" src="/stockpilot-mark.png" alt="" aria-hidden="true"
        width={46} height={46} sx={{ display: 'block', objectFit: 'contain' }} />
    </Box>
    <Box>
      <Typography component="span" sx={{
        display: 'block', fontSize: 23, fontWeight: 800, lineHeight: 1.15,
        letterSpacing: '-1px', whiteSpace: 'nowrap',
        color: light ? '#fff' : 'text.primary',
      }}>Stock<Box component="span" sx={{ fontWeight: 500, color: light ? '#d9edb5' : 'primary.main' }}>Pilot</Box></Typography>
      <Typography component="span" sx={{
        display: 'block', mt: 0.6, fontSize: 8, fontWeight: 700,
        letterSpacing: '1.55px', lineHeight: 1.2, whiteSpace: 'nowrap',
        color: light ? '#b6cec2' : 'text.secondary',
      }}>INVENTORY & OPERATIONS</Typography>
    </Box>
  </Stack>
}
