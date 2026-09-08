import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from '@mui/material';
import type { DemandePaiementBatchResult } from './demandePaiementBatchApi';
import { getBatchOpLabel, type DemandePaiementBatchOperationRoute } from './demandePaiementBatchConfig';

export interface DemandePaiementBatchResultDialogProps {
  open: boolean;
  result: DemandePaiementBatchResult | null;
  onClose: () => void;
}

function Section({
  title,
  rows,
  empty,
}: {
  title: string;
  rows: DemandePaiementBatchResult['details'];
  empty: string;
}) {
  return (
    <Box sx={{ mt: 2 }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
        {title} ({rows.length})
      </Typography>
      {rows.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          {empty}
        </Typography>
      ) : (
        <Stack spacing={1}>
          {rows.map((d) => (
            <Box
              key={d.idDemandePaiement}
              sx={{
                p: 1,
                borderRadius: 1,
                border: '1px solid',
                borderColor: 'divider',
              }}
            >
              <Typography variant="body2" sx={{ fontWeight: 700 }}>
                {d.reference ?? `#${d.idDemandePaiement}`}
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                {d.statutAvant}
                {d.statutApres ? ` → ${d.statutApres}` : ''}
              </Typography>
              {d.codeErreur && (
                <Typography variant="caption" color="error.main" sx={{ display: 'block', mt: 0.5 }}>
                  {d.codeErreur}
                </Typography>
              )}
              {d.message && (
                <Typography variant="body2" sx={{ mt: 0.5 }}>
                  {d.message}
                </Typography>
              )}
            </Box>
          ))}
        </Stack>
      )}
    </Box>
  );
}

export function DemandePaiementBatchResultDialog({
  open,
  result,
  onClose,
}: DemandePaiementBatchResultDialogProps) {
  if (!result) return null;

  const success = result.details.filter((d) => d.outcome === 'SUCCESS');
  const ignored = result.details.filter((d) => d.outcome === 'IGNORED');
  const errors = result.details.filter((d) => d.outcome === 'ERROR');
  const opLabel = getBatchOpLabel(result.operation as DemandePaiementBatchOperationRoute);

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Traitement terminé</DialogTitle>
      <DialogContent dividers>
        <Typography variant="body2" sx={{ mb: 1 }}>
          Opération : {opLabel}
        </Typography>
        <Stack direction="row" spacing={2} useFlexGap sx={{ flexWrap: 'wrap' }}>
          <Typography variant="body2">{result.totalSelectionne} sélectionnées</Typography>
          <Typography variant="body2" color="success.main">
            {result.reussies} réussies
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {result.ignorees} ignorée{result.ignorees > 1 ? 's' : ''}
          </Typography>
          <Typography variant="body2" color="error.main">
            {result.erreurs} erreur{result.erreurs > 1 ? 's' : ''}
          </Typography>
        </Stack>

        <Section title="✓ Réussies" rows={success} empty="Aucune." />
        <Section title="↷ Ignorées" rows={ignored} empty="Aucune." />
        <Section title="✕ Erreurs" rows={errors} empty="Aucune." />

        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 2 }}>
          CorrelationId : {result.correlationId}
        </Typography>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} variant="contained">
          Fermer
        </Button>
      </DialogActions>
    </Dialog>
  );
}