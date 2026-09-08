import { Chip } from '@mui/material';
import { labelStatutDpm, statutDpmChipSx } from './paiementUtils';

interface DemandePaiementStatusBadgeProps {
  statut: string | null | undefined;
  size?: 'small' | 'medium';
}

/** Badge de statut DPM (phase visa budgétaire uniquement). */
export function DemandePaiementStatusBadge({
  statut,
  size = 'small',
}: DemandePaiementStatusBadgeProps) {
  const sx = statutDpmChipSx(statut);
  return (
    <Chip
      size={size}
      label={labelStatutDpm(statut)}
      variant="outlined"
      sx={{
        fontWeight: 600,
        height: size === 'small' ? 24 : 28,
        bgcolor: sx.bgcolor,
        color: sx.color,
        borderColor: sx.borderColor,
      }}
    />
  );
}

/** Alias demandé dans le périmètre. */
export { DemandePaiementStatusBadge as StatusBadge };
