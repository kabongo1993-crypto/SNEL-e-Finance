import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import CenterFocusStrongIcon from '@mui/icons-material/CenterFocusStrong';
import FullscreenIcon from '@mui/icons-material/Fullscreen';
import FullscreenExitIcon from '@mui/icons-material/FullscreenExit';
import HubOutlinedIcon from '@mui/icons-material/HubOutlined';
import ZoomInIcon from '@mui/icons-material/ZoomIn';
import ZoomOutIcon from '@mui/icons-material/ZoomOut';
import { Box, Button, ButtonGroup, Paper, Stack, Typography } from '@mui/material';
import {
  Background,
  Controls,
  Handle,
  Position,
  ReactFlow,
  ReactFlowProvider,
  useEdgesState,
  useNodesState,
  useReactFlow,
  type Edge,
  type Node,
  type NodeProps,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { useCallback, useEffect, useMemo, useState } from 'react';
import type { StructureOrganisationnelle } from '../../../services/apiClient';
import { findNode, getAncestorChain, getTypeVisual, type TreeNode } from '../orgUtils';

type OrgNodeData = {
  structure: StructureOrganisationnelle;
  role: 'ancestor' | 'focus' | 'child';
  onSelect: (id: number) => void;
};

function OrgFlowNode({ data }: NodeProps<Node<OrgNodeData>>) {
  const d = data;
  const visual = getTypeVisual(d.structure.typeStructure);
  const isFocus = d.role === 'focus';

  return (
    <Box
      onClick={() => d.onSelect(d.structure.idStructure)}
      sx={{
        width: 180,
        px: 1.25,
        py: 1,
        borderRadius: 1.5,
        bgcolor: 'var(--ef-surface)',
        border: '1px solid',
        borderColor: isFocus ? visual.color : 'var(--ef-border)',
        boxShadow: isFocus ? `0 0 0 2px ${visual.soft}` : 'var(--ef-shadow-sm, none)',
        cursor: 'pointer',
        transition: 'border-color 150ms ease, box-shadow 150ms ease',
        '&:hover': { borderColor: visual.color },
      }}
    >
      <Handle type="target" position={Position.Top} style={{ opacity: 0 }} />
      <Typography
        sx={{
          fontSize: '0.65rem',
          fontWeight: 700,
          color: visual.color,
          letterSpacing: '0.04em',
          textTransform: 'uppercase',
        }}
      >
        {d.structure.typeStructure}
      </Typography>
      <Typography sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700, fontSize: '0.8rem' }}>
        {d.structure.code}
      </Typography>
      <Typography
        sx={{
          fontSize: '0.7rem',
          color: 'text.secondary',
          lineHeight: 1.3,
          display: '-webkit-box',
          WebkitLineClamp: 2,
          WebkitBoxOrient: 'vertical',
          overflow: 'hidden',
        }}
      >
        {d.structure.libelle}
      </Typography>
      <Handle type="source" position={Position.Bottom} style={{ opacity: 0 }} />
    </Box>
  );
}

const nodeTypes = { orgNode: OrgFlowNode };

function buildOrgGraph(
  structures: StructureOrganisationnelle[],
  tree: TreeNode[],
  selectedId: number,
  onSelect: (id: number) => void,
): { nodes: Node[]; edges: Edge[] } {
  const chain = getAncestorChain(structures, selectedId);
  const focusNode = findNode(tree, selectedId);
  const children = focusNode?.children.map((c) => c.structure) ?? [];

  const NODE_W = 180;
  const NODE_H = 78;
  const V_GAP = 90;
  const H_GAP = 24;

  const nodes: Node[] = [];
  const edges: Edge[] = [];

  // Ancestors + focus stacked vertically centered at x=0
  chain.forEach((s, index) => {
    const isFocus = s.idStructure === selectedId;
    nodes.push({
      id: `n-${s.idStructure}`,
      type: 'orgNode',
      position: { x: 0, y: index * (NODE_H + V_GAP) },
      data: {
        structure: s,
        role: isFocus ? 'focus' : 'ancestor',
        onSelect,
      },
      draggable: false,
    });
    if (index > 0) {
      const parent = chain[index - 1]!;
      edges.push({
        id: `e-${parent.idStructure}-${s.idStructure}`,
        source: `n-${parent.idStructure}`,
        target: `n-${s.idStructure}`,
        type: 'smoothstep',
        style: { stroke: 'var(--ef-border-strong)', strokeWidth: 1.5 },
      });
    }
  });

  const focusY = (chain.length - 1) * (NODE_H + V_GAP);
  const childY = focusY + NODE_H + V_GAP;
  const totalWidth = children.length * NODE_W + Math.max(0, children.length - 1) * H_GAP;
  const startX = -totalWidth / 2 + NODE_W / 2;

  children.forEach((child, i) => {
    const x = startX + i * (NODE_W + H_GAP) - NODE_W / 2;
    nodes.push({
      id: `n-${child.idStructure}`,
      type: 'orgNode',
      position: { x, y: childY },
      data: { structure: child, role: 'child', onSelect },
      draggable: false,
    });
    edges.push({
      id: `e-${selectedId}-${child.idStructure}`,
      source: `n-${selectedId}`,
      target: `n-${child.idStructure}`,
      type: 'smoothstep',
      style: { stroke: 'var(--ef-border-strong)', strokeWidth: 1.5 },
    });
  });

  // Center ancestor column on x relative to children spread
  const centerOffset = NODE_W / 2;
  for (const n of nodes) {
    if (chain.some((s) => `n-${s.idStructure}` === n.id)) {
      n.position.x = -centerOffset;
    }
  }

  return { nodes, edges };
}

function OrgChartCanvas({
  structures,
  tree,
  selectedId,
  onSelect,
}: {
  structures: StructureOrganisationnelle[];
  tree: TreeNode[];
  selectedId: number;
  onSelect: (id: number) => void;
}) {
  const { fitView, zoomIn, zoomOut } = useReactFlow();
  const graph = useMemo(
    () => buildOrgGraph(structures, tree, selectedId, onSelect),
    [structures, tree, selectedId, onSelect],
  );
  const [nodes, setNodes, onNodesChange] = useNodesState(graph.nodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(graph.edges);

  useEffect(() => {
    setNodes(graph.nodes);
    setEdges(graph.edges);
    const t = window.setTimeout(() => {
      void fitView({ padding: 0.2, duration: 200 });
    }, 50);
    return () => window.clearTimeout(t);
  }, [graph, setNodes, setEdges, fitView]);

  return (
    <>
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        nodeTypes={nodeTypes}
        fitView
        minZoom={0.35}
        maxZoom={1.6}
        nodesDraggable={false}
        nodesConnectable={false}
        elementsSelectable={false}
        proOptions={{ hideAttribution: true }}
        style={{ background: 'transparent' }}
      >
        <Background gap={18} size={1} color="var(--ef-border)" />
        <Controls showInteractive={false} style={{ display: 'none' }} />
      </ReactFlow>
      <Stack
        direction="row"
        spacing={0.5}
        sx={{ position: 'absolute', right: 12, bottom: 12, zIndex: 5 }}
      >
        <ButtonGroup size="small" variant="outlined">
          <Button onClick={() => zoomIn({ duration: 150 })} aria-label="Zoom +">
            <ZoomInIcon fontSize="small" />
          </Button>
          <Button onClick={() => zoomOut({ duration: 150 })} aria-label="Zoom -">
            <ZoomOutIcon fontSize="small" />
          </Button>
          <Button onClick={() => void fitView({ padding: 0.2, duration: 200 })} aria-label="Recentrer">
            <CenterFocusStrongIcon fontSize="small" />
          </Button>
        </ButtonGroup>
      </Stack>
    </>
  );
}

interface OrgChartPanelProps {
  structures: StructureOrganisationnelle[];
  tree: TreeNode[];
  selectedId: number | null;
  onSelect: (id: number) => void;
  mode: 'organigramme' | 'arbre';
  onModeChange: (mode: 'organigramme' | 'arbre') => void;
}

export function OrgChartPanel({
  structures,
  tree,
  selectedId,
  onSelect,
  mode,
  onModeChange,
}: OrgChartPanelProps) {
  const [fullscreen, setFullscreen] = useState(false);

  const selected = useMemo(
    () => structures.find((s) => s.idStructure === selectedId) ?? null,
    [structures, selectedId],
  );

  const renderMiniTree = useCallback(() => {
    if (selectedId == null) return null;
    const chain = getAncestorChain(structures, selectedId);
    const focus = findNode(tree, selectedId);
    return (
      <Box sx={{ p: 2, overflow: 'auto', height: '100%' }}>
        {chain.map((s, i) => {
          const visual = getTypeVisual(s.typeStructure);
          return (
            <Box key={s.idStructure} sx={{ pl: i * 2, mb: 1 }}>
              <Box
                onClick={() => onSelect(s.idStructure)}
                sx={{
                  display: 'inline-flex',
                  flexDirection: 'column',
                  px: 1.25,
                  py: 0.85,
                  borderRadius: 1.25,
                  border: '1px solid',
                  borderColor: s.idStructure === selectedId ? visual.color : 'divider',
                  bgcolor: 'var(--ef-surface)',
                  cursor: 'pointer',
                  minWidth: 160,
                }}
              >
                <Typography sx={{ fontSize: '0.65rem', color: visual.color, fontWeight: 700 }}>
                  {s.code}
                </Typography>
                <Typography sx={{ fontSize: '0.75rem' }}>{s.libelle}</Typography>
              </Box>
            </Box>
          );
        })}
        {focus && focus.children.length > 0 && (
          <Box sx={{ pl: chain.length * 2, display: 'flex', flexWrap: 'wrap', gap: 1 }}>
            {focus.children.map((c) => {
              const visual = getTypeVisual(c.structure.typeStructure);
              return (
                <Box
                  key={c.structure.idStructure}
                  onClick={() => onSelect(c.structure.idStructure)}
                  sx={{
                    px: 1.25,
                    py: 0.85,
                    borderRadius: 1.25,
                    border: '1px solid',
                    borderColor: 'divider',
                    cursor: 'pointer',
                    minWidth: 140,
                    '&:hover': { borderColor: visual.color },
                  }}
                >
                  <Typography sx={{ fontSize: '0.65rem', color: visual.color, fontWeight: 700 }}>
                    {c.structure.code}
                  </Typography>
                  <Typography sx={{ fontSize: '0.75rem' }}>{c.structure.libelle}</Typography>
                </Box>
              );
            })}
          </Box>
        )}
      </Box>
    );
  }, [structures, tree, selectedId, onSelect]);

  return (
    <Paper
      sx={{
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        minHeight: 0,
        overflow: 'hidden',
        ...(fullscreen
          ? {
              position: 'fixed',
              inset: 12,
              zIndex: 1400,
            }
          : {}),
      }}
    >
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={1}
        sx={{
          px: 1.5,
          py: 1.1,
          borderBottom: '1px solid',
          borderColor: 'divider',
          alignItems: { sm: 'center' },
          justifyContent: 'space-between',
        }}
      >
        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Structure sélectionnée
          </Typography>
          {selected && (
            <Typography variant="caption" color="text.secondary">
              {selected.code} — {selected.libelle}
            </Typography>
          )}
        </Box>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <ButtonGroup size="small" variant="outlined">
            <Button
              startIcon={<HubOutlinedIcon />}
              variant={mode === 'organigramme' ? 'contained' : 'outlined'}
              onClick={() => onModeChange('organigramme')}
            >
              Organigramme
            </Button>
            <Button
              startIcon={<AccountTreeOutlinedIcon />}
              variant={mode === 'arbre' ? 'contained' : 'outlined'}
              onClick={() => onModeChange('arbre')}
            >
              Vue arborescente
            </Button>
          </ButtonGroup>
          <Button
            size="small"
            variant="outlined"
            onClick={() => setFullscreen((v) => !v)}
            aria-label="Plein écran"
          >
            {fullscreen ? <FullscreenExitIcon fontSize="small" /> : <FullscreenIcon fontSize="small" />}
          </Button>
        </Stack>
      </Stack>

      <Box sx={{ flex: 1, minHeight: 320, position: 'relative', bgcolor: 'var(--ef-surface-secondary)' }}>
        {selectedId == null ? (
          <Box sx={{ height: '100%', display: 'grid', placeItems: 'center', p: 3 }}>
            <Typography color="text.secondary" sx={{ textAlign: 'center' }}>
              Sélectionnez une structure dans l’arborescence pour afficher son organigramme.
            </Typography>
          </Box>
        ) : mode === 'arbre' ? (
          renderMiniTree()
        ) : (
          <ReactFlowProvider>
            <OrgChartCanvas
              structures={structures}
              tree={tree}
              selectedId={selectedId}
              onSelect={onSelect}
            />
          </ReactFlowProvider>
        )}
      </Box>
    </Paper>
  );
}
