import CodeOutlinedIcon from '@mui/icons-material/CodeOutlined';
import EventOutlinedIcon from '@mui/icons-material/EventOutlined';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import { Box, Chip, Paper, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { formatDateFr } from '../../mocks/types';
import type { Departement } from '../../services/apiClient';
import { libelleStatut } from './deptUtils';

interface DepartementsDetailPanelProps {
  selected: Departement | null;
}

function InfoRow({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  return (
    <Stack direction="row" spacing={1.25} sx={{ alignItems: 'flex-start', py: 0.85 }}>
      <Box sx={{ color: 'text.secondary', mt: 0.15 }}>{icon}</Box>
      <Box sx={{ minWidth: 0 }}>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
          {label}
        </Typography>
        <Typography variant="body2" sx={{ fontWeight: 600, wordBreak: 'break-word' }}>
          {value}
        </Typography>
      </Box>
    </Stack>
  );
}

export function DepartementsDetailPanel({ selected }: DepartementsDetailPanelProps) {
  if (!selected) {
    return (
      <Paper sx={{ height: '100%', minHeight: 280, display: 'grid', placeItems: 'center', p: 2 }}>
        <Typography color="text.secondary" variant="body2" sx={{ textAlign: 'center' }}>
          Sélectionnez un département pour afficher ses détails.
        </Typography>
      </Paper>
    );
  }

  return (
    <Paper sx={{ height: '100%', minHeight: 280, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <Box sx={{ px: 1.75, pt: 1.75, pb: 1.25, borderBottom: '1px solid', borderColor: 'divider' }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
          Détails du département
        </Typography>
        <Typography sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700, fontSize: '0.9rem' }}>
          {selected.code}
        </Typography>
        <Typography sx={{ fontSize: '0.875rem', fontWeight: 650, lineHeight: 1.35 }}>
          {selected.libelle}
        </Typography>
        <Chip
          size="small"
          label={libelleStatut(selected.actif)}
          color={selected.actif ? 'success' : 'default'}
          variant={selected.actif ? 'outlined' : 'filled'}
          sx={{ mt: 1, fontWeight: 700 }}
        />
      </Box>
      <Box sx={{ flex: 1, overflow: 'auto', px: 1.75, py: 1 }}>
        <InfoRow icon={<CodeOutlinedIcon fontSize="small" />} label="Code" value={selected.code} />
        <InfoRow icon={<CodeOutlinedIcon fontSize="small" />} label="Libellé" value={selected.libelle} />
        <InfoRow icon={<EventOutlinedIcon fontSize="small" />} label="Statut" value={libelleStatut(selected.actif)} />
        <InfoRow
          icon={<EventOutlinedIcon fontSize="small" />}
          label="Date de création"
          value={formatDateFr(selected.dateCreation)}
        />
        <InfoRow
          icon={<PaymentsOutlinedIcon fontSize="small" />}
          label="Unités budgétaires rattachées"
          value={String(selected.nombreUnitesBudgetaires ?? 0)}
        />
      </Box>
    </Paper>
  );
}
