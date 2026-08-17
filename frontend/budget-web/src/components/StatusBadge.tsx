import { Chip } from '@mui/material';
import { STATUS_LABELS, STATUS_TONES, type EntityStatus, type StatusTone } from '../types/status';

const toneColor: Record<StatusTone, 'default' | 'info' | 'warning' | 'success' | 'error' | 'secondary'> = {
  default: 'default',
  info: 'info',
  warning: 'warning',
  success: 'success',
  error: 'error',
  secondary: 'secondary',
};

interface StatusBadgeProps {
  status: EntityStatus;
  size?: 'small' | 'medium';
}

export function StatusBadge({ status, size = 'small' }: StatusBadgeProps) {
  return (
    <Chip
      size={size}
      label={STATUS_LABELS[status]}
      color={toneColor[STATUS_TONES[status]]}
      variant={status === 'brouillon' || status === 'annule' ? 'outlined' : 'filled'}
      sx={{ fontWeight: 600, height: size === 'small' ? 24 : 28 }}
    />
  );
}
