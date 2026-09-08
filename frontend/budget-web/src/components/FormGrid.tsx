import { Box, type StackProps, type SxProps, type Theme } from '@mui/material';
import type { ReactNode } from 'react';
import {
  formFieldLabelClearance,
  formGridColumnGap,
  formGridRowGap,
} from './formTokens';
import { FilterFields, filterFieldsGridSx, type FilterGridColumns } from './FilterZone';

type FormGridProps = {
  children: ReactNode;
  /**
   * `flow` — flex wrap (largeurs intrinsèques / density).
   * `columns` — grille CSS à axes verticaux stables (recommandé filtres / barres alignées).
   */
  variant?: 'flow' | 'columns';
  /** Colonnes si variant="columns" (défaut design system filtres). */
  columns?: FilterGridColumns;
  sx?: SxProps<Theme>;
} & Omit<StackProps, 'children' | 'spacing' | 'sx' | 'direction'>;

/**
 * Conteneur de champs e-Finance.
 * - flow : proportions individuelles (formulaires denses)
 * - columns : grille alignée (filtres / toolbars) — préférer FilterZone pour Recherche+actions
 */
export function FormGrid({
  children,
  variant = 'flow',
  columns,
  sx,
  ...rest
}: FormGridProps) {
  if (variant === 'columns') {
    return (
      <FilterFields columns={columns} sx={sx}>
        {children}
      </FilterFields>
    );
  }

  return (
    <Box
      {...rest}
      sx={[
        {
          display: 'flex',
          flexDirection: 'row',
          flexWrap: 'wrap',
          alignItems: 'flex-start',
          width: '100%',
          columnGap: formGridColumnGap,
          rowGap: formGridRowGap,
          '& > *': {
            pt: formFieldLabelClearance,
            boxSizing: 'border-box',
          },
        },
        ...(Array.isArray(sx) ? sx : sx ? [sx] : []),
      ]}
    >
      {children}
    </Box>
  );
}

/** @deprecated Préférer FilterFields / FilterZone — alias colonnes alignées. */
export const FilterRow = FilterFields;

type FormFieldSlotProps = {
  children: ReactNode;
  grow?: boolean | number;
  minWidth?: number;
  maxWidth?: number;
  fullRow?: boolean;
};

/** Emplacement de champ dans FormGrid flow. */
export function FormFieldSlot({
  children,
  grow = false,
  minWidth = 160,
  maxWidth,
  fullRow = false,
}: FormFieldSlotProps) {
  return (
    <Box
      sx={{
        flex: fullRow ? '1 1 100%' : grow ? `1 1 ${minWidth}px` : `0 1 auto`,
        minWidth: fullRow ? '100%' : minWidth,
        maxWidth: fullRow ? '100%' : maxWidth,
        width: fullRow ? '100%' : undefined,
      }}
    >
      {children}
    </Box>
  );
}

export { filterFieldsGridSx };
