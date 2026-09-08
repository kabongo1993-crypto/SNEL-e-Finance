/**
 * SNEL Online — e-Finance design tokens.
 * Source unique : page /login = référence institutionnelle ; app = déclinaison métier.
 */

/** Nom affiché dans l’interface (sidebar, textes). */
export const BRAND_NAME = 'e-finance' as const;

/** Titre exact de l’onglet navigateur / document. */
export const DOCUMENT_TITLE = 'SNEL Online — e-Finance' as const;

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
  xs: 8,
  sm: 10,
  md: 14,
  lg: 18,
  xl: 24,
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

/**
 * Mode clair — référence métier (continuité login).
 */
export const efLightColors = {
  primary: '#06439B',
  primaryHover: '#1457A6',
  primarySoft: '#E8F0FA',
  primaryDark: '#0B2947',
  accent: '#FFD500',
  /** Fond rupture / accent jaune très clair */
  accentSoft: '#FFF8E1',
  background: '#F3F6FA',
  surface: '#FFFFFF',
  surfaceSecondary: '#F7FAFC',
  surfaceHover: '#F1F5FA',
  sidebar: '#0B2947',
  sidebarText: '#E8EEF5',
  sidebarTextMuted: '#9BB0C4',
  /** Actif nav = bleu SNEL principal */
  sidebarActive: '#06439B',
  sidebarHover: 'rgba(255,255,255,0.06)',
  topbar: '#FFFFFF',
  text: '#17324D',
  textSecondary: '#64748B',
  border: '#D8E1EA',
  borderStrong: '#B8C7D6',
  borderSubtle: '#E6EDF4',
  success: '#1E7A46',
  successSoft: '#EAF6EE',
  warning: '#B7791F',
  warningSoft: '#FFF8E7',
  danger: '#B42318',
  dangerSoft: '#FCEEEE',
  info: '#06439B',
  infoSoft: '#E8F0FA',
  disabled: '#B8C2CE',
  /** Delta KPI positif — plus lumineux que `success` pour rester lisible en petit corps. */
  kpiPositive: '#12874C',
  kpiNegative: '#C4342A',
  overlay: 'rgba(11, 41, 71, 0.45)',
  chartPrimary: '#06439B',
  chartSecondary: '#1457A6',
  chartTertiary: '#5B8BB5',
  chartMuted: '#A8BBC9',
  chartSuccess: '#1E7A46',
  chartWarning: '#B7791F',
} as const;

/**
 * Mode sombre — même design system, surfaces bleutées (jamais #000).
 */
export const efDarkColors = {
  primary: '#2F80D8',
  primaryHover: '#4A96E3',
  primarySoft: '#152A45',
  primaryDark: '#081827',
  accent: '#FFD500',
  accentSoft: 'rgba(255, 213, 0, 0.12)',
  background: '#0B1424',
  surface: '#111D2E',
  surfaceSecondary: '#172438',
  surfaceHover: '#1B2A40',
  sidebar: '#081827',
  sidebarText: '#DCE4EE',
  sidebarTextMuted: '#A8B4C3',
  sidebarActive: '#06439B',
  sidebarHover: 'rgba(255,255,255,0.05)',
  topbar: '#111D2E',
  text: '#F1F5F9',
  textSecondary: '#A8B4C3',
  border: '#293A50',
  borderStrong: '#3A4F68',
  borderSubtle: '#22314A',
  success: '#3D9B66',
  successSoft: 'rgba(61, 155, 102, 0.16)',
  warning: '#D4A017',
  warningSoft: 'rgba(212, 160, 23, 0.14)',
  danger: '#E05A4F',
  dangerSoft: 'rgba(224, 90, 79, 0.14)',
  info: '#2F80D8',
  infoSoft: 'rgba(47, 128, 216, 0.16)',
  disabled: '#4A5A6E',
  kpiPositive: '#4CB27A',
  kpiNegative: '#F07167',
  overlay: 'rgba(0, 0, 0, 0.6)',
  chartPrimary: '#2F80D8',
  chartSecondary: '#4A96E3',
  chartTertiary: '#7AB0D0',
  chartMuted: '#4A5A6E',
  chartSuccess: '#3D9B66',
  chartWarning: '#D4A017',
} as const;

export type EfColorTokens = {
  primary: string;
  primaryHover: string;
  primarySoft: string;
  primaryDark: string;
  accent: string;
  accentSoft: string;
  background: string;
  surface: string;
  surfaceSecondary: string;
  surfaceHover: string;
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
  borderSubtle: string;
  success: string;
  successSoft: string;
  warning: string;
  warningSoft: string;
  danger: string;
  dangerSoft: string;
  info: string;
  infoSoft: string;
  disabled: string;
  kpiPositive: string;
  kpiNegative: string;
  overlay: string;
  chartPrimary: string;
  chartSecondary: string;
  chartTertiary: string;
  chartMuted: string;
  chartSuccess: string;
  chartWarning: string;
};

/**
 * Ombres diffuses « carte flottante » : une passe de contact serrée + une passe
 * large très peu opaque, pour détacher les cartes sans les cerner.
 */
export const efShadows = {
  light: {
    sm: '0 1px 3px rgba(11, 41, 71, 0.05)',
    md: '0 1px 3px rgba(11, 41, 71, 0.04), 0 8px 24px rgba(11, 41, 71, 0.06)',
    lg: '0 2px 6px rgba(11, 41, 71, 0.05), 0 18px 40px rgba(11, 41, 71, 0.09)',
  },
  dark: {
    sm: '0 1px 3px rgba(0, 0, 0, 0.4)',
    md: '0 1px 3px rgba(0, 0, 0, 0.35), 0 8px 24px rgba(0, 0, 0, 0.45)',
    lg: '0 2px 6px rgba(0, 0, 0, 0.4), 0 18px 40px rgba(0, 0, 0, 0.55)',
  },
} as const;
