import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  List,
  ListItem,
  ListItemText,
  Stack,
  Typography,
} from '@mui/material';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import {
  fetchClassementAe,
  reorderClassementAe,
  type ClassementAeLigne,
} from '../../services/apiClient';
import { PrimaryButton, SecondaryButton, useMsgBox } from '../../components';

function apiErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const data = (err as { response?: { data?: { message?: string } } }).response?.data;
    if (data?.message) return data.message;
  }
  return fallback;
}

type Props = {
  open: boolean;
  onClose: () => void;
  idVersion: number;
  idUB: number;
  codeUB?: string;
};

export function ClassementAeDialog({ open, onClose, idVersion, idUB, codeUB }: Props) {
  const msgBox = useMsgBox();
  const [lignes, setLignes] = useState<ClassementAeLigne[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setLignes(await fetchClassementAe(idVersion, idUB));
      setDirty(false);
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger le classement AE.'));
    } finally {
      setLoading(false);
    }
  }, [idVersion, idUB]);

  useEffect(() => {
    if (open && idVersion > 0 && idUB > 0) {
      void load();
    }
  }, [open, idVersion, idUB, load]);

  const move = (index: number, delta: number) => {
    const target = index + delta;
    if (target < 0 || target >= lignes.length) return;
    const next = [...lignes];
    const tmp = next[index]!;
    next[index] = next[target]!;
    next[target] = tmp;
    setLignes(next);
    setDirty(true);
  };

  const save = async () => {
    setSaving(true);
    try {
      const updated = await reorderClassementAe({
        idVersion,
        idUB,
        idsClassementOrdonnes: lignes.map((l) => l.idClassementAE),
      });
      setLignes(updated);
      setDirty(false);
      void msgBox.success('Ordre AE enregistré.');
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Échec de l’enregistrement de l’ordre.'));
    } finally {
      setSaving(false);
    }
  };

  let itemCounter = 0;

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>
        Ordre AE — {codeUB ?? `UB #${idUB}`}
      </DialogTitle>
      <DialogContent dividers>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
          Réordonnez les groupes et actions. Le numéro 01…N du rapport sera calculé automatiquement
          (items uniquement). Les montants ne sont pas modifiés.
        </Typography>
        {error && (
          <Alert severity="error" sx={{ mb: 1.5 }}>
            {error}
          </Alert>
        )}
        {loading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
            <CircularProgress size={28} />
          </Box>
        ) : lignes.length === 0 ? (
          <Typography color="text.secondary">
            Aucun classement pour cette Version × UB. Enregistrez d’abord des prévisions AE.
          </Typography>
        ) : (
          <List dense disablePadding>
            {lignes.map((l, index) => {
              const isGroupe = l.typeLigne.toUpperCase() === 'GROUPE';
              const label = isGroupe
                ? (l.libelleGroupe ?? `Groupe #${l.idGroupeItemAE}`)
                : (l.libelleItemAE ?? '—');
              const prefix = isGroupe ? null : String(++itemCounter).padStart(2, '0');
              const secondary = isGroupe
                ? 'Groupe (en-tête)'
                : l.libelleGroupeDeduit
                  ? `Groupe : ${l.libelleGroupeDeduit}`
                  : 'Sans groupe';

              return (
                <ListItem
                  key={l.idClassementAE}
                  divider
                  secondaryAction={
                    <Stack direction="row" spacing={0.5}>
                      <IconButton
                        size="small"
                        aria-label="Monter"
                        disabled={index === 0 || saving}
                        onClick={() => move(index, -1)}
                      >
                        <ArrowUpwardIcon fontSize="small" />
                      </IconButton>
                      <IconButton
                        size="small"
                        aria-label="Descendre"
                        disabled={index === lignes.length - 1 || saving}
                        onClick={() => move(index, 1)}
                      >
                        <ArrowDownwardIcon fontSize="small" />
                      </IconButton>
                    </Stack>
                  }
                  sx={{ pr: 10 }}
                >
                  <ListItemText
                    primary={
                      <Typography
                        component="span"
                        sx={{ fontWeight: isGroupe ? 700 : 500, pl: isGroupe ? 0 : 2 }}
                      >
                        {prefix ? `${prefix}  ` : ''}
                        {label}
                      </Typography>
                    }
                    secondary={secondary}
                  />
                </ListItem>
              );
            })}
          </List>
        )}
      </DialogContent>
      <DialogActions>
        <SecondaryButton onClick={onClose} disabled={saving}>
          Fermer
        </SecondaryButton>
        <PrimaryButton onClick={() => void save()} disabled={!dirty || saving || lignes.length === 0}>
          {saving ? 'Enregistrement…' : 'Enregistrer l’ordre'}
        </PrimaryButton>
      </DialogActions>
    </Dialog>
  );
}
