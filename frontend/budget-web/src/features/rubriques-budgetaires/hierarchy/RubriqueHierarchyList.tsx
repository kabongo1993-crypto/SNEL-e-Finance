import MoreVertIcon from '@mui/icons-material/MoreVert';
import {
  Box,
  Chip,
  IconButton,
  ListItemText,
  Menu,
  MenuItem,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import type { RubriqueBudgetaire } from '../../../services/apiClient';
import { flattenRubriqueHierarchy } from './buildHierarchy';
import { formatRubriqueParent } from './formatLabel';
import { resolveRubriqueBreakLevel, rubriqueRuptureRowSx } from './styles';
import { estRuptureNoeud, type RubriqueHierarchyNode } from './types';
import { formatDateFr, libelleStatut } from '../rubriqueUtils';

export interface RubriqueHierarchyListAction {
  id: string;
  label: string;
  color?: 'inherit' | 'error' | 'primary' | 'success';
  hidden?: (row: RubriqueBudgetaire) => boolean;
  onClick: (row: RubriqueBudgetaire) => void;
}

interface RubriqueHierarchyListProps {
  nodes: RubriqueHierarchyNode[];
  selectedId: number | null;
  onSelect: (id: number) => void;
  actions?: RubriqueHierarchyListAction[];
  emptyTitle: string;
  emptyDescription: string;
}

/**
 * Liste hiérarchique Rupture → RB (même ordre que l’arbre, sans repli).
 * Utilisée pour la « vue liste » du référentiel afin de ne jamais perdre le contexte de rupture.
 */
export function RubriqueHierarchyList({
  nodes,
  selectedId,
  onSelect,
  actions = [],
  emptyTitle,
  emptyDescription,
}: RubriqueHierarchyListProps) {
  const flat = flattenRubriqueHierarchy(nodes);
  const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
  const [menuRow, setMenuRow] = useState<RubriqueBudgetaire | null>(null);

  if (flat.length === 0) {
    return (
      <Paper sx={{ p: 4, textAlign: 'center' }}>
        <Typography sx={{ fontWeight: 600 }}>{emptyTitle}</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
          {emptyDescription}
        </Typography>
      </Paper>
    );
  }

  const visibleActions = (row: RubriqueBudgetaire) => actions.filter((a) => !a.hidden?.(row));

  return (
    <Paper>
      <TableContainer sx={{ maxHeight: 640 }}>
        <Table stickyHeader size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={{ fontWeight: 700, width: 100 }}>Code</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Libellé</TableCell>
              <TableCell sx={{ fontWeight: 700, width: 110 }}>Type</TableCell>
              <TableCell sx={{ fontWeight: 700, width: 100 }}>Statut</TableCell>
              <TableCell sx={{ fontWeight: 700, width: 100 }}>Création</TableCell>
              {actions.length > 0 ? <TableCell align="right" sx={{ width: 56 }} /> : null}
            </TableRow>
          </TableHead>
          <TableBody>
            {flat.map((node) => {
              const r = node.rubrique;
              const isRupture = estRuptureNoeud(node);
              const level = resolveRubriqueBreakLevel(r.niveau, r.parentId);
              const selected = selectedId === r.idRB;
              const depth = r.niveau;
              return (
                <TableRow
                  key={r.idRB}
                  hover
                  selected={selected}
                  onClick={() => onSelect(r.idRB)}
                  sx={{
                    cursor: 'pointer',
                    ...(isRupture ? rubriqueRuptureRowSx(level) : null),
                    '& td': { fontWeight: isRupture ? 700 : 400 },
                  }}
                >
                  <TableCell
                    sx={{
                      fontFamily: 'ui-monospace, monospace',
                      pl: 1.5 + depth * 2,
                      borderLeft: isRupture ? undefined : '4px solid transparent',
                    }}
                  >
                    {r.codeRB}
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontWeight: isRupture ? 700 : 500 }}>
                      {r.libelle}
                    </Typography>
                    {!isRupture && r.parentCode ? (
                      <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                        {formatRubriqueParent(r)}
                      </Typography>
                    ) : null}
                  </TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      label={isRupture ? 'Rupture' : 'RB'}
                      variant="outlined"
                      sx={{ fontWeight: 700, minWidth: 72 }}
                    />
                  </TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      label={libelleStatut(r.actif)}
                      color={r.actif ? 'success' : 'default'}
                      variant={r.actif ? 'outlined' : 'filled'}
                      sx={{ fontWeight: 700 }}
                    />
                  </TableCell>
                  <TableCell>{formatDateFr(r.dateCreation)}</TableCell>
                  {actions.length > 0 ? (
                    <TableCell align="right" onClick={(e) => e.stopPropagation()}>
                      {visibleActions(r).length > 0 ? (
                        <IconButton
                          size="small"
                          aria-label="Actions"
                          onClick={(e) => {
                            setMenuAnchor(e.currentTarget);
                            setMenuRow(r);
                          }}
                        >
                          <MoreVertIcon fontSize="small" />
                        </IconButton>
                      ) : null}
                    </TableCell>
                  ) : null}
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
      <Box sx={{ px: 2, py: 1, borderTop: '1px solid', borderColor: 'divider' }}>
        <Typography variant="caption" color="text.secondary">
          <strong>{flat.length}</strong> élément{flat.length > 1 ? 's' : ''} (ruptures + RB)
        </Typography>
      </Box>
      <Menu
        anchorEl={menuAnchor}
        open={Boolean(menuAnchor) && menuRow != null}
        onClose={() => {
          setMenuAnchor(null);
          setMenuRow(null);
        }}
      >
        {menuRow &&
          visibleActions(menuRow).map((a) => (
            <MenuItem
              key={a.id}
              onClick={() => {
                a.onClick(menuRow);
                setMenuAnchor(null);
                setMenuRow(null);
              }}
              sx={a.color === 'error' ? { color: 'error.main' } : undefined}
            >
              <ListItemText>{a.label}</ListItemText>
            </MenuItem>
          ))}
      </Menu>
    </Paper>
  );
}
