import { createTheme, alpha, type Theme } from '@mui/material/styles';
import {
  efDarkColors,
  efLightColors,
  efRadius,
  efShadows,
  type EfColorTokens,
} from './tokens';

function buildShadows(mode: 'light' | 'dark'): Theme['shadows'] {
  const s = mode === 'light' ? efShadows.light : efShadows.dark;
  const shadows = Array(25).fill('none') as Theme['shadows'];
  shadows[1] = s.sm;
  shadows[2] = s.md;
  shadows[3] = s.md;
  shadows[4] = s.lg;
  for (let i = 5; i < 25; i++) shadows[i] = s.lg;
  return shadows;
}

function createEfTheme(mode: 'light' | 'dark') {
  const c: EfColorTokens = mode === 'light' ? efLightColors : efDarkColors;
  const isDark = mode === 'dark';

  return createTheme({
    cssVariables: true,
    palette: {
      mode,
      primary: {
        main: c.primary,
        dark: c.primaryHover,
        light: c.accent,
        contrastText: '#FFFFFF',
      },
      secondary: {
        main: c.accent,
        contrastText: '#FFFFFF',
      },
      success: { main: c.success },
      warning: { main: c.warning },
      error: { main: c.danger },
      info: { main: c.info },
      background: {
        default: c.background,
        paper: c.surface,
      },
      text: {
        primary: c.text,
        secondary: c.textSecondary,
      },
      divider: c.border,
      action: {
        hover: isDark ? 'rgba(255,255,255,0.04)' : 'rgba(26,75,114,0.04)',
        selected: isDark ? 'rgba(74,144,194,0.16)' : 'rgba(26,75,114,0.08)',
      },
    },
    typography: {
      fontFamily: '"IBM Plex Sans", "Segoe UI", sans-serif',
      h1: { fontSize: '1.75rem', fontWeight: 650, letterSpacing: '-0.02em', lineHeight: 1.25 },
      h2: { fontSize: '1.5rem', fontWeight: 650, letterSpacing: '-0.02em', lineHeight: 1.3 },
      h3: { fontSize: '1.25rem', fontWeight: 650, letterSpacing: '-0.01em', lineHeight: 1.35 },
      h4: { fontSize: '1.25rem', fontWeight: 650, letterSpacing: '-0.01em', lineHeight: 1.35 },
      h5: { fontSize: '1.05rem', fontWeight: 650, lineHeight: 1.4 },
      h6: { fontSize: '0.95rem', fontWeight: 650, lineHeight: 1.4 },
      subtitle1: { fontSize: '0.9rem', fontWeight: 600 },
      subtitle2: { fontSize: '0.8125rem', fontWeight: 600 },
      body1: { fontSize: '0.875rem', lineHeight: 1.5 },
      body2: { fontSize: '0.8125rem', lineHeight: 1.45 },
      caption: { fontSize: '0.75rem', lineHeight: 1.4 },
      overline: {
        fontSize: '0.6875rem',
        fontWeight: 650,
        letterSpacing: '0.06em',
        textTransform: 'uppercase',
      },
      button: { textTransform: 'none', fontWeight: 600, fontSize: '0.8125rem' },
    },
    shape: { borderRadius: efRadius.sm },
    shadows: buildShadows(mode),
    components: {
      MuiCssBaseline: {
        styleOverrides: {
          ':root': {
            '--ef-primary': c.primary,
            '--ef-primary-hover': c.primaryHover,
            '--ef-primary-soft': c.primarySoft,
            '--ef-accent': c.accent,
            '--ef-background': c.background,
            '--ef-surface': c.surface,
            '--ef-surface-secondary': c.surfaceSecondary,
            '--ef-sidebar': c.sidebar,
            '--ef-sidebar-text': c.sidebarText,
            '--ef-sidebar-text-muted': c.sidebarTextMuted,
            '--ef-sidebar-active': c.sidebarActive,
            '--ef-sidebar-hover': c.sidebarHover,
            '--ef-topbar': c.topbar,
            '--ef-text': c.text,
            '--ef-text-secondary': c.textSecondary,
            '--ef-border': c.border,
            '--ef-border-strong': c.borderStrong,
            '--ef-success': c.success,
            '--ef-warning': c.warning,
            '--ef-danger': c.danger,
            '--ef-info': c.info,
            '--ef-chart-1': c.chartPrimary,
            '--ef-chart-2': c.chartSecondary,
            '--ef-chart-3': c.chartTertiary,
            '--ef-chart-4': c.chartMuted,
            '--ef-radius': `${efRadius.sm}px`,
            '--ef-transition': '180ms ease',
          },
          body: {
            backgroundColor: c.background,
            color: c.text,
          },
          '*:focus-visible': {
            outline: `2px solid ${c.accent}`,
            outlineOffset: 2,
          },
        },
      },
      MuiButton: {
        defaultProps: { disableElevation: true, size: 'small' },
        styleOverrides: {
          root: {
            borderRadius: efRadius.sm,
            minHeight: 32,
            paddingInline: 12,
            transition: 'background-color 150ms ease, border-color 150ms ease, color 150ms ease',
          },
          sizeSmall: { minHeight: 30, fontSize: '0.8125rem' },
          sizeMedium: { minHeight: 34 },
          contained: {
            '&.MuiButton-colorPrimary:hover': { backgroundColor: c.primaryHover },
          },
        },
      },
      MuiIconButton: {
        styleOverrides: {
          root: {
            borderRadius: efRadius.sm,
            transition: 'background-color 150ms ease',
          },
        },
      },
      MuiPaper: {
        defaultProps: { elevation: 0 },
        styleOverrides: {
          root: {
            backgroundImage: 'none',
            border: `1px solid ${c.border}`,
            borderRadius: efRadius.md,
          },
        },
      },
      MuiTableHead: {
        styleOverrides: {
          root: {
            '& .MuiTableCell-head': {
              backgroundColor: isDark ? c.surfaceSecondary : alpha(c.primary, 0.04),
              color: c.textSecondary,
              fontWeight: 650,
              fontSize: '0.6875rem',
              letterSpacing: '0.04em',
              textTransform: 'uppercase',
              borderBottom: `1px solid ${c.border}`,
              paddingTop: 8,
              paddingBottom: 8,
              whiteSpace: 'nowrap',
            },
          },
        },
      },
      MuiTableCell: {
        styleOverrides: {
          root: {
            borderColor: c.border,
            fontSize: '0.8125rem',
            paddingTop: 7,
            paddingBottom: 7,
          },
        },
      },
      MuiTableRow: {
        styleOverrides: {
          root: {
            transition: 'background-color 120ms ease',
            '&.MuiTableRow-hover:hover': {
              backgroundColor: isDark ? 'rgba(255,255,255,0.03)' : alpha(c.primary, 0.03),
            },
          },
        },
      },
      MuiChip: {
        styleOverrides: {
          root: {
            fontWeight: 600,
            borderRadius: efRadius.xs,
            height: 22,
            fontSize: '0.6875rem',
          },
        },
      },
      MuiTextField: {
        defaultProps: { size: 'small' },
      },
      MuiTooltip: {
        styleOverrides: {
          tooltip: {
            backgroundColor: isDark ? '#1E2A3A' : c.sidebar,
            fontSize: '0.75rem',
          },
        },
      },
      MuiDrawer: {
        styleOverrides: {
          paper: {
            borderRight: 'none',
            backgroundImage: 'none',
          },
        },
      },
      MuiAppBar: {
        styleOverrides: {
          root: { backgroundImage: 'none' },
        },
      },
      MuiListItemButton: {
        styleOverrides: {
          root: {
            borderRadius: efRadius.sm,
            transition: 'background-color 150ms ease, color 150ms ease',
          },
        },
      },
    },
  });
}

export const lightTheme = createEfTheme('light');
export const darkTheme = createEfTheme('dark');

/** @deprecated Prefer ThemeModeProvider */
export const theme = lightTheme;
