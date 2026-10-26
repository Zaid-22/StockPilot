import { useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Alert, Box, Button, Divider, IconButton, InputAdornment, Stack, TextField, Typography } from '@mui/material'
import VisibilityOutlinedIcon from '@mui/icons-material/VisibilityOutlined'
import VisibilityOffOutlinedIcon from '@mui/icons-material/VisibilityOffOutlined'
import Brand from '../../components/Brand'
import ThemeToggle from '../../components/ThemeToggle'
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
    <Box component="header" className="login-header"><Brand /><ThemeToggle /></Box>
    <Box component="main" className="login-form-panel">
      <Box sx={{ width: '100%', maxWidth: 410, mx: 'auto' }}>
        <Typography component="h1" variant="h4" fontWeight={700} sx={{ mb: 3 }}>Sign in</Typography>
        {auth.notice && <Alert severity="info" sx={{ mb: 2 }}>{auth.notice}</Alert>}
        {failure && <Alert severity="error" sx={{ mb: 2 }}>{failure}</Alert>}
        <Box component="form" onSubmit={handleSubmit(submit)} noValidate>
          <Stack spacing={2.5}>
            <TextField label="Email address" type="email" autoComplete="username" fullWidth {...register('email')} error={Boolean(errors.email)} helperText={errors.email?.message} />
            <TextField label="Password" type={showPassword ? 'text' : 'password'} autoComplete="current-password" fullWidth {...register('password')} error={Boolean(errors.password)} helperText={errors.password?.message}
              slotProps={{ input: { endAdornment: <InputAdornment position="end"><IconButton aria-label={showPassword ? 'Hide password' : 'Show password'} onClick={() => setShowPassword(!showPassword)} edge="end">{showPassword ? <VisibilityOffOutlinedIcon /> : <VisibilityOutlinedIcon />}</IconButton></InputAdornment> } }} />
            <Button type="submit" size="large" variant="contained" disabled={isSubmitting} sx={{ py: 1.7 }}>{isSubmitting ? 'Signing in…' : 'Sign in'}</Button>
          </Stack>
        </Box>
        <Divider sx={{ my: 4 }} />
        <Typography variant="body2" color="text.secondary" lineHeight={1.8}>Contact your administrator if you need an account.</Typography>
      </Box>
    </Box>
  </Box>
}
