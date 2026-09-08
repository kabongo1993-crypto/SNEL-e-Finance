import { useEffect, useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import type { DemandePaiementBatchOpDef } from './demandePaiementBatchConfig';

export interface RetourDemandePaiementPayload {
  motifRetour: string;
  commentaireRetour?: string | null;
}

export interface DemandePaiementBatchRejetDialogProps {
  open: boolean;
  busy?: boolean;
  selectedCount: number;
  operation: DemandePaiementBatchOpDef | null;
  onConfirm: (retour: RetourDemandePaiementPayload) => void;
  onClose: () => void;
}

export function DemandePaiementBatchRejetDialog({
  open,
  busy,
  selectedCount,
  operation,
  onConfirm,
  onClose,
}: DemandePaiementBatchRejetDialogProps) {
  const [motifRetour, setMotifRetour] = useState('');
  const [commentaireRetour, setCommentaireRetour] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setMotifRetour('');
    setCommentaireRetour('');
    setError(null);
  }, [open]);

  const label = operation?.label ?? 'Rejeter';

  const handleConfirm = () => {
    const motif = motifRetour.trim();
    if (!motif) {
      setError('Le motif de retour est obligatoire.');
      return;
    }
    onConfirm({
      motifRetour: motif,
      commentaireRetour: commentaireRetour.trim() || null,
    });
  };

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>{label} ?</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Typography variant="body2">
            Vous allez rejeter {selectedCount} demande{selectedCount > 1 ? 's' : ''} de paiement.
            Le motif s’applique à chaque DPM sélectionnée ; le traitement reste individuel.
          </Typography>
          <TextField
            label="Motif de retour"
            required
            fullWidth
            multiline
            minRows={2}
            value={motifRetour}
            onChange={(e) => {
              setMotifRetour(e.target.value);
              if (error) setError(null);
            }}
            error={Boolean(error)}
            helperText={error ?? undefined}
            disabled={busy}
          />
          <TextField
            label="Commentaire (optionnel)"
            fullWidth
            multiline
            minRows={2}
            value={commentaireRetour}
            onChange={(e) => setCommentaireRetour(e.target.value)}
            disabled={busy}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>
          Annuler
        </Button>
        <Button variant="contained" color="error" onClick={handleConfirm} disabled={busy}>
          {label}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
