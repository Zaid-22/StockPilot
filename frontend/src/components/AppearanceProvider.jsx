import { useEffect, useMemo, useState } from 'react'
import { createTheme, CssBaseline, ThemeProvider } from '@mui/material'
import { AppearanceContext } from './appearanceContext'

const storageKey = 'stockpilot-color-mode'

function initialMode() {
  try {
    const saved = localStorage.getItem(storageKey)
    if (saved === 'light' || saved === 'dark') return saved
  } catch { /* Storage can be disabled by the browser. */ }
  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

export default function AppearanceProvider({ children }) {
  const [mode, setMode] = useState(initialMode)
  const theme = useMemo(() => createTheme({
    palette: {
      mode,
      primary: { main: mode === 'dark' ? '#91d5b0' : '#244f3d', contrastText: mode === 'dark' ? '#10251c' : '#ffffff' },
      background: { default: mode === 'dark' ? '#101714' : '#f7f8f4', paper: mode === 'dark' ? '#19241f' : '#ffffff' },
      text: { primary: mode === 'dark' ? '#e7f0eb' : '#21392e', secondary: mode === 'dark' ? '#a9bbb0' : '#68766e' },
      divider: mode === 'dark' ? '#34473c' : '#e1e6dd',
    },
    typography: { fontFamily: 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif', button: { textTransform: 'none', fontWeight: 600 } },
    shape: { borderRadius: 12 },
    components: {
      MuiButton: { defaultProps: { disableElevation: true } },
      MuiPaper: { styleOverrides: { root: { backgroundImage: 'none' }, outlined: ({ theme }) => ({ borderColor: theme.palette.divider }) } },
    },
  }), [mode])

  useEffect(() => {
    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', theme.palette.background.default)
  }, [theme])

  function toggleMode() {
    const next = mode === 'dark' ? 'light' : 'dark'
    setMode(next)
    try { localStorage.setItem(storageKey, next) } catch { /* The toggle still works without storage. */ }
  }

  return <AppearanceContext.Provider value={{ mode, toggleMode }}><ThemeProvider theme={theme}><CssBaseline enableColorScheme />{children}</ThemeProvider></AppearanceContext.Provider>
}
