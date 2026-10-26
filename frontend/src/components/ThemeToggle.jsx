import { useContext } from 'react'
import { IconButton, Tooltip } from '@mui/material'
import DarkModeOutlinedIcon from '@mui/icons-material/DarkModeOutlined'
import LightModeOutlinedIcon from '@mui/icons-material/LightModeOutlined'
import { AppearanceContext } from './appearanceContext'

export default function ThemeToggle() {
  const appearance = useContext(AppearanceContext)
  if (!appearance) return null
  const dark = appearance.mode === 'dark'
  const label = dark ? 'Switch to light mode' : 'Switch to dark mode'
  return <Tooltip title={label}><IconButton onClick={appearance.toggleMode} aria-label={label} color="primary" sx={{ border: 1, borderColor: 'divider', width: 44, height: 44, flexShrink: 0 }}>{dark ? <LightModeOutlinedIcon /> : <DarkModeOutlinedIcon />}</IconButton></Tooltip>
}
