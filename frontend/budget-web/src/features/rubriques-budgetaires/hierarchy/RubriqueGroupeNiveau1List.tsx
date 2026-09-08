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
import { useState, Fragment } from 'react';
import type { RubriqueBudgetaire } from '../../../services/apiClient';
import { formatGroupeNiveau1, type RubriqueGroupeNiveau1 } from './buildGroupeNiveau1';
import { rubriqueRuptureRowSx } from './styles';
import { formatDateFr, libelleStatut } from '../rubriqueUtils';

export interface RubriqueGroupeNiveau1ListAction {
  id: string;
  label: string;
  color?: 'inherit' | 'error' | 'primary' | 'success';
  hidden?: (row: RubriqueBudgetaire) => boolean;
  onClick: (row: RubriqueBudgetaire) => void;
}

interface RubriqueGroupeNiveau1ListProps {
  groupes: RubriqueGroupeNiveau1[];
  selectedId: number | null;
  onSelect: (id: number) => void;
  actions?: RubriqueGroupeNiveau1ListAction[];
  emptyTitle: string;
  emptyDescription: string;
}

/**
 * Liste hiérarchique Groupe N1 → RB (sections techniques absentes).
 */
export function RubriqueGroupeNiveau1List({
  groupes,
  selectedId,
  onSelect,
  actions = [],
  emptyTitle,
  emptyDescription,
}: RubriqueGroupeNiveau1ListProps) {
  const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
  const [menuRow, setMenuRow] = useState<RubriqueBudgetaire | null>(null);

  const totalRb = groupes.reduce((n, g) => n + g.rubriques.length, 0);

  if (groupes.length === 0) {
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
              {/* La rupture par groupe interdit la vue carte : on réduit les colonnes. */}
              <TableCell sx={{ fontWeight: 700, width: 110, display: { xs: 'none', md: 'table-cell' } }}>
                Type
              </TableCell>
              <TableCell sx={{ fontWeight: 700, width: 100 }}>Statut</TableCell>
              <TableCell sx={{ fontWeight: 700, width: 100, display: { xs: 'none', md: 'table-cell' } }}>
                Création
              </TableCell>
              {actions.length > 0 ? <TableCell align="right" sx={{ width: 56 }} /> : null}
            </TableRow>
          </TableHead>
          <TableBody>
            {groupes.map((groupe) => (
              <Fragment key={`g-${groupe.idGroupeRB}`}>
                <TableRow sx={rubriqueRuptureRowSx('primary')}>
                  <TableCell
                    colSpan={actions.length > 0 ? 6 : 5}
                    sx={{ fontWeight: 700, py: 1 }}
                  >
                    <Box sx={{ display: 'flex', gap: 1, alignItems: 'baseline', flexWrap: 'wrap' }}>
                      <Typography
                        component="span"
                        sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 800 }}
                      >
                        [{groupe.codeGroupe}]
                      </Typography>
                      <Typography component="span" sx={{ fontWeight: 700 }}>
                        {groupe.libelleGroupe}
                      </Typography>
                      <Typography variant="caption" sx={{ opacity: 0.75 }}>
                        {groupe.rubriques.length} RB
                      </Typography>
                    </Box>
                  </TableCell>
                </TableRow>
                {groupe.rubriques.map((r) => {
                  const selected = selectedId === r.idRB;
                  return (
                    <TableRow
                      key={r.idRB}
                      hover
                      selected={selected}
                      onClick={() => onSelect(r.idRB)}
                      sx={{ cursor: 'pointer' }}
                    >
                      <TableCell sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700, pl: 4 }}>
                        {r.codeRB}
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2" sx={{ fontWeight: 500 }}>
                          {r.libelle}
                        </Typography>
                        <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                          {formatGroupeNiveau1(groupe)}
                        </Typography>
                      </TableCell>
                      <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                        <Chip size="small" label="RB" variant="outlined" sx={{ fontWeight: 700, minWidth: 72 }} />
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
                      <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                        {formatDateFr(r.dateCreation)}
                      </TableCell>
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
              </Fragment>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <Box sx={{ px: 2, py: 1, borderTop: '1px solid', borderColor: 'divider' }}>
        <Typography variant="caption" color="text.secondary">
          <strong>{groupes.length}</strong> groupe{groupes.length > 1 ? 's' : ''} ·{' '}
          <strong>{totalRb}</strong> RB
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
