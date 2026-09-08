import { ListSubheader, MenuItem, TextField, type TextFieldProps } from '@mui/material';
import { useMemo } from 'react';
import type { RubriqueBudgetaire } from '../../../services/apiClient';
import { buildRubriqueHierarchy, flattenRubriqueHierarchy } from './buildHierarchy';
import { formatRubrique } from './formatLabel';
import { RUBRIQUE_RUPTURE } from './styles';
import { estRuptureNoeud, type RubriqueHierarchyNode } from './types';

type RubriqueParentSelectProps = Omit<TextFieldProps, 'select' | 'children' | 'value' | 'onChange'> & {
  rubriques: RubriqueBudgetaire[];
  value: string;
  onChange: (value: string) => void;
  /** Exclure une rubrique (édition) et éventuellement ses descendants. */
  excludeId?: number | null;
  allowEmpty?: boolean;
  emptyLabel?: string;
};

function collectDescendantIds(node: RubriqueHierarchyNode, into: Set<number>) {
  into.add(node.rubrique.idRB);
  node.children.forEach((c) => collectDescendantIds(c, into));
}

/**
 * Sélecteur de rubrique / parent avec contexte Rupture → RB.
 */
export function RubriqueHierarchySelect({
  rubriques,
  value,
  onChange,
  excludeId,
  allowEmpty = true,
  emptyLabel = 'Aucune (racine, niveau 0)',
  slotProps,
  ...fieldProps
}: RubriqueParentSelectProps) {
  const options = useMemo(() => {
    const tree = buildRubriqueHierarchy(rubriques);
    const excluded = new Set<number>();
    if (excludeId != null) {
      const find = (nodes: RubriqueHierarchyNode[]): RubriqueHierarchyNode | null => {
        for (const n of nodes) {
          if (n.rubrique.idRB === excludeId) return n;
          const found = find(n.children);
          if (found) return found;
        }
        return null;
      };
      const node = find(tree);
      if (node) collectDescendantIds(node, excluded);
      else excluded.add(excludeId);
    }
    return flattenRubriqueHierarchy(tree).filter((n) => !excluded.has(n.rubrique.idRB));
  }, [rubriques, excludeId]);

  return (
    <TextField
      {...fieldProps}
      select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      slotProps={{
        ...slotProps,
        select: {
          ...(typeof slotProps?.select === 'object' ? slotProps.select : null),
          MenuProps: {
            slotProps: {
              paper: {
                sx: { maxHeight: 360 },
              },
            },
          },
        },
      }}
    >
      {allowEmpty ? (
        <MenuItem value="">
          <em>{emptyLabel}</em>
        </MenuItem>
      ) : null}
      {options.map((node) => {
        const isRupture = estRuptureNoeud(node);
        const depth = node.rubrique.niveau;
        return (
          <MenuItem
            key={node.rubrique.idRB}
            value={String(node.rubrique.idRB)}
            sx={{
              pl: 1.5 + depth * 1.5,
              fontWeight: isRupture ? 700 : 400,
              bgcolor: isRupture ? RUBRIQUE_RUPTURE.bgcolor : undefined,
              borderLeft: isRupture ? RUBRIQUE_RUPTURE.borderLeft : undefined,
              '&.Mui-selected': {
                bgcolor: isRupture ? RUBRIQUE_RUPTURE.bgcolorNested : undefined,
              },
            }}
          >
            {formatRubrique(node.rubrique)}
            {isRupture ? ' (rupture)' : ''}
          </MenuItem>
        );
      })}
      {options.length === 0 && !allowEmpty ? (
        <ListSubheader disableSticky>Aucune rubrique disponible</ListSubheader>
      ) : null}
    </TextField>
  );
}
