import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useEffect, useMemo, useState } from 'react';
import type { DemandePaiementBatchOpDef } from './demandePaiementBatchConfig';
import type { DeclarationValidationPhysiquePayload } from './demandePaiementBatchApi';

export interface DemandePaiementBatchPhysiqueRowMeta {
  idDemandePaiement: number;
  reference: string;
}

export interface DemandePaiementBatchPhysiqueDialogProps {
  open: boolean;
  busy?: boolean;
  niveau: 1 | 2;
  operation: DemandePaiementBatchOpDef | null;
  rows: DemandePaiementBatchPhysiqueRowMeta[];
  onConfirm: (declarations: Map<number, DeclarationValidationPhysiquePayload>) => void;
  onClose: () => void;
}

interface DraftDeclaration {
  nomSignataire: string;
  fonctionSignataire: string;
  dateSignature: string;
  commentaire: string;
}

function emptyDraft(): DraftDeclaration {
  return {
    nomSignataire: '',
    fonctionSignataire: '',
    dateSignature: new Date().toISOString().slice(0, 10),
    commentaire: '',
  };
}

function buildInitialDrafts(ids: number[]): Record<number, DraftDeclaration> {
  const next: Record<number, DraftDeclaration> = {};
  for (const id of ids) next[id] = emptyDraft();
  return next;
}

export function DemandePaiementBatchPhysiqueDialog({
  open,
  busy,
  niveau,
  operation,
  rows,
  onConfirm,
  onClose,
}: DemandePaiementBatchPhysiqueDialogProps) {
  const idsKey = useMemo(() => rows.map((r) => r.idDemandePaiement).join(','), [rows]);
  const [drafts, setDrafts] = useState<Record<number, DraftDeclaration>>(() =>
    buildInitialDrafts(rows.map((r) => r.idDemandePaiement)),
  );
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setDrafts(buildInitialDrafts(rows.map((r) => r.idDemandePaiement)));
    setFormError(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when dialog opens / selection changes
  }, [open, idsKey]);

  const updateDraft = (id: number, patch: Partial<DraftDeclaration>) => {
    setDrafts((prev) => ({
      ...prev,
      [id]: { ...(prev[id] ?? emptyDraft()), ...patch },
    }));
  };

  const applyTemplateToAll = () => {
    const first = rows[0];
    if (!first) return;
    const template = drafts[first.idDemandePaiement] ?? emptyDraft();
    const next: Record<number, DraftDeclaration> = {};
    for (const row of rows) {
      next[row.idDemandePaiement] = { ...template };
    }
    setDrafts(next);
  };

  const handleSubmit = () => {
    const map = new Map<number, DeclarationValidationPhysiquePayload>();
    for (const row of rows) {
      const draft = drafts[row.idDemandePaiement] ?? emptyDraft();
      if (!draft.nomSignataire.trim()) {
        setFormError(`Nom du signataire obligatoire pour ${row.reference}.`);
        return;
      }
      if (!draft.dateSignature) {
        setFormError(`Date de signature obligatoire pour ${row.reference}.`);
        return;
      }
      map.set(row.idDemandePaiement, {
        nomSignataire: draft.nomSignataire.trim(),
        fonctionSignataire: draft.fonctionSignataire.trim() || null,
        dateSignature: draft.dateSignature,
        commentaire: draft.commentaire.trim() || null,
      });
    }
    setFormError(null);
    onConfirm(map);
  };

  const label = operation?.label ?? `Declarer validation physique N${niveau}`;

  return (
    <Dialog open={open} onClose={() => !busy && onClose()} maxWidth="md" fullWidth>
      <DialogTitle>{label}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Alert severity="info" sx={{ py: 0.5 }}>
            Chaque demande conserve sa propre declaration (signataire, fonction, date, commentaire).
            Vous serez enregistre comme declarant, pas comme signataire.
          </Alert>
          {niveau === 2 && (
            <Alert severity="warning" sx={{ py: 0.5 }}>
              Pour N2, le document DPM signe doit deja etre joint sur chaque demande (controle
              metier unitaire).
            </Alert>
          )}
          {rows.length > 1 && (
            <Box>
              <Button size="small" variant="outlined" disabled={busy} onClick={applyTemplateToAll}>
                Copier la 1re declaration sur toutes
              </Button>
            </Box>
          )}
          {formError && (
            <Alert severity="error" sx={{ py: 0.5 }}>
              {formError}
            </Alert>
          )}
          {rows.map((row, index) => {
            const draft = drafts[row.idDemandePaiement] ?? emptyDraft();
            return (
              <Box key={row.idDemandePaiement}>
                {index > 0 && <Divider sx={{ mb: 2 }} />}
                <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                  {row.reference}
                </Typography>
                <Stack spacing={1.5}>
                  <TextField
                    label="Nom du signataire"
                    required
                    fullWidth
                    size="small"
                    disabled={busy}
                    value={draft.nomSignataire}
                    onChange={(e) =>
                      updateDraft(row.idDemandePaiement, { nomSignataire: e.target.value })
                    }
                  />
                  <TextField
                    label="Fonction du signataire"
                    fullWidth
                    size="small"
                    disabled={busy}
                    value={draft.fonctionSignataire}
                    onChange={(e) =>
                      updateDraft(row.idDemandePaiement, { fonctionSignataire: e.target.value })
                    }
                  />
                  <TextField
                    label="Date de signature"
                    type="date"
                    required
                    fullWidth
                    size="small"
                    disabled={busy}
                    value={draft.dateSignature}
                    onChange={(e) =>
                      updateDraft(row.idDemandePaiement, { dateSignature: e.target.value })
                    }
                    slotProps={{ inputLabel: { shrink: true } }}
                  />
                  <TextField
                    label="Commentaire"
                    fullWidth
                    size="small"
                    multiline
                    minRows={2}
                    disabled={busy}
                    value={draft.commentaire}
                    onChange={(e) =>
                      updateDraft(row.idDemandePaiement, { commentaire: e.target.value })
                    }
                  />
                </Stack>
              </Box>
            );
          })}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button disabled={busy} onClick={onClose}>
          Annuler
        </Button>
        <Button variant="contained" disabled={busy || rows.length === 0} onClick={handleSubmit}>
          {label}
        </Button>
      </DialogActions>
    </Dialog>
  );
}