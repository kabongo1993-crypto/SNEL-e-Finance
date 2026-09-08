import CalendarMonthOutlinedIcon from '@mui/icons-material/CalendarMonthOutlined';
import EventOutlinedIcon from '@mui/icons-material/EventOutlined';
import FlagOutlinedIcon from '@mui/icons-material/FlagOutlined';
import HistoryIcon from '@mui/icons-material/History';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import PersonOutlinedIcon from '@mui/icons-material/PersonOutlined';
import { Box, Chip, Paper, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import type { VersionBudgetaire } from '../../services/apiClient';
import { formatDateFr } from './versionUtils';

interface VersionsBudgetairesDetailPanelProps {
  selected: VersionBudgetaire | null;
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

function statutColor(statut: string): 'success' | 'warning' | 'info' | 'error' | 'default' {
  switch (statut.toUpperCase()) {
    case 'VALIDEE':
      return 'success';
    case 'SOUMISE':
      return 'info';
    case 'CONTROLEE':
      return 'warning';
    case 'REJETEE':
      return 'error';
    default:
      return 'default';
  }
}

export function VersionsBudgetairesDetailPanel({ selected }: VersionsBudgetairesDetailPanelProps) {
  if (!selected) {
    return (
      <Paper sx={{ height: '100%', minHeight: 280, display: 'grid', placeItems: 'center', p: 2 }}>
        <Typography color="text.secondary" variant="body2" sx={{ textAlign: 'center' }}>
          Sélectionnez une version budgétaire pour afficher ses détails.
        </Typography>
      </Paper>
    );
  }

  const precedente =
    selected.numeroVersionPrecedente != null
      ? `n° ${selected.numeroVersionPrecedente}${selected.libelleVersionPrecedente ? ` — ${selected.libelleVersionPrecedente}` : ''}`
      : '—';

  return (
    <Paper sx={{ height: '100%', minHeight: 280, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <Box sx={{ px: 1.75, pt: 1.75, pb: 1.25, borderBottom: '1px solid', borderColor: 'divider' }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
          Détails de la version
        </Typography>
        <Typography sx={{ fontWeight: 800, fontSize: '1.15rem', letterSpacing: '-0.02em' }}>
          {selected.anneeExercice}
        </Typography>
        <Typography sx={{ fontSize: '0.875rem', fontWeight: 650, lineHeight: 1.35 }}>
          n° {selected.numeroVersion} — {selected.libelle}
        </Typography>
        <Chip
          size="small"
          label={selected.statut}
          color={statutColor(selected.statut)}
          variant="outlined"
          sx={{ mt: 1, fontWeight: 700 }}
        />
      </Box>
      <Box sx={{ flex: 1, overflow: 'auto', px: 1.75, py: 1 }}>
        <InfoRow
          icon={<CalendarMonthOutlinedIcon fontSize="small" />}
          label="Exercice"
          value={`${selected.anneeExercice} (${selected.statutExercice})`}
        />
        <InfoRow icon={<FlagOutlinedIcon fontSize="small" />} label="Numéro" value={String(selected.numeroVersion)} />
        <InfoRow icon={<FlagOutlinedIcon fontSize="small" />} label="Libellé" value={selected.libelle} />
        <InfoRow icon={<HistoryIcon fontSize="small" />} label="Version précédente" value={precedente} />
        <InfoRow icon={<EventOutlinedIcon fontSize="small" />} label="Début d’effet" value={formatDateFr(selected.dateDebutEffet)} />
        <InfoRow icon={<EventOutlinedIcon fontSize="small" />} label="Fin d’effet" value={formatDateFr(selected.dateFinEffet)} />
        <InfoRow icon={<EventOutlinedIcon fontSize="small" />} label="Date de création" value={formatDateFr(selected.dateCreation)} />
        <InfoRow icon={<PersonOutlinedIcon fontSize="small" />} label="Créée par" value={selected.nomUtilisateurCreation} />
        <InfoRow
          icon={<PersonOutlinedIcon fontSize="small" />}
          label="Soumise par"
          value={selected.nomUtilisateurSoumission ?? '—'}
        />
        <InfoRow icon={<EventOutlinedIcon fontSize="small" />} label="Date de soumission" value={formatDateFr(selected.dateSoumission)} />
        <InfoRow
          icon={<PersonOutlinedIcon fontSize="small" />}
          label="Contrôlée par"
          value={selected.nomUtilisateurControle ?? '—'}
        />
        <InfoRow icon={<EventOutlinedIcon fontSize="small" />} label="Date de contrôle" value={formatDateFr(selected.dateControle)} />
        <InfoRow
          icon={<PersonOutlinedIcon fontSize="small" />}
          label="Validée par"
          value={selected.nomUtilisateurValidation ?? '—'}
        />
        <InfoRow icon={<EventOutlinedIcon fontSize="small" />} label="Date de validation" value={formatDateFr(selected.dateValidation)} />
        <InfoRow
          icon={<PersonOutlinedIcon fontSize="small" />}
          label="Rejetée par"
          value={selected.nomUtilisateurRejet ?? '—'}
        />
        <InfoRow icon={<EventOutlinedIcon fontSize="small" />} label="Date de rejet" value={formatDateFr(selected.dateRejet)} />
        <InfoRow
          icon={<FlagOutlinedIcon fontSize="small" />}
          label="Motif de rejet"
          value={selected.motifRejet?.trim() ? selected.motifRejet : '—'}
        />
        <InfoRow icon={<FlagOutlinedIcon fontSize="small" />} label="Motif" value={selected.motif?.trim() ? selected.motif : '—'} />
        <InfoRow
          icon={<PaymentsOutlinedIcon fontSize="small" />}
          label="Prévisions rattachées"
          value={String(selected.nombrePrevisions)}
        />
        <InfoRow
          icon={<PaymentsOutlinedIcon fontSize="small" />}
          label="Transferts rattachés"
          value={String(selected.nombreTransferts)}
        />
        <InfoRow
          icon={<HistoryIcon fontSize="small" />}
          label="Versions suivantes"
          value={String(selected.nombreVersionsSuivantes)}
        />
      </Box>
    </Paper>
  );
}
