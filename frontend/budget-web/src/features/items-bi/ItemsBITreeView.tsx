import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { Box, Paper, Typography } from '@mui/material';
import type { ItemBITreeNode } from './itemBIUtils';

interface ItemsBITreeViewProps {
  nodes: ItemBITreeNode[];
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
  node: ItemBITreeNode;
  depth: number;
  expanded: Set<number>;
  selectedId: number | null;
  onToggle: (id: number) => void;
  onSelect: (id: number) => void;
}) {
  const hasChildren = node.children.length > 0;
  const isExpanded = expanded.has(node.item.idItemBI);
  const isSelected = selectedId === node.item.idItemBI;

  return (
    <Box>
      <Box
        onClick={() => onSelect(node.item.idItemBI)}
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
          '&:hover': { bgcolor: isSelected ? 'action.selected' : 'action.hover' },
        }}
      >
        <Box
          component="span"
          onClick={(e) => {
            e.stopPropagation();
            if (hasChildren) onToggle(node.item.idItemBI);
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
            minWidth: 48,
            flexShrink: 0,
          }}
        >
          {node.item.codeItem}
        </Typography>
        <Typography noWrap sx={{ flex: 1, fontSize: '0.8rem', fontWeight: isSelected ? 650 : 500, minWidth: 0 }}>
          {node.item.libelle}
        </Typography>
        {!node.item.actif && (
          <Typography variant="caption" color="text.secondary" sx={{ flexShrink: 0 }}>
            Inactif
          </Typography>
        )}
      </Box>
      {hasChildren && isExpanded &&
        node.children.map((child) => (
          <TreeRow
            key={child.item.idItemBI}
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

export function ItemsBITreeView({
  nodes,
  expanded,
  selectedId,
  onToggle,
  onSelect,
  emptyTitle,
  emptyDescription,
}: ItemsBITreeViewProps) {
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
          key={node.item.idItemBI}
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
