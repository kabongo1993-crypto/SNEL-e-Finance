import PlayArrowOutlinedIcon from '@mui/icons-material/PlayArrowOutlined';
import { Box, Button, Paper, Stack, Typography } from '@mui/material';
import type { DemandePaiementBatchOpDef } from './demandePaiementBatchConfig';

export interface DemandePaiementBatchActionBarProps {
  selectedCount: number;
  statutLabel: string;
  operations: DemandePaiementBatchOpDef[];
  busy?: boolean;
  onOperation: (op: DemandePaiementBatchOpDef) => void;
  onClear: () => void;
}

export function DemandePaiementBatchActionBar({
  selectedCount,
  statutLabel,
  operations,
  busy,
  onOperation,
  onClear,
}: DemandePaiementBatchActionBarProps) {
  if (selectedCount <= 0 || operations.length === 0) return null;

  return (
    <Paper
      variant="outlined"
      sx={{
        p: 1.5,
        mb: 2,
        position: 'sticky',
        top: 8,
        zIndex: 2,
        bgcolor: 'background.paper',
      }}
    >
      <Stack
        direction={{ xs: 'column', md: 'row' }}
        spacing={1.5}
        sx={{ alignItems: { md: 'center' }, justifyContent: 'space-between' }}
      >
        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            {selectedCount} demande{selectedCount > 1 ? 's' : ''} sélectionnée
            {selectedCount > 1 ? 's' : ''}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Statut : {statutLabel}
          </Typography>
        </Box>
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>
          {operations.map((op) => (
            <Button
              key={op.operation}
              variant="contained"
              size="small"
              disabled={busy}
              startIcon={<PlayArrowOutlinedIcon />}
              onClick={() => onOperation(op)}
            >
              {op.label}
            </Button>
          ))}
          <Button size="small" variant="text" disabled={busy} onClick={onClear}>
            Vider la sélection
          </Button>
        </Stack>
      </Stack>
    </Paper>
  );
}