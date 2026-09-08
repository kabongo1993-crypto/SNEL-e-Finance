import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { Box, Paper, Typography } from '@mui/material';
import type { RubriqueBudgetaire } from '../../../services/apiClient';
import { formatGroupeNiveau1, type RubriqueGroupeNiveau1 } from './buildGroupeNiveau1';
import { RUBRIQUE_RUPTURE, rubriqueRuptureTreeRowSx } from './styles';

export interface RubriqueGroupeNiveau1TreeProps {
  groupes: RubriqueGroupeNiveau1[];
  expanded: Set<number>;
  selectedId: number | null;
  onToggle: (idGroupeRB: number) => void;
  onSelect: (idRB: number) => void;
  emptyTitle?: string;
  emptyDescription?: string;
  maxHeight?: number | string;
}

function GroupeHeader({
  groupe,
  expanded,
  onToggle,
}: {
  groupe: RubriqueGroupeNiveau1;
  expanded: boolean;
  onToggle: () => void;
}) {
  return (
    <Box
      onClick={onToggle}
      role="treeitem"
      aria-expanded={expanded}
      sx={{
        display: 'flex',
        alignItems: 'center',
        gap: 0.5,
        px: 0.75,
        py: 0.75,
        cursor: 'pointer',
        ...rubriqueRuptureTreeRowSx('primary', false),
        mb: 0.25,
      }}
    >
      <Box
        component="span"
        sx={{
          width: 18,
          height: 18,
          display: 'grid',
          placeItems: 'center',
          flexShrink: 0,
        }}
      >
        {expanded ? <ExpandMoreIcon sx={{ fontSize: 18 }} /> : <ChevronRightIcon sx={{ fontSize: 18 }} />}
      </Box>
      <Typography
        component="span"
        sx={{
          fontFamily: 'ui-monospace, monospace',
          fontSize: '0.85rem',
          fontWeight: 800,
          minWidth: 36,
          flexShrink: 0,
        }}
      >
        [{groupe.codeGroupe}]
      </Typography>
      <Typography noWrap sx={{ flex: 1, fontSize: '0.875rem', fontWeight: 700, minWidth: 0 }}>
        {groupe.libelleGroupe}
      </Typography>
      <Typography variant="caption" sx={{ flexShrink: 0, opacity: 0.75, fontWeight: 600 }}>
        {groupe.rubriques.length} RB
      </Typography>
    </Box>
  );
}

function RbRow({
  rubrique,
  selected,
  onSelect,
}: {
  rubrique: RubriqueBudgetaire;
  selected: boolean;
  onSelect: () => void;
}) {
  return (
    <Box
      onClick={onSelect}
      role="treeitem"
      aria-selected={selected}
      sx={{
        display: 'flex',
        alignItems: 'center',
        gap: 0.75,
        pl: 3.5,
        pr: 0.75,
        py: 0.45,
        cursor: 'pointer',
        borderRadius: 1,
        bgcolor: selected ? 'action.selected' : 'transparent',
        '&:hover': { bgcolor: selected ? 'action.selected' : 'action.hover' },
      }}
    >
      <Typography
        component="span"
        sx={{
          fontFamily: 'ui-monospace, monospace',
          fontSize: '0.75rem',
          fontWeight: 700,
          minWidth: 52,
          flexShrink: 0,
        }}
      >
        {rubrique.codeRB}
      </Typography>
      <Typography noWrap sx={{ flex: 1, fontSize: '0.8rem', fontWeight: selected ? 650 : 500, minWidth: 0 }}>
        {rubrique.libelle}
      </Typography>
      {!rubrique.actif && (
        <Typography variant="caption" color="text.secondary" sx={{ flexShrink: 0 }}>
          Inactif
        </Typography>
      )}
    </Box>
  );
}

/**
 * Arbre réutilisable Groupe niveau 1 → RB (sections techniques masquées).
 */
export function RubriqueGroupeNiveau1Tree({
  groupes,
  expanded,
  selectedId,
  onToggle,
  onSelect,
  emptyTitle = 'Aucune rubrique',
  emptyDescription = '',
  maxHeight = 640,
}: RubriqueGroupeNiveau1TreeProps) {
  if (groupes.length === 0) {
    return (
      <Paper sx={{ p: 4, textAlign: 'center' }}>
        <Typography sx={{ fontWeight: 600 }}>{emptyTitle}</Typography>
        {emptyDescription ? (
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            {emptyDescription}
          </Typography>
        ) : null}
      </Paper>
    );
  }

  return (
    <Paper
      sx={{ p: 1, minHeight: 360, maxHeight, overflow: 'auto' }}
      role="tree"
      aria-label="Groupes niveau 1 des rubriques budgétaires"
    >
      {groupes.map((groupe) => {
        const isExpanded = expanded.has(groupe.idGroupeRB);
        return (
          <Box key={groupe.idGroupeRB} sx={{ mb: 0.5 }}>
            <GroupeHeader
              groupe={groupe}
              expanded={isExpanded}
              onToggle={() => onToggle(groupe.idGroupeRB)}
            />
            {isExpanded &&
              groupe.rubriques.map((rb) => (
                <RbRow
                  key={rb.idRB}
                  rubrique={rb}
                  selected={selectedId === rb.idRB}
                  onSelect={() => onSelect(rb.idRB)}
                />
              ))}
          </Box>
        );
      })}
    </Paper>
  );
}

export function RubriqueGroupeNiveau1Legend() {
  return (
    <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap', alignItems: 'center' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
        <Box
          sx={{
            width: 14,
            height: 14,
            bgcolor: RUBRIQUE_RUPTURE.bgcolor,
            border: `1px solid ${RUBRIQUE_RUPTURE.border}`,
            borderRadius: 0.5,
          }}
        />
        <Typography variant="caption" color="text.secondary">
          Groupe niveau 1
        </Typography>
      </Box>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
        <Box
          sx={{
            width: 14,
            height: 14,
            bgcolor: 'background.paper',
            border: '1px solid',
            borderColor: 'divider',
            borderRadius: 0.5,
          }}
        />
        <Typography variant="caption" color="text.secondary">
          Rubrique budgétaire (RB)
        </Typography>
      </Box>
    </Box>
  );
}

export function groupeNiveau1AriaLabel(groupe: RubriqueGroupeNiveau1): string {
  return `Groupe ${formatGroupeNiveau1(groupe)}`;
}
