import CategoryOutlinedIcon from '@mui/icons-material/CategoryOutlined';
import CodeOutlinedIcon from '@mui/icons-material/CodeOutlined';
import FolderOutlinedIcon from '@mui/icons-material/FolderOutlined';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import EventOutlinedIcon from '@mui/icons-material/EventOutlined';
import { Box, Chip, Paper, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { formatDateFr } from '../../mocks/types';
import type { Structure } from '../../services/apiClient';
import { getTypeVisual } from '../structures/orgUtils';
import { extraireCodeAffichage, libelleParent, libelleStatut } from './structureUtils';

interface StructuresDetailPanelProps {
  selected: Structure | null;
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

export function StructuresDetailPanel({ selected }: StructuresDetailPanelProps) {
  if (!selected) {
    return (
      <Paper sx={{ height: '100%', minHeight: 280, display: 'grid', placeItems: 'center', p: 2 }}>
        <Typography color="text.secondary" variant="body2" sx={{ textAlign: 'center' }}>
          Sélectionnez une structure pour afficher ses détails.
        </Typography>
      </Paper>
    );
  }

  const visual = getTypeVisual(selected.typeStructure);
  const codeAffichage = extraireCodeAffichage(selected.code);

  return (
    <Paper sx={{ height: '100%', minHeight: 280, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <Box sx={{ px: 1.75, pt: 1.75, pb: 1.25, borderBottom: '1px solid', borderColor: 'divider' }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
          Détails de la structure
        </Typography>
        <Chip
          size="small"
          label={visual.label}
          sx={{ bgcolor: visual.soft, color: visual.color, fontWeight: 700, mb: 1 }}
        />
        <Typography sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700, fontSize: '0.85rem' }}>
          {codeAffichage}
        </Typography>
        <Typography sx={{ fontSize: '0.875rem', fontWeight: 650, lineHeight: 1.35 }}>
          {selected.libelle}
        </Typography>
      </Box>
      <Box sx={{ flex: 1, overflow: 'auto', px: 1.75, py: 1 }}>
        <InfoRow icon={<CategoryOutlinedIcon fontSize="small" />} label="Type" value={visual.label} />
        <InfoRow icon={<CodeOutlinedIcon fontSize="small" />} label="Code" value={codeAffichage} />
        {codeAffichage !== selected.code && (
          <InfoRow icon={<CodeOutlinedIcon fontSize="small" />} label="Code technique" value={selected.code} />
        )}
        <InfoRow icon={<FolderOutlinedIcon fontSize="small" />} label="Structure parente" value={libelleParent(selected)} />
        <InfoRow
          icon={<EventOutlinedIcon fontSize="small" />}
          label="Statut"
          value={libelleStatut(selected.actif)}
        />
        <InfoRow
          icon={<EventOutlinedIcon fontSize="small" />}
          label="Date de création"
          value={formatDateFr(selected.dateCreation)}
        />
        <InfoRow
          icon={<AccountTreeOutlinedIcon fontSize="small" />}
          label="Sous-structures"
          value={String(selected.nombreEnfants)}
        />
        <InfoRow
          icon={<PaymentsOutlinedIcon fontSize="small" />}
          label="Unités budgétaires rattachées"
          value={String(selected.nombreUnitesBudgetaires)}
        />
      </Box>
    </Paper>
  );
}
