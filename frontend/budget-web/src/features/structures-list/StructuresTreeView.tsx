import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { Box, Paper, Typography } from '@mui/material';
import { getTypeVisual } from '../structures/orgUtils';
import { extraireCodeAffichage, type StructureTreeNode } from './structureUtils';

interface StructuresTreeViewProps {
  nodes: StructureTreeNode[];
  expanded: Set<number>;
  selectedId: number | null;
  onToggle: (id: number) => void;
  onSelect: (id: number) => void;
  emptyTitle: string;
  emptyDescription: string;
}

function TreeRow({
  node,
  depth,
  expanded,
  selectedId,
  onToggle,
  onSelect,
}: {
  node: StructureTreeNode;
  depth: number;
  expanded: Set<number>;
  selectedId: number | null;
  onToggle: (id: number) => void;
  onSelect: (id: number) => void;
}) {
  const hasChildren = node.children.length > 0;
  const isExpanded = expanded.has(node.structure.idStructure);
  const isSelected = selectedId === node.structure.idStructure;
  const visual = getTypeVisual(node.structure.typeStructure);
  const code = extraireCodeAffichage(node.structure.code);

  return (
    <Box>
      <Box
        onClick={() => onSelect(node.structure.idStructure)}
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 0.5,
          pl: 0.75 + depth * 1.5,
          pr: 0.75,
          py: 0.45,
          cursor: 'pointer',
          borderRadius: 1,
          bgcolor: isSelected ? 'action.selected' : 'transparent',
          borderLeft: '2px solid',
          borderColor: isSelected ? visual.color : 'transparent',
          '&:hover': { bgcolor: isSelected ? 'action.selected' : 'action.hover' },
        }}
      >
        <Box
          component="span"
          onClick={(e) => {
            e.stopPropagation();
            if (hasChildren) onToggle(node.structure.idStructure);
          }}
          sx={{
            width: 18,
            height: 18,
            display: 'grid',
            placeItems: 'center',
            color: 'text.secondary',
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
            fontSize: '0.75rem',
            fontWeight: 700,
            color: visual.color,
            minWidth: 40,
            flexShrink: 0,
          }}
        >
          {code}
        </Typography>
        <Typography noWrap sx={{ flex: 1, fontSize: '0.8rem', fontWeight: isSelected ? 650 : 500, minWidth: 0 }}>
          {node.structure.libelle}
        </Typography>
        {!node.structure.actif && (
          <Typography variant="caption" color="text.secondary" sx={{ flexShrink: 0 }}>
            Inactif
          </Typography>
        )}
      </Box>
      {hasChildren && isExpanded &&
        node.children.map((child) => (
          <TreeRow
            key={child.structure.idStructure}
            node={child}
            depth={depth + 1}
            expanded={expanded}
            selectedId={selectedId}
            onToggle={onToggle}
            onSelect={onSelect}
          />
        ))}
    </Box>
  );
}

export function StructuresTreeView({
  nodes,
  expanded,
  selectedId,
  onToggle,
  onSelect,
  emptyTitle,
  emptyDescription,
}: StructuresTreeViewProps) {
  if (nodes.length === 0) {
    return (
      <Paper sx={{ p: 4, textAlign: 'center' }}>
        <Typography sx={{ fontWeight: 600 }}>{emptyTitle}</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
          {emptyDescription}
        </Typography>
      </Paper>
    );
  }

  return (
    <Paper sx={{ p: 1, minHeight: 360, maxHeight: 640, overflow: 'auto' }}>
      {nodes.map((node) => (
        <TreeRow
          key={node.structure.idStructure}
          node={node}
          depth={0}
          expanded={expanded}
          selectedId={selectedId}
          onToggle={onToggle}
          onSelect={onSelect}
        />
      ))}
    </Paper>
  );
}
