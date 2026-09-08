import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { Box, Paper, Typography } from '@mui/material';
import type { RubriqueHierarchyNode } from './types';
import { estRuptureNoeud } from './types';
import { formatRubrique } from './formatLabel';
import { resolveRubriqueBreakLevel, rubriqueRuptureTreeRowSx } from './styles';

export interface RubriqueHierarchyTreeProps {
  nodes: RubriqueHierarchyNode[];
  expanded: Set<number>;
  selectedId: number | null;
  onToggle: (id: number) => void;
  onSelect: (id: number) => void;
  emptyTitle?: string;
  emptyDescription?: string;
  maxHeight?: number | string;
  /** Si true, seules les feuilles (RB) sont sélectionnables. */
  selectLeavesOnly?: boolean;
}

function TreeRow({
  node,
  depth,
  expanded,
  selectedId,
  onToggle,
  onSelect,
  selectLeavesOnly,
}: {
  node: RubriqueHierarchyNode;
  depth: number;
  expanded: Set<number>;
  selectedId: number | null;
  onToggle: (id: number) => void;
  onSelect: (id: number) => void;
  selectLeavesOnly: boolean;
}) {
  const isRupture = estRuptureNoeud(node);
  const hasChildren = node.children.length > 0;
  const isExpanded = expanded.has(node.rubrique.idRB);
  const isSelected = selectedId === node.rubrique.idRB;
  const breakLevel = resolveRubriqueBreakLevel(node.rubrique.niveau, node.rubrique.parentId);
  const canSelect = !selectLeavesOnly || !isRupture;

  return (
    <Box>
      <Box
        onClick={() => {
          if (canSelect) onSelect(node.rubrique.idRB);
          else if (hasChildren) onToggle(node.rubrique.idRB);
        }}
        role="treeitem"
        aria-expanded={hasChildren ? isExpanded : undefined}
        aria-selected={isSelected}
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 0.5,
          pl: 0.75 + depth * 1.75,
          pr: 0.75,
          py: isRupture ? 0.65 : 0.45,
          cursor: canSelect || hasChildren ? 'pointer' : 'default',
          ...(isRupture
            ? rubriqueRuptureTreeRowSx(breakLevel, isSelected)
            : {
                borderRadius: 1,
                bgcolor: isSelected ? 'action.selected' : 'transparent',
                '&:hover': { bgcolor: isSelected ? 'action.selected' : 'action.hover' },
              }),
        }}
      >
        <Box
          component="span"
          onClick={(e) => {
            e.stopPropagation();
            if (hasChildren) onToggle(node.rubrique.idRB);
          }}
          sx={{
            width: 18,
            height: 18,
            display: 'grid',
            placeItems: 'center',
            color: isRupture ? 'inherit' : 'text.secondary',
            flexShrink: 0,
            visibility: hasChildren ? 'visible' : 'hidden',
          }}
        >
          {isExpanded ? <ExpandMoreIcon sx={{ fontSize: 16 }} /> : <ChevronRightIcon sx={{ fontSize: 16 }} />}
        </Box>
        <Typography
          component="span"
          sx={{
            fontFamily: 'ui-monospace, monospace',
            fontSize: isRupture ? '0.8rem' : '0.75rem',
            fontWeight: 700,
            minWidth: 52,
            flexShrink: 0,
          }}
        >
          {node.rubrique.codeRB}
        </Typography>
        <Typography
          noWrap
          sx={{
            flex: 1,
            fontSize: isRupture ? '0.85rem' : '0.8rem',
            fontWeight: isRupture ? 700 : isSelected ? 650 : 500,
            minWidth: 0,
          }}
        >
          {node.rubrique.libelle}
        </Typography>
        {!node.rubrique.actif && (
          <Typography variant="caption" sx={{ flexShrink: 0, opacity: 0.8 }}>
            Inactif
          </Typography>
        )}
      </Box>
      {hasChildren &&
        isExpanded &&
        node.children.map((child) => (
          <TreeRow
            key={child.rubrique.idRB}
            node={child}
            depth={depth + 1}
            expanded={expanded}
            selectedId={selectedId}
            onToggle={onToggle}
            onSelect={onSelect}
            selectLeavesOnly={selectLeavesOnly}
          />
        ))}
    </Box>
  );
}

/**
 * Arbre réutilisable Rupture (jaune) → RB (indentée).
 * Source de vérité : hiérarchie `FK_RubriqueBudgetaireParent`.
 */
export function RubriqueHierarchyTree({
  nodes,
  expanded,
  selectedId,
  onToggle,
  onSelect,
  emptyTitle = 'Aucune rubrique',
  emptyDescription = '',
  maxHeight = 640,
  selectLeavesOnly = false,
}: RubriqueHierarchyTreeProps) {
  if (nodes.length === 0) {
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
    <Paper sx={{ p: 1, minHeight: 360, maxHeight, overflow: 'auto' }} role="tree" aria-label="Rubriques budgétaires">
      {nodes.map((node) => (
        <TreeRow
          key={node.rubrique.idRB}
          node={node}
          depth={0}
          expanded={expanded}
          selectedId={selectedId}
          onToggle={onToggle}
          onSelect={onSelect}
          selectLeavesOnly={selectLeavesOnly}
        />
      ))}
    </Paper>
  );
}

/** Légende visuelle commune. */
export function RubriqueHierarchyLegend() {
  return (
    <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap', alignItems: 'center' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
        <Box
          sx={{
            width: 14,
            height: 14,
            bgcolor: 'var(--ef-accent-soft)',
            border: '1px solid var(--ef-accent)',
            borderRadius: 0.5,
          }}
        />
        <Typography variant="caption" color="text.secondary">
          Rupture / catégorie
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

export function rubriqueNodeAriaLabel(node: RubriqueHierarchyNode): string {
  const kind = estRuptureNoeud(node) ? 'Rupture' : 'RB';
  return `${kind} ${formatRubrique(node.rubrique)}`;
}
