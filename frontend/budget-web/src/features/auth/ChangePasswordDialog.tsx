import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
} from '@mui/material';
import { useState } from 'react';
import { useMsgBox } from '../../components';
import { changePasswordRequest } from '../../services/apiClient';

interface ChangePasswordDialogProps {
  open: boolean;
  onClose: () => void;
}

export function ChangePasswordDialog({ open, onClose }: ChangePasswordDialogProps) {
  const msgBox = useMsgBox();
  const [actuel, setActuel] = useState('');
  const [nouveau, setNouveau] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [busy, setBusy] = useState(false);

  const reset = () => {
    setActuel('');
    setNouveau('');
    setConfirmation('');
    setBusy(false);
  };

  const handleClose = () => {
    if (busy) return;
    reset();
    onClose();
  };

  const handleSubmit = async () => {
    if (!actuel || !nouveau || !confirmation) {
      void msgBox.error('Tous les champs sont obligatoires.');
      return;
    }
    if (nouveau.length < 8) {
      void msgBox.error('Le nouveau mot de passe doit contenir au moins 8 caractères.');
      return;
    }
    if (nouveau !== confirmation) {
      void msgBox.error('La confirmation ne correspond pas au nouveau mot de passe.');
      return;
    }
    if (actuel === nouveau) {
      void msgBox.error('Le nouveau mot de passe doit être différent de l’actuel.');
      return;
    }

    setBusy(true);
    try {
      await changePasswordRequest({
        motDePasseActuel: actuel,
        nouveauMotDePasse: nouveau,
        confirmationMotDePasse: confirmation,
      });
      void msgBox.success('Mot de passe mis à jour. Utilisez-le à la prochaine connexion.');
      reset();
      onClose();
    } catch (e: unknown) {
      const msg =
        (e as { response?: { data?: { message?: string } } })?.response?.data?.message ||
        (e as Error)?.message ||
        'Impossible de changer le mot de passe.';
      void msgBox.error(msg);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="xs" fullWidth>
      <DialogTitle>Changer le mot de passe</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <TextField
            label="Mot de passe actuel"
            type="password"
            autoComplete="current-password"
            value={actuel}
            onChange={(e) => setActuel(e.target.value)}
            disabled={busy}
            fullWidth
            required
          />
          <TextField
            label="Nouveau mot de passe"
            type="password"
            autoComplete="new-password"
            value={nouveau}
            onChange={(e) => setNouveau(e.target.value)}
            disabled={busy}
            fullWidth
            required
            helperText="Au moins 8 caractères"
          />
          <TextField
            label="Confirmer le nouveau mot de passe"
            type="password"
            autoComplete="new-password"
            value={confirmation}
            onChange={(e) => setConfirmation(e.target.value)}
            disabled={busy}
            fullWidth
            required
          />
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={handleClose} disabled={busy}>
          Annuler
        </Button>
        <Button variant="contained" onClick={() => void handleSubmit()} disabled={busy}>
          {busy ? 'Enregistrement…' : 'Enregistrer'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
