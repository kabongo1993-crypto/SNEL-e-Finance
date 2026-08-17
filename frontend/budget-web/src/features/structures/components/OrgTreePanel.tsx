import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ViewListOutlinedIcon from '@mui/icons-material/ViewListOutlined';
import { Box, Button, Chip, Paper, Stack, Typography } from '@mui/material';
import { getTypeVisual, type TreeNode } from '../orgUtils';

interface OrgTreePanelProps {
  nodes: TreeNode[];
  expanded: Set<number>;
  selectedId: number | null;
  onToggle: (id: number) => void;
  onSelect: (id: number) => void;
  onSwitchToList: () => void;
}

function TreeRow({
  node,
  depth,
  expanded,
  selectedId,
  onToggle,
  onSelect,
}: {
  node: TreeNode;
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
  const count = node.structure.nombreEnfants || node.structure.nombreUnitesBudgetaires;

  return (
    <Box>
      <Box
        onClick={() => onSelect(node.structure.idStructure)}
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 0.5,
          pl: 0.75 + depth * 1.4,
          pr: 0.75,
          py: 0.4,
          cursor: 'pointer',
          borderRadius: 1,
          bgcolor: isSelected ? 'action.selected' : 'transparent',
          borderLeft: '2px solid',
          borderColor: isSelected ? visual.color : 'transparent',
          transition: 'background-color 120ms ease',
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
            fontSize: '0.7rem',
            fontWeight: 700,
            color: visual.color,
            minWidth: 36,
            flexShrink: 0,
          }}
        >
          {node.structure.code}
        </Typography>
        <Typography
          noWrap
          sx={{
            flex: 1,
            fontSize: '0.75rem',
            fontWeight: isSelected ? 650 : 500,
            minWidth: 0,
          }}
        >
          {node.structure.libelle}
        </Typography>
        {count > 0 && (
          <Typography
            component="span"
            sx={{ fontSize: '0.65rem', color: 'text.secondary', fontWeight: 600, flexShrink: 0 }}
          >
            {count}
          </Typography>
        )}
      </Box>
      {hasChildren && isExpanded && (
        <Box>
          {node.children.map((child) => (
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
      )}
    </Box>
  );
}

export function OrgTreePanel({
  nodes,
  expanded,
  selectedId,
  onToggle,
  onSelect,
  onSwitchToList,
}: OrgTreePanelProps) {
  return (
    <Paper
      sx={{
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        minHeight: 0,
        overflow: 'hidden',
      }}
    >
      <Box sx={{ px: 1.5, py: 1.25, borderBottom: '1px solid', borderColor: 'divider' }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          Arborescence organisationnelle
        </Typography>
        <Stack direction="row" spacing={0.5} sx={{ mt: 0.75, flexWrap: 'wrap' }} useFlexGap>
          {['ENTITE', 'DEPARTEMENT', 'DIRECTION', 'DIVISION'].map((t) => {
            const v = getTypeVisual(t);
            return <Chip key={t} size="small" label={v.label} sx={{ bgcolor: v.soft, color: v.color, height: 20 }} />;
          })}
        </Stack>
      </Box>
      <Box sx={{ flex: 1, overflow: 'auto', py: 0.75, px: 0.5 }}>
        {nodes.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>
            Aucun résultat pour les filtres sélectionnés.
          </Typography>
        ) : (
          nodes.map((node) => (
            <TreeRow
              key={node.structure.idStructure}
              node={node}
              depth={0}
              expanded={expanded}
              selectedId={selectedId}
              onToggle={onToggle}
              onSelect={onSelect}
            />
          ))
        )}
      </Box>
      <Box sx={{ p: 1, borderTop: '1px solid', borderColor: 'divider' }}>
        <Button
          fullWidth
          size="small"
          variant="outlined"
          startIcon={<ViewListOutlinedIcon />}
          onClick={onSwitchToList}
        >
          Vue liste
        </Button>
      </Box>
    </Paper>
  );
}
