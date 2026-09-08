import CalendarMonthOutlinedIcon from '@mui/icons-material/CalendarMonthOutlined';
import EventOutlinedIcon from '@mui/icons-material/EventOutlined';
import FlagOutlinedIcon from '@mui/icons-material/FlagOutlined';
import { Box, Chip, Paper, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import type { Exercice } from '../../services/apiClient';
import { formatDateExercice } from './exerciceUtils';

interface ExercicesDetailPanelProps {
  selected: Exercice | null;
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

export function ExercicesDetailPanel({ selected }: ExercicesDetailPanelProps) {
  if (!selected) {
    return (
      <Paper sx={{ height: '100%', minHeight: 280, display: 'grid', placeItems: 'center', p: 2 }}>
        <Typography color="text.secondary" variant="body2" sx={{ textAlign: 'center' }}>
          Sélectionnez un exercice pour afficher ses détails.
        </Typography>
      </Paper>
    );
  }

  const ouvert = selected.statut.toUpperCase() === 'OUVERT';

  return (
    <Paper sx={{ height: '100%', minHeight: 280, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <Box sx={{ px: 1.75, pt: 1.75, pb: 1.25, borderBottom: '1px solid', borderColor: 'divider' }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
          Détails de l’exercice
        </Typography>
        <Typography sx={{ fontWeight: 800, fontSize: '1.35rem', letterSpacing: '-0.02em', lineHeight: 1.2 }}>
          {selected.annee}
        </Typography>
        <Chip
          size="small"
          label={selected.statut}
          color={ouvert ? 'success' : 'default'}
          variant={ouvert ? 'outlined' : 'filled'}
          sx={{ mt: 1, fontWeight: 700 }}
        />
      </Box>
      <Box sx={{ flex: 1, overflow: 'auto', px: 1.75, py: 1 }}>
        <InfoRow icon={<CalendarMonthOutlinedIcon fontSize="small" />} label="Année" value={String(selected.annee)} />
        <InfoRow icon={<FlagOutlinedIcon fontSize="small" />} label="Statut" value={selected.statut} />
        <InfoRow
          icon={<EventOutlinedIcon fontSize="small" />}
          label="Date d’ouverture"
          value={formatDateExercice(selected.dateOuverture)}
        />
        <InfoRow
          icon={<EventOutlinedIcon fontSize="small" />}
          label="Date de clôture"
          value={formatDateExercice(selected.dateCloture)}
        />
        <InfoRow
          icon={<CalendarMonthOutlinedIcon fontSize="small" />}
          label="Identifiant technique"
          value={String(selected.idExercice)}
        />

        <Box sx={{ mt: 2, pt: 1.5, borderTop: '1px dashed', borderColor: 'divider' }}>
          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, letterSpacing: 0.3 }}>
            Versions budgétaires
          </Typography>
          <Typography variant="body2" sx={{ mt: 0.75, fontWeight: 600 }}>
            {selected.nombreVersions} version{selected.nombreVersions > 1 ? 's' : ''} liée
            {selected.nombreVersions > 1 ? 's' : ''}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            Le détail des versions sera disponible dans un module dédié.
          </Typography>
        </Box>
      </Box>
    </Paper>
  );
}
