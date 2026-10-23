import { useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Alert, Box, Button, Chip, Divider, IconButton, InputAdornment, Stack, TextField, Typography } from '@mui/material'
import ArrowForwardIcon from '@mui/icons-material/ArrowForward'
import VisibilityOutlinedIcon from '@mui/icons-material/VisibilityOutlined'
import VisibilityOffOutlinedIcon from '@mui/icons-material/VisibilityOffOutlined'
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline'
import Brand from '../../components/Brand'
import { useAuth } from './authContext'
import { loginSchema } from './loginSchema'

export default function LoginPage() {
  const auth = useAuth()
  const location = useLocation()
  const [showPassword, setShowPassword] = useState(false)
  const [failure, setFailure] = useState('')
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm({
    resolver: zodResolver(loginSchema), defaultValues: { email: '', password: '' },
  })
  const next = location.state?.from
  if (auth.user) return <Navigate to={typeof next === 'string' && next.startsWith('/') && !next.startsWith('//') && next !== '/login' ? next : '/'} replace />

  async function submit(values) {
    setFailure('')
    try { await auth.login(values) }
    catch (error) { setFailure(error.message) }
  }

  return <Box className="login-shell">
    <Box className="login-story">
      <Brand light />
      <Box sx={{ my: 'auto', py: 7 }}>
        <Chip label="A clearer view of your business" sx={{ mb: 3, color: '#d9edb5', bgcolor: '#ffffff10', border: '1px solid #ffffff20' }} />
        <Typography component="h1" sx={{ fontSize: { xs: 38, lg: 56 }, fontWeight: 700, letterSpacing: '-2px', lineHeight: 1.08, maxWidth: 500 }}>Every item.<br />Every movement.<br /><Box component="span" sx={{ color: '#d9edb5' }}>All in one place.</Box></Typography>
        <Typography sx={{ color: '#b6cec2', maxWidth: 390, mt: 3, lineHeight: 1.8 }}>Keep your inventory, purchasing, and sales connected. Give your team a workspace they can count on.</Typography>
        <Box className="stock-illustration" aria-hidden="true"><div className="crate crate-one">01</div><div className="crate crate-two">02</div><div className="crate crate-three">03</div><div className="illustration-line" /></Box>
        <Stack direction="row" alignItems="center" spacing={1} sx={{ color: '#d9edb5', mt: 2 }}><CheckCircleOutlineIcon fontSize="small" /><Typography variant="body2">A shared workspace. Clear responsibilities.</Typography></Stack>
      </Box>
      <Typography variant="caption" sx={{ color: '#93b6a5' }}>STOCKPILOT / INVENTORY & OPERATIONS</Typography>
    </Box>
    <Box className="login-form-panel">
      <Box sx={{ display: { xs: 'block', md: 'none' }, mb: 5 }}><Brand /></Box>
      <Box sx={{ width: '100%', maxWidth: 410, mx: 'auto' }}>
        <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: 2 }}>YOUR WORKSPACE</Typography>
        <Typography component="h2" variant="h3" sx={{ mt: 1, mb: 1.5, fontWeight: 700, letterSpacing: -1.5 }}>Welcome back.</Typography>
        <Typography color="text.secondary" sx={{ mb: 4 }}>Sign in to keep things moving.</Typography>
        {auth.notice && <Alert severity="info" sx={{ mb: 2 }}>{auth.notice}</Alert>}
        {failure && <Alert severity="error" sx={{ mb: 2 }}>{failure}</Alert>}
        <Box component="form" onSubmit={handleSubmit(submit)} noValidate>
          <Stack spacing={2.5}>
            <TextField label="Email address" type="email" autoComplete="username" fullWidth {...register('email')} error={Boolean(errors.email)} helperText={errors.email?.message} />
            <TextField label="Password" type={showPassword ? 'text' : 'password'} autoComplete="current-password" fullWidth {...register('password')} error={Boolean(errors.password)} helperText={errors.password?.message}
              slotProps={{ input: { endAdornment: <InputAdornment position="end"><IconButton aria-label={showPassword ? 'Hide password' : 'Show password'} onClick={() => setShowPassword(!showPassword)} edge="end">{showPassword ? <VisibilityOffOutlinedIcon /> : <VisibilityOutlinedIcon />}</IconButton></InputAdornment> } }} />
            <Button type="submit" size="large" variant="contained" disabled={isSubmitting} endIcon={!isSubmitting && <ArrowForwardIcon />} sx={{ py: 1.7 }}>{isSubmitting ? 'Signing in…' : 'Sign in'}</Button>
          </Stack>
        </Box>
        <Divider sx={{ my: 4 }} />
        <Typography variant="body2" color="text.secondary" lineHeight={1.8}>Need access? Contact your workspace administrator to set up your account.</Typography>
      </Box>
      <Typography variant="caption" color="text.secondary" sx={{ mt: 5, textAlign: 'center' }}>Your operations, organized.</Typography>
    </Box>
  </Box>
}
