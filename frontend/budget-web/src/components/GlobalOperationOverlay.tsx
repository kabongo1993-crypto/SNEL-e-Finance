import { Backdrop, Box, CircularProgress, Typography } from '@mui/material';
import { efZIndex } from '../theme/tokens';

export const DEFAULT_OPERATION_OVERLAY_MESSAGE = 'Opération';

export interface GlobalOperationOverlayProps {
  open: boolean;
  /** Ligne principale (ex. « Envoi en validation »). « en cours… » est ajouté automatiquement. */
  message?: string;
}

/**
 * Overlay plein écran : fond estompé, blocage des interactions, spinner + message.
 * Réutilise les tokens charte (`--ef-overlay`) et MUI Backdrop.
 */
export function GlobalOperationOverlay({
  open,
  message = DEFAULT_OPERATION_OVERLAY_MESSAGE,
}: GlobalOperationOverlayProps) {
  if (!open) return null;

  return (
    <Backdrop
      open
      sx={{
        zIndex: efZIndex.toast,
        bgcolor: 'var(--ef-overlay)',
        flexDirection: 'column',
        gap: 2,
      }}
      aria-busy
      aria-live="polite"
      aria-label={`${message} en cours`}
    >
      <CircularProgress size={44} sx={{ color: 'common.white' }} />
      <Box sx={{ textAlign: 'center', color: 'common.white', px: 2, maxWidth: 360 }}>
        <Typography variant="subtitle1" component="p" sx={{ fontWeight: 600 }}>
          {message}
        </Typography>
        <Typography variant="body2" sx={{ opacity: 0.88, mt: 0.5 }} component="p">
          en cours…
        </Typography>
      </Box>
    </Backdrop>
  );
}
