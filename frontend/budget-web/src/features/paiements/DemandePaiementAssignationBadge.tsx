import { Chip, Stack, Typography } from '@mui/material';
import {
  resolveAssignationDisplay,
  type DemandeAssignationView,
} from './demandePaiementAssignationUtils';

type DemandePaiementAssignationBadgeProps = {
  assignation: DemandeAssignationView | null | undefined;
  /** Affichage compact pour les listes. */
  compact?: boolean;
};

/** Badge pool / nominatif — rien si l'API ne fournit pas l'assignation. */
export function DemandePaiementAssignationBadge({
  assignation,
  compact = false,
}: DemandePaiementAssignationBadgeProps) {
  const display = resolveAssignationDisplay(assignation);
  if (!display) return null;

  if (display.kind === 'pool') {
    if (compact) {
      return (
        <Chip size="small" variant="outlined" label="Pool" sx={{ fontWeight: 600 }} />
      );
    }
    return (
      <Stack spacing={0.25}>
        <Typography variant="caption" color="text.secondary">
          Pool métier
        </Typography>
        <Typography variant="body2" sx={{ fontWeight: 600 }}>
          Non assignée
        </Typography>
      </Stack>
    );
  }

  if (display.kind === 'yours') {
    return (
      <Chip
        size={compact ? 'small' : 'medium'}
        color="primary"
        label="À vous"
        sx={{ fontWeight: 800, letterSpacing: 0.2 }}
      />
    );
  }

  const label = compact ? display.label : `Assignée à : ${display.label}`;
  return (
    <Chip
      size={compact ? 'small' : 'medium'}
      variant="outlined"
      label={label}
      sx={{ fontWeight: 600, maxWidth: compact ? 220 : '100%' }}
    />
  );
}
