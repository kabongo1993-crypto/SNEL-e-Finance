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

/** Variables CSS de la charte — famille unique `--ef-*`. */
function cssVars(c: EfColorTokens): Record<string, string> {
  return {
    '--ef-primary': c.primary,
    '--ef-primary-hover': c.primaryHover,
    '--ef-primary-soft': c.primarySoft,
    '--ef-primary-dark': c.primaryDark,
    '--ef-accent': c.accent,
    '--ef-accent-soft': c.accentSoft,
    '--ef-background': c.background,
    '--ef-surface': c.surface,
    '--ef-surface-secondary': c.surfaceSecondary,
    '--ef-surface-hover': c.surfaceHover,
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
    '--ef-border-subtle': c.borderSubtle,
    '--ef-success': c.success,
    '--ef-success-soft': c.successSoft,
    '--ef-warning': c.warning,
    '--ef-warning-soft': c.warningSoft,
    '--ef-danger': c.danger,
    '--ef-danger-soft': c.dangerSoft,
    '--ef-info': c.info,
    '--ef-info-soft': c.infoSoft,
    '--ef-disabled': c.disabled,
    '--ef-kpi-positive': c.kpiPositive,
    '--ef-kpi-negative': c.kpiNegative,
    '--ef-overlay': c.overlay,
    '--ef-chart-1': c.chartPrimary,
    '--ef-chart-2': c.chartSecondary,
    '--ef-chart-3': c.chartTertiary,
    '--ef-chart-4': c.chartMuted,
    '--ef-radius': `${efRadius.sm}px`,
    '--ef-radius-card': `${efRadius.md}px`,
    '--ef-radius-lg': `${efRadius.lg}px`,
    '--ef-transition': '180ms ease',
  };
}

function createEfTheme(mode: 'light' | 'dark') {
  const c: EfColorTokens = mode === 'light' ? efLightColors : efDarkColors;
  const isDark = mode === 'dark';
  const efSurfaceShadow = isDark ? efShadows.dark.md : efShadows.light.md;

  return createTheme({
    cssVariables: true,
    palette: {
      mode,
      primary: {
        main: c.primary,
        dark: c.primaryDark,
        light: c.primaryHover,
        contrastText: '#FFFFFF',
      },
      secondary: {
        main: c.primaryHover,
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
        disabled: c.disabled,
      },
      divider: c.border,
      action: {
        hover: isDark ? 'rgba(255,255,255,0.04)' : alpha(c.primary, 0.04),
        selected: isDark ? alpha(c.primary, 0.22) : alpha(c.primary, 0.08),
        disabled: c.disabled,
        disabledBackground: isDark ? alpha(c.disabled, 0.25) : '#E8ECF0',
      },
    },
    typography: {
      fontFamily: '"Inter", "IBM Plex Sans", "Segoe UI", sans-serif',
      h1: { fontSize: '1.875rem', fontWeight: 750, letterSpacing: '-0.022em', lineHeight: 1.22, color: c.text },
      h2: { fontSize: '1.5rem', fontWeight: 750, letterSpacing: '-0.022em', lineHeight: 1.28, color: c.text },
      h3: { fontSize: '1.25rem', fontWeight: 700, letterSpacing: '-0.014em', lineHeight: 1.32, color: c.text },
      // h4/h5 portent les montants : chiffres à largeur fixe pour aligner les colonnes.
      h4: {
        fontSize: '1.375rem',
        fontWeight: 750,
        letterSpacing: '-0.024em',
        lineHeight: 1.24,
        color: c.text,
        fontVariantNumeric: 'tabular-nums',
      },
      h5: {
        fontSize: '0.9375rem',
        fontWeight: 700,
        letterSpacing: '-0.01em',
        lineHeight: 1.38,
        color: c.text,
        fontVariantNumeric: 'tabular-nums',
      },
      h6: { fontSize: '0.875rem', fontWeight: 700, letterSpacing: '-0.006em', lineHeight: 1.4, color: c.text },
      subtitle1: { fontSize: '0.875rem', fontWeight: 650 },
      subtitle2: { fontSize: '0.75rem', fontWeight: 650 },
      body1: { fontSize: '0.8125rem', lineHeight: 1.55 },
      body2: { fontSize: '0.75rem', lineHeight: 1.5 },
      caption: { fontSize: '0.6875rem', lineHeight: 1.45 },
      overline: {
        fontSize: '0.6875rem',
        fontWeight: 650,
        letterSpacing: '0.06em',
        textTransform: 'uppercase',
      },
      button: { textTransform: 'none', fontWeight: 600, fontSize: '0.75rem' },
    },
    shape: { borderRadius: efRadius.sm },
    shadows: buildShadows(mode),
    components: {
      MuiCssBaseline: {
        styleOverrides: {
          ':root': cssVars(c),
          body: {
            backgroundColor: c.background,
            color: c.text,
          },
          '*:focus-visible': {
            outline: `2px solid ${c.primary}`,
            outlineOffset: 2,
          },
          '*::-webkit-scrollbar': { width: 10, height: 10 },
          '*::-webkit-scrollbar-track': { background: 'transparent' },
          '*::-webkit-scrollbar-thumb': {
            backgroundColor: alpha(c.textSecondary, isDark ? 0.35 : 0.28),
            borderRadius: 8,
            border: '2px solid transparent',
            backgroundClip: 'content-box',
          },
          '*::-webkit-scrollbar-thumb:hover': {
            backgroundColor: alpha(c.textSecondary, isDark ? 0.55 : 0.45),
          },
        },
      },
      MuiButton: {
        defaultProps: { disableElevation: true, size: 'small' },
        styleOverrides: {
          root: {
            borderRadius: efRadius.sm,
            minHeight: 40,
            paddingInline: 16,
            transition: 'background-color 150ms ease, border-color 150ms ease, color 150ms ease',
            '&.Mui-disabled': {
              backgroundColor: isDark ? alpha(c.disabled, 0.25) : '#E8ECF0',
              color: isDark ? c.textSecondary : '#8A9AAB',
              borderColor: 'transparent',
            },
          },
          // 36px au clavier/souris, 40px dès que le pointeur est grossier (tactile).
          sizeSmall: {
            minHeight: 36,
            fontSize: '0.8125rem',
            '@media (pointer: coarse)': { minHeight: 40 },
          },
          sizeMedium: { minHeight: 40 },
          contained: {
            '&.MuiButton-colorPrimary:hover': { backgroundColor: c.primaryHover },
          },
          outlined: {
            borderColor: c.primary,
            color: c.primary,
            backgroundColor: 'transparent',
            '&:hover': {
              borderColor: c.primaryHover,
              backgroundColor: alpha(c.primary, 0.06),
            },
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
            backgroundColor: c.surface,
            border: `1px solid ${c.borderSubtle}`,
            borderRadius: efRadius.md,
            boxShadow: efSurfaceShadow,
          },
        },
      },
      MuiCard: {
        defaultProps: { elevation: 0 },
        styleOverrides: {
          root: {
            backgroundImage: 'none',
            backgroundColor: c.surface,
            border: `1px solid ${c.borderSubtle}`,
            borderRadius: efRadius.md,
            boxShadow: efSurfaceShadow,
          },
        },
      },
      MuiDialog: {
        styleOverrides: {
          paper: {
            backgroundColor: c.surface,
            backgroundImage: 'none',
            border: `1px solid ${c.border}`,
            borderRadius: efRadius.lg,
          },
        },
      },
      MuiMenu: {
        styleOverrides: {
          paper: {
            backgroundColor: c.surface,
            backgroundImage: 'none',
            border: `1px solid ${c.border}`,
            borderRadius: efRadius.sm,
          },
        },
      },
      MuiPopover: {
        styleOverrides: {
          paper: {
            backgroundColor: c.surface,
            backgroundImage: 'none',
            border: `1px solid ${c.border}`,
            borderRadius: efRadius.sm,
          },
        },
      },
      MuiSelect: {
        styleOverrides: {
          select: {
            backgroundColor: c.surface,
          },
        },
      },
      MuiOutlinedInput: {
        styleOverrides: {
          root: {
            borderRadius: efRadius.sm,
            backgroundColor: c.surface,
            '& .MuiOutlinedInput-notchedOutline': {
              borderColor: c.border,
            },
            '&:hover .MuiOutlinedInput-notchedOutline': {
              borderColor: c.borderStrong,
            },
            '&.Mui-focused .MuiOutlinedInput-notchedOutline': {
              borderColor: c.primary,
              borderWidth: 1,
            },
            '&.Mui-focused': {
              boxShadow: `0 0 0 3px ${alpha(c.primary, 0.12)}`,
            },
            '&.Mui-disabled': {
              backgroundColor: c.surfaceSecondary,
            },
          },
          // Au doigt, les champs `size="small"` (~40px) sont trop bas : on rallonge.
          input: {
            color: c.text,
            '@media (pointer: coarse)': { paddingTop: 12, paddingBottom: 12 },
          },
        },
      },
      MuiMenuItem: {
        styleOverrides: {
          root: {
            borderRadius: efRadius.xs,
            marginInline: 4,
            '@media (pointer: coarse)': { minHeight: 44 },
          },
        },
      },
      MuiFilledInput: {
        styleOverrides: {
          root: {
            backgroundColor: c.surfaceSecondary,
            '&:hover': { backgroundColor: c.surfaceSecondary },
            '&.Mui-focused': { backgroundColor: c.surfaceSecondary },
          },
        },
      },
      MuiInputBase: {
        styleOverrides: {
          root: {
            color: c.text,
          },
        },
      },
      MuiInputLabel: {
        styleOverrides: {
          root: {
            color: c.textSecondary,
            fontWeight: 600,
            '&.Mui-focused': { color: c.primary },
          },
        },
      },
      MuiAlert: {
        styleOverrides: {
          root: {
            borderRadius: efRadius.md,
            border: `1px solid ${c.border}`,
            '&.MuiAlert-standardWarning': {
              backgroundColor: c.warningSoft,
              borderColor: isDark ? alpha(c.warning, 0.35) : '#F0D9A0',
              color: c.text,
              '& .MuiAlertTitle-root': { color: c.text, fontWeight: 700 },
            },
            '&.MuiAlert-standardInfo': {
              backgroundColor: c.infoSoft,
              borderColor: isDark ? alpha(c.primary, 0.35) : alpha(c.primary, 0.25),
            },
            '&.MuiAlert-standardSuccess': {
              backgroundColor: c.successSoft,
              borderColor: isDark ? alpha(c.success, 0.35) : alpha(c.success, 0.3),
            },
            '&.MuiAlert-standardError': {
              backgroundColor: c.dangerSoft,
              borderColor: isDark ? alpha(c.danger, 0.35) : alpha(c.danger, 0.3),
            },
          },
        },
      },
      MuiTableHead: {
        styleOverrides: {
          root: {
            '& .MuiTableCell-head': {
              backgroundColor: c.surface,
              color: c.textSecondary,
              fontWeight: 650,
              fontSize: '0.6875rem',
              letterSpacing: '0.01em',
              textTransform: 'none',
              borderBottom: `1px solid ${c.border}`,
              paddingTop: 10,
              paddingBottom: 10,
              whiteSpace: 'nowrap',
            },
          },
        },
      },
      MuiTableCell: {
        styleOverrides: {
          root: {
            borderColor: c.borderSubtle,
            fontSize: '0.75rem',
            paddingTop: 10,
            paddingBottom: 10,
            color: c.text,
            fontVariantNumeric: 'tabular-nums',
          },
        },
      },
      MuiTableRow: {
        styleOverrides: {
          root: {
            backgroundColor: c.surface,
            transition: 'background-color 120ms ease',
            '&.MuiTableRow-hover:hover': {
              backgroundColor: c.surfaceHover,
            },
          },
        },
      },
      MuiTableContainer: {
        styleOverrides: {
          root: {
            backgroundColor: c.surface,
          },
        },
      },
      MuiChip: {
        styleOverrides: {
          root: {
            fontWeight: 600,
            borderRadius: 999,
            height: 24,
            fontSize: '0.75rem',
          },
          filled: {
            '&.MuiChip-colorDefault': {
              backgroundColor: c.surfaceSecondary,
              color: c.text,
            },
          },
        },
      },
      MuiTextField: {
        defaultProps: { size: 'small' },
      },
      MuiTooltip: {
        styleOverrides: {
          tooltip: {
            backgroundColor: isDark ? c.surfaceSecondary : c.sidebar,
            fontSize: '0.75rem',
            borderRadius: efRadius.xs,
            paddingBlock: 6,
            paddingInline: 10,
          },
        },
      },
      MuiDrawer: {
        styleOverrides: {
          paper: {
            border: 'none',
            borderRadius: 0,
            backgroundImage: 'none',
            backgroundColor: c.sidebar,
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
