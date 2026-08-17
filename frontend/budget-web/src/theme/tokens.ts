/**
 * e-finance design tokens — single source of truth.
 * Pages must not invent arbitrary colors, radii, or shadows.
 */

/** Nom affiché dans l’interface (sidebar, textes). */
export const BRAND_NAME = 'e-finance' as const;

/** Titre exact de l’onglet navigateur / document. */
export const DOCUMENT_TITLE = 'SNEL e-Finance' as const;

/** Chemin public du logo officiel SNEL. */
export const SNEL_LOGO_SRC = '/branding/snel-logo.png' as const;

export const efSpacing = {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 24,
  xxl: 32,
} as const;

export const efRadius = {
  xs: 4,
  sm: 6,
  md: 8,
  lg: 10,
} as const;

export const efZIndex = {
  sidebar: 1200,
  topbar: 1100,
  dropdown: 1300,
  modal: 1400,
  toast: 1500,
} as const;

export const efTransition = {
  fast: '120ms ease',
  normal: '180ms ease',
  slow: '240ms ease',
} as const;

/** Semantic color sets — designed independently for light and dark (not auto-inverted). */
export const efLightColors = {
  primary: '#1A4B72',
  primaryHover: '#153D5E',
  primarySoft: '#E8F1F8',
  accent: '#2A6FA8',
  background: '#EEF2F6',
  surface: '#FFFFFF',
  surfaceSecondary: '#F5F8FB',
  sidebar: '#0B2540',
  sidebarText: '#D6E0EA',
  sidebarTextMuted: '#8FA3B5',
  sidebarActive: 'rgba(255,255,255,0.1)',
  sidebarHover: 'rgba(255,255,255,0.06)',
  topbar: '#FFFFFF',
  text: '#1A2332',
  textSecondary: '#5A6B7C',
  border: '#D0DBE6',
  borderStrong: '#B5C3D1',
  success: '#1E7A46',
  warning: '#B7791F',
  danger: '#B42318',
  info: '#1B6FA5',
  chartPrimary: '#1A4B72',
  chartSecondary: '#2A6FA8',
  chartTertiary: '#5B8BB5',
  chartMuted: '#A8BBC9',
  chartSuccess: '#1E7A46',
  chartWarning: '#B7791F',
} as const;

export const efDarkColors = {
  primary: '#4A90C2',
  primaryHover: '#63A4D0',
  primarySoft: '#1A2F42',
  accent: '#5BA3D4',
  background: '#0B1220',
  surface: '#141C2B',
  surfaceSecondary: '#1A2436',
  sidebar: '#080E18',
  sidebarText: '#DCE4EE',
  sidebarTextMuted: '#8B9AAB',
  sidebarActive: 'rgba(74,144,194,0.18)',
  sidebarHover: 'rgba(255,255,255,0.05)',
  topbar: '#141C2B',
  text: '#E8EEF5',
  textSecondary: '#9AA8B8',
  border: '#2A3548',
  borderStrong: '#3A4860',
  success: '#3D9B66',
  warning: '#D4A017',
  danger: '#E05A4F',
  info: '#4A9FD4',
  chartPrimary: '#4A90C2',
  chartSecondary: '#5BA3D4',
  chartTertiary: '#7AB0D0',
  chartMuted: '#4A5A6E',
  chartSuccess: '#3D9B66',
  chartWarning: '#D4A017',
} as const;

export type EfColorTokens = {
  primary: string;
  primaryHover: string;
  primarySoft: string;
  accent: string;
  background: string;
  surface: string;
  surfaceSecondary: string;
  sidebar: string;
  sidebarText: string;
  sidebarTextMuted: string;
  sidebarActive: string;
  sidebarHover: string;
  topbar: string;
  text: string;
  textSecondary: string;
  border: string;
  borderStrong: string;
  success: string;
  warning: string;
  danger: string;
  info: string;
  chartPrimary: string;
  chartSecondary: string;
  chartTertiary: string;
  chartMuted: string;
  chartSuccess: string;
  chartWarning: string;
};

export const efShadows = {
  light: {
    sm: '0 1px 2px rgba(15, 39, 68, 0.05)',
    md: '0 2px 8px rgba(15, 39, 68, 0.07)',
    lg: '0 8px 24px rgba(15, 39, 68, 0.1)',
  },
  dark: {
    sm: '0 1px 2px rgba(0, 0, 0, 0.35)',
    md: '0 2px 8px rgba(0, 0, 0, 0.4)',
    lg: '0 8px 24px rgba(0, 0, 0, 0.45)',
  },
} as const;

/** @deprecated Use theme palette / CSS vars — kept for gradual migration */
export const efColors = {
  navy: efLightColors.sidebar,
  navyMid: efLightColors.primary,
  teal: efLightColors.accent,
  tealSoft: efLightColors.primarySoft,
  bg: efLightColors.background,
  surface: efLightColors.surface,
  border: efLightColors.border,
  text: efLightColors.text,
  muted: efLightColors.textSecondary,
  success: efLightColors.success,
  warning: efLightColors.warning,
  danger: efLightColors.danger,
  info: efLightColors.info,
} as const;
