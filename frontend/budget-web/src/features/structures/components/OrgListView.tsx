import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import { Box, Button, Chip, Paper, TextField, Typography } from '@mui/material';
import { useMemo, useState } from 'react';
import { DataTable, type DataTableColumn } from '../../../components';
import type { StructureOrganisationnelle } from '../../../services/apiClient';
import { getTypeVisual } from '../orgUtils';

interface OrgListViewProps {
  structures: StructureOrganisationnelle[];
  selectedId: number | null;
  onSelect: (id: number) => void;
  onBackToTree: () => void;
}

type Row = StructureOrganisationnelle & { id: string };

export function OrgListView({ structures, selectedId, onSelect, onBackToTree }: OrgListViewProps) {
  const [search, setSearch] = useState('');

  const rows: Row[] = useMemo(() => {
    const q = search.trim().toLowerCase();
    return structures
      .filter((s) => {
        if (!q) return true;
        return (
          s.code.toLowerCase().includes(q) ||
          s.libelle.toLowerCase().includes(q) ||
          s.typeStructure.toLowerCase().includes(q) ||
          (s.parentCode?.toLowerCase().includes(q) ?? false) ||
          (s.departementCode?.toLowerCase().includes(q) ?? false)
        );
      })
      .map((s) => ({ ...s, id: String(s.idStructure) }));
  }, [structures, search]);

  const columns: DataTableColumn<Row>[] = [
    {
      id: 'type',
      label: 'Type',
      sortable: true,
      sortValue: (r) => r.typeStructure,
      mobile: 'meta',
      render: (r) => {
        const v = getTypeVisual(r.typeStructure);
        return (
          <Chip size="small" label={r.typeStructure} sx={{ bgcolor: v.soft, color: v.color, fontWeight: 650 }} />
        );
      },
    },
    {
      id: 'code',
      label: 'Code',
      sortable: true,
      sortValue: (r) => r.code,
      mobile: 'title',
      render: (r) => (
        <Typography variant="body2" sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700 }}>
          {r.code}
        </Typography>
      ),
    },
    {
      id: 'libelle',
      label: 'Libellé',
      sortable: true,
      sortValue: (r) => r.libelle,
      mobile: 'subtitle',
      render: (r) => r.libelle,
    },
    { id: 'parent', label: 'Parent', mobile: 'meta', render: (r) => r.parentCode ?? '—' },
    { id: 'dept', label: 'Département', mobile: 'hidden', render: (r) => r.departementCode ?? '—' },
    {
      id: 'statut',
      label: 'Statut',
      mobile: 'meta',
      render: () => <Chip size="small" label="Actif" color="success" variant="outlined" />,
    },
    {
      id: 'ub',
      label: 'UB',
      align: 'right',
      sortable: true,
      sortValue: (r) => r.nombreUnitesBudgetaires,
      mobile: 'hidden',
      render: (r) => r.nombreUnitesBudgetaires,
    },
  ];

  return (
    <Paper sx={{ p: 1.5 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1.5, gap: 1 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
          Vue liste des structures
        </Typography>
        <Button size="small" variant="outlined" startIcon={<AccountTreeOutlinedIcon />} onClick={onBackToTree}>
          Vue arborescence
        </Button>
      </Box>
      <TextField
        size="small"
        fullWidth
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Filtrer la liste…"
        sx={{ mb: 1.5 }}
      />
      <DataTable
        columns={columns}
        rows={rows}
        actions={[{ id: 'voir', label: 'Voir', onClick: (r) => onSelect(r.idStructure) }]}
      />
      {selectedId != null && (
        <Typography variant="caption" color="text.secondary" sx={{ mt: 1, display: 'block' }}>
          Structure sélectionnée : #{selectedId}
        </Typography>
      )}
    </Paper>
  );
}
