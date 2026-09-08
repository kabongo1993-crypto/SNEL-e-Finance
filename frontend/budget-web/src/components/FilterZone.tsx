import { Box, Stack, type SxProps, type Theme } from '@mui/material';
import type { ReactNode } from 'react';
import {
  filterGridColumns,
  filterZoneGap,
  formFieldLabelClearance,
  formGridColumnGap,
  formGridRowGap,
} from './formTokens';

export type FilterGridColumns = {
  xs?: number;
  sm?: number;
  md?: number;
  lg?: number;
  xl?: number;
};

type FilterFieldsProps = {
  children: ReactNode;
  /** Nombre de colonnes par breakpoint (défaut : design system). */
  columns?: FilterGridColumns;
  sx?: SxProps<Theme>;
};

function toTemplate(n: number): string {
  return n <= 1 ? 'minmax(0, 1fr)' : `repeat(${n}, minmax(0, 1fr))`;
}

/** Styles partagés : grille CSS à colonnes stables + champs qui remplissent la cellule. */
export function filterFieldsGridSx(columns: FilterGridColumns = filterGridColumns): SxProps<Theme> {
  const xs = columns.xs ?? filterGridColumns.xs;
  const sm = columns.sm ?? filterGridColumns.sm;
  const md = columns.md ?? filterGridColumns.md;
  const lg = columns.lg ?? filterGridColumns.lg;
  const xl = columns.xl ?? filterGridColumns.xl;

  return {
    display: 'grid',
    width: '100%',
    alignItems: 'start',
    columnGap: formGridColumnGap,
    rowGap: formGridRowGap,
    gridTemplateColumns: {
      xs: toTemplate(xs),
      sm: toTemplate(sm),
      md: toTemplate(md),
      lg: toTemplate(lg),
      xl: toTemplate(xl),
    },
    // Réserve le label flottant ; largeur = colonne (pas le density px du champ).
    '& > *': {
      pt: formFieldLabelClearance,
      boxSizing: 'border-box',
      minWidth: 0,
      width: '100%',
      maxWidth: '100%',
    },
    '& > .MuiAutocomplete-root, & > .MuiTextField-root, & > .MuiFormControl-root': {
      width: '100% !important',
      minWidth: '0 !important',
      maxWidth: '100% !important',
      flex: 'none !important',
    },
    '& > .MuiAutocomplete-root .MuiTextField-root, & > .MuiAutocomplete-root .MuiFormControl-root': {
      width: '100% !important',
      minWidth: '0 !important',
      maxWidth: '100% !important',
    },
  };
}

/**
 * Grille de filtres métier — une seule grille CSS, axes verticaux communs.
 * Ne pas y placer Recherche / Actualiser (utiliser FilterSearchRow).
 */
export function FilterFields({ children, columns, sx }: FilterFieldsProps) {
  return (
    <Box sx={[filterFieldsGridSx(columns), ...(Array.isArray(sx) ? sx : sx ? [sx] : [])]}>
      {children}
    </Box>
  );
}

type FilterSearchRowProps = {
  /** Champ recherche (prend l’espace restant). */
  search?: ReactNode;
  /** Boutons (Actualiser, etc.) — largeur stable à droite. */
  actions?: ReactNode;
  children?: ReactNode;
  sx?: SxProps<Theme>;
};

/**
 * Rangée distincte : Recherche (flex) + actions (fixe).
 * Reste sur une ligne tant que la largeur le permet.
 */
export function FilterSearchRow({ search, actions, children, sx }: FilterSearchRowProps) {
  return (
    <Box
      sx={[
        {
          display: 'flex',
          flexDirection: { xs: 'column', sm: 'row' },
          flexWrap: 'wrap',
          alignItems: { xs: 'stretch', sm: 'flex-start' },
          columnGap: formGridColumnGap,
          rowGap: formGridColumnGap,
          width: '100%',
          pt: formFieldLabelClearance,
          '& > [data-filter-search]': {
            flex: '1 1 220px',
            minWidth: { xs: '100%', sm: 200 },
            maxWidth: '100%',
          },
          '& > [data-filter-actions]': {
            display: 'flex',
            flexWrap: 'wrap',
            gap: 1,
            flexShrink: 0,
            alignItems: 'center',
          },
        },
        ...(Array.isArray(sx) ? sx : sx ? [sx] : []),
      ]}
    >
      {search != null && <Box data-filter-search>{search}</Box>}
      {actions != null && <Box data-filter-actions>{actions}</Box>}
      {children}
    </Box>
  );
}

type FilterZoneProps = {
  /** Filtres métier uniquement (Exercice, Département, …). */
  children: ReactNode;
  search?: ReactNode;
  actions?: ReactNode;
  columns?: FilterGridColumns;
  sx?: SxProps<Theme>;
};

/**
 * Zone de filtres standard e-Finance :
 * 1) grille alignée des filtres ;
 * 2) rangée Recherche + actions.
 */
export function FilterZone({ children, search, actions, columns, sx }: FilterZoneProps) {
  const hasSearchRow = search != null || actions != null;
  return (
    <Stack
      spacing={filterZoneGap}
      useFlexGap
      sx={[{ width: '100%' }, ...(Array.isArray(sx) ? sx : sx ? [sx] : [])]}
    >
      <FilterFields columns={columns}>{children}</FilterFields>
      {hasSearchRow && <FilterSearchRow search={search} actions={actions} />}
    </Stack>
  );
}
