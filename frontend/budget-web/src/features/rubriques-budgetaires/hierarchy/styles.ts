import type { SxProps, Theme } from '@mui/material';

/** Styles globaux Rupture → RB (tous écrans Budget Web) — tokens SNEL. */
export const RUBRIQUE_RUPTURE = {
  bgcolor: 'var(--ef-accent-soft)',
  color: 'var(--ef-text)',
  border: 'var(--ef-accent)',
  borderLeft: '4px solid var(--ef-accent)',
  bgcolorNested: 'var(--ef-accent-soft)',
  colorNested: 'var(--ef-text)',
  borderNested: 'var(--ef-accent)',
} as const;

export const RUBRIQUE_LEAF = {
  bgcolor: 'transparent',
  color: 'text.primary',
} as const;

/**
 * Groupe niveau 1 — style prévisions (bleu-gris sobre / surface secondaire).
 */
export const GROUPE_NIVEAU1_PREVISION = {
  bgcolor: 'var(--ef-surface-secondary)',
  color: 'var(--ef-text)',
  border: 'var(--ef-border)',
  borderLeft: '4px solid var(--ef-primary)',
} as const;

export type RubriqueBreakVisualLevel = 'primary' | 'nested';

export function resolveRubriqueBreakLevel(
  niveau: number | null | undefined,
  parentId: number | null | undefined,
): RubriqueBreakVisualLevel {
  if (parentId == null || (niveau ?? 0) <= 0) return 'primary';
  return 'nested';
}

export function rubriqueRuptureRowSx(level: RubriqueBreakVisualLevel = 'primary'): SxProps<Theme> {
  const nested = level === 'nested';
  return {
    bgcolor: nested ? RUBRIQUE_RUPTURE.bgcolorNested : RUBRIQUE_RUPTURE.bgcolor,
    color: nested ? RUBRIQUE_RUPTURE.colorNested : RUBRIQUE_RUPTURE.color,
    fontWeight: 700,
    borderLeft: nested ? `4px solid ${RUBRIQUE_RUPTURE.borderNested}` : RUBRIQUE_RUPTURE.borderLeft,
  };
}

export function rubriqueRuptureTreeRowSx(
  level: RubriqueBreakVisualLevel,
  selected: boolean,
): SxProps<Theme> {
  const base = rubriqueRuptureRowSx(level);
  return {
    ...base,
    borderRadius: 1,
    ...(selected
      ? {
          outline: '2px solid',
          outlineColor: 'primary.main',
          outlineOffset: -2,
        }
      : null),
    '&:hover': {
      filter: 'brightness(0.97)',
    },
  };
}
