import CodeOutlinedIcon from '@mui/icons-material/CodeOutlined';
import EventOutlinedIcon from '@mui/icons-material/EventOutlined';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import SortOutlinedIcon from '@mui/icons-material/SortOutlined';
import { Box, Chip, Paper, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import type { TypeBudget } from '../../services/apiClient';
import { libelleStatut } from './typeBudgetUtils';

interface TypesBudgetDetailPanelProps {
  selected: TypeBudget | null;
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

export function TypesBudgetDetailPanel({ selected }: TypesBudgetDetailPanelProps) {
  if (!selected) {
    return (
      <Paper sx={{ height: '100%', minHeight: 280, display: 'grid', placeItems: 'center', p: 2 }}>
        <Typography color="text.secondary" variant="body2" sx={{ textAlign: 'center' }}>
          Sélectionnez un type de budget pour afficher ses détails.
        </Typography>
      </Paper>
    );
  }

  const previsions = selected.nombrePrevisions ?? 0;

  return (
    <Paper sx={{ height: '100%', minHeight: 280, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <Box sx={{ px: 1.75, pt: 1.75, pb: 1.25, borderBottom: '1px solid', borderColor: 'divider' }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
          Détails du type de budget
        </Typography>
        <Typography sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700, fontSize: '0.9rem' }}>
          {selected.codeType}
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
        <InfoRow icon={<CodeOutlinedIcon fontSize="small" />} label="Code" value={selected.codeType} />
        <InfoRow icon={<CodeOutlinedIcon fontSize="small" />} label="Libellé" value={selected.libelle} />
        <InfoRow
          icon={<SortOutlinedIcon fontSize="small" />}
          label="Ordre d’affichage"
          value={String(selected.ordreAffichage)}
        />
        <InfoRow icon={<EventOutlinedIcon fontSize="small" />} label="Statut" value={libelleStatut(selected.actif)} />
        <InfoRow
          icon={<PaymentsOutlinedIcon fontSize="small" />}
          label="Prévisions rattachées"
          value={String(previsions)}
        />
      </Box>
    </Paper>
  );
}
