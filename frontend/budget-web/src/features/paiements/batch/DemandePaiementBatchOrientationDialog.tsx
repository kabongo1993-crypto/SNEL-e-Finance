import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControlLabel,
  MenuItem,
  Radio,
  RadioGroup,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useEffect, useMemo, useState } from 'react';
import type { DemandePaiementBatchOpDef } from './demandePaiementBatchConfig';
import type { OrienterDemandePaiementPayload } from './demandePaiementBatchApi';

export interface DemandePaiementBatchOrientationRowMeta {
  idDemandePaiement: number;
  reference: string;
}

export interface DemandePaiementBatchOrientationUserOption {
  idUtilisateur: number;
  label: string;
}

export interface DemandePaiementBatchOrientationDialogProps {
  open: boolean;
  busy?: boolean;
  operation: DemandePaiementBatchOpDef | null;
  rows: DemandePaiementBatchOrientationRowMeta[];
  utilisateurs?: DemandePaiementBatchOrientationUserOption[];
  onConfirm: (orientations: Map<number, OrienterDemandePaiementPayload>) => void;
  onClose: () => void;
}

type ModeCible = 'pool' | 'nominatif';

interface DraftOrientation {
  mode: ModeCible;
  idUtilisateurCible: string;
}

function emptyDraft(): DraftOrientation {
  return { mode: 'pool', idUtilisateurCible: '' };
}

function buildInitial(rows: DemandePaiementBatchOrientationRowMeta[]): Record<number, DraftOrientation> {
  const next: Record<number, DraftOrientation> = {};
  for (const row of rows) next[row.idDemandePaiement] = emptyDraft();
  return next;
}

export function DemandePaiementBatchOrientationDialog({
  open,
  busy,
  operation,
  rows,
  utilisateurs = [],
  onConfirm,
  onClose,
}: DemandePaiementBatchOrientationDialogProps) {
  const idsKey = useMemo(() => rows.map((r) => r.idDemandePaiement).join(','), [rows]);
  const [drafts, setDrafts] = useState<Record<number, DraftOrientation>>(() => buildInitial(rows));
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setDrafts(buildInitial(rows));
    setFormError(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset on open / selection
  }, [open, idsKey]);

  const updateDraft = (id: number, patch: Partial<DraftOrientation>) => {
    setDrafts((prev) => ({
      ...prev,
      [id]: { ...(prev[id] ?? emptyDraft()), ...patch },
    }));
  };

  const applyPoolToAll = () => {
    const next: Record<number, DraftOrientation> = {};
    for (const row of rows) next[row.idDemandePaiement] = emptyDraft();
    setDrafts(next);
  };

  const handleSubmit = () => {
    const map = new Map<number, OrienterDemandePaiementPayload>();
    for (const row of rows) {
      const draft = drafts[row.idDemandePaiement] ?? emptyDraft();
      if (draft.mode === 'pool') {
        map.set(row.idDemandePaiement, { idUtilisateurCible: null });
        continue;
      }
      const id = Number(draft.idUtilisateurCible);
      if (!Number.isFinite(id) || id <= 0) {
        setFormError(`Utilisateur cible obligatoire pour ${row.reference}.`);
        return;
      }
      map.set(row.idDemandePaiement, { idUtilisateurCible: id });
    }
    setFormError(null);
    onConfirm(map);
  };

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="md">
      <DialogTitle>{operation?.label ?? 'Orienter les DPM'}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Typography variant="body2" color="text.secondary">
            Choisissez pour chaque DPM le pool de controle budgetaire ou un utilisateur cible. Le
            serveur reste la source de verite (eligibilite, securite, routage).
          </Typography>
          <Box>
            <Button size="small" onClick={applyPoolToAll} disabled={busy}>
              Tout orienter vers le pool
            </Button>
          </Box>
          {formError && <Alert severity="error">{formError}</Alert>}
          {rows.map((row, index) => {
            const draft = drafts[row.idDemandePaiement] ?? emptyDraft();
            return (
              <Box key={row.idDemandePaiement}>
                {index > 0 && <Divider sx={{ mb: 2 }} />}
                <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                  {row.reference}
                </Typography>
                <RadioGroup
                  row
                  value={draft.mode}
                  onChange={(e) =>
                    updateDraft(row.idDemandePaiement, { mode: e.target.value as ModeCible })
                  }
                >
                  <FormControlLabel
                    value="pool"
                    control={<Radio size="small" disabled={busy} />}
                    label="Pool controle budgetaire"
                  />
                  <FormControlLabel
                    value="nominatif"
                    control={<Radio size="small" disabled={busy} />}
                    label="Utilisateur cible"
                  />
                </RadioGroup>
                {draft.mode === 'nominatif' && (
                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ mt: 1 }}>
                    {utilisateurs.length > 0 ? (
                      <TextField
                        select
                        size="small"
                        label="Utilisateur"
                        value={draft.idUtilisateurCible}
                        onChange={(e) =>
                          updateDraft(row.idDemandePaiement, { idUtilisateurCible: e.target.value })
                        }
                        sx={{ minWidth: 280 }}
                        disabled={busy}
                      >
                        {utilisateurs.map((u) => (
                          <MenuItem key={u.idUtilisateur} value={String(u.idUtilisateur)}>
                            {u.label}
                          </MenuItem>
                        ))}
                      </TextField>
                    ) : (
                      <TextField
                        size="small"
                        label="Id utilisateur cible"
                        type="number"
                        value={draft.idUtilisateurCible}
                        onChange={(e) =>
                          updateDraft(row.idDemandePaiement, { idUtilisateurCible: e.target.value })
                        }
                        sx={{ minWidth: 200 }}
                        disabled={busy}
                      />
                    )}
                  </Stack>
                )}
              </Box>
            );
          })}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>
          Annuler
        </Button>
        <Button variant="contained" onClick={handleSubmit} disabled={busy || rows.length === 0}>
          Orienter
        </Button>
      </DialogActions>
    </Dialog>
  );
}
