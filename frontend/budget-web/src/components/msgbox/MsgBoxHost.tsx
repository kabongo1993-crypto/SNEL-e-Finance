import CheckCircleOutlinedIcon from '@mui/icons-material/CheckCircleOutlined';
import ErrorOutlinedIcon from '@mui/icons-material/ErrorOutlined';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import WarningAmberOutlinedIcon from '@mui/icons-material/WarningAmberOutlined';
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Stack,
  Typography,
} from '@mui/material';
import { ConfirmDialog } from '../ConfirmDialog';
import { efZIndex } from '../../theme/tokens';
import type { MsgBoxQueueItem, MsgBoxSeverity } from './MsgBoxContext';

const SEVERITY_COLOR: Record<MsgBoxSeverity, 'success' | 'error' | 'warning' | 'info'> = {
  success: 'success',
  error: 'error',
  warning: 'warning',
  info: 'info',
};

function SeverityIcon({ severity }: { severity: MsgBoxSeverity }) {
  const color = SEVERITY_COLOR[severity];
  const sx = { fontSize: 28 };
  switch (severity) {
    case 'success':
      return <CheckCircleOutlinedIcon color={color} sx={sx} />;
    case 'error':
      return <ErrorOutlinedIcon color={color} sx={sx} />;
    case 'warning':
      return <WarningAmberOutlinedIcon color={color} sx={sx} />;
    default:
      return <InfoOutlinedIcon color={color} sx={sx} />;
  }
}

type MsgBoxHostProps = {
  current: MsgBoxQueueItem | null;
};

/**
 * Host unique — affiche le message courant (file d’attente gérée par le Provider).
 * Confirmations : réutilise ConfirmDialog (pas de duplication).
 */
export function MsgBoxHost({ current }: MsgBoxHostProps) {
  if (!current) return null;

  if (current.kind === 'confirm') {
    return (
      <ConfirmDialog
        open
        elevate
        title={current.title}
        message={current.message}
        danger={current.danger}
        confirmLabel={current.confirmLabel}
        cancelLabel={current.cancelLabel}
        onConfirm={() => current.resolve(true)}
        onClose={() => current.resolve(false)}
      />
    );
  }

  const { severity, title, message, okLabel, resolve } = current;

  return (
    <Dialog
      open
      onClose={() => resolve()}
      maxWidth="xs"
      fullWidth
      aria-labelledby="msgbox-title"
      aria-describedby="msgbox-desc"
      slotProps={{
        root: {
          sx: { zIndex: efZIndex.toast },
        },
      }}
    >
      <DialogTitle id="msgbox-title" sx={{ pr: 2 }}>
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'flex-start' }}>
          <Box sx={{ mt: 0.25, flexShrink: 0 }} aria-hidden>
            <SeverityIcon severity={severity} />
          </Box>
          <Typography component="span" variant="h6" sx={{ fontWeight: 700, lineHeight: 1.3 }}>
            {title}
          </Typography>
        </Stack>
      </DialogTitle>
      <DialogContent>
        <DialogContentText
          id="msgbox-desc"
          component="div"
          sx={{
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-word',
            maxHeight: { xs: '50vh', sm: '60vh' },
            overflow: 'auto',
            color: 'text.primary',
          }}
        >
          {message}
        </DialogContentText>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button variant="contained" color="primary" onClick={() => resolve()} autoFocus>
          {okLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
