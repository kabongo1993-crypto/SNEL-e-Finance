import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useEffect, useMemo, useState } from 'react';
import type { DemandePaiementBatchOpDef } from './demandePaiementBatchConfig';
import type { DemandePaiementBatchDocumentsPayload } from './demandePaiementBatchApi';
import {
  DOCUMENTS_DIALOG_MODE_EDITABLE,
  buildDocumentsDraftFromDpm,
  instrumentLabel,
  instrumentsForMode,
  toDocumentsPayload,
  type DocumentsDraft,
  type DocumentsRowSource,
  type InstrumentDocuments,
} from './demandePaiementBatchDocuments';

export type DemandePaiementBatchDocumentsRowMeta = DocumentsRowSource;

export interface DemandePaiementBatchDocumentsDialogProps {
  open: boolean;
  busy?: boolean;
  operation: DemandePaiementBatchOpDef | null;
  rows: DemandePaiementBatchDocumentsRowMeta[];
  onConfirm: (documents: Map<number, DemandePaiementBatchDocumentsPayload>) => void;
  onClose: () => void;
}

function buildInitialDrafts(rows: DocumentsRowSource[]): Record<number, DocumentsDraft> {
  const next: Record<number, DocumentsDraft> = {};
  for (const row of rows) {
    const draft = buildDocumentsDraftFromDpm(row);
    if (draft) next[row.idDemandePaiement] = draft;
  }
  return next;
}

export function DemandePaiementBatchDocumentsDialog({
  open,
  busy,
  operation,
  rows,
  onConfirm,
  onClose,
}: DemandePaiementBatchDocumentsDialogProps) {
  const idsKey = useMemo(() => rows.map((r) => r.idDemandePaiement).join(','), [rows]);
  const [drafts, setDrafts] = useState<Record<number, DocumentsDraft>>(() => buildInitialDrafts(rows));
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setDrafts(buildInitialDrafts(rows));
    setFormError(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when dialog opens / selection changes
  }, [open, idsKey]);

  const handleInstrumentChange = (id: number, typeInstrument: InstrumentDocuments) => {
    setDrafts((prev) => {
      const current = prev[id];
      if (!current || current.modePaiementSollicite === 'BANQUE') return prev;
      return { ...prev, [id]: { ...current, typeInstrument } };
    });
  };

  const handleSubmit = () => {
    const map = new Map<number, DemandePaiementBatchDocumentsPayload>();
    for (const row of rows) {
      const draft = drafts[row.idDemandePaiement] ?? buildDocumentsDraftFromDpm(row);
      if (!draft) {
        setFormError(`Mode de paiement introuvable pour ${row.reference}.`);
        return;
      }
      map.set(row.idDemandePaiement, toDocumentsPayload(draft));
    }
    setFormError(null);
    onConfirm(map);
  };

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="md">
      <DialogTitle>{operation?.label ?? 'Établir les documents'}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Typography variant="body2" color="text.secondary">
            Le mode de paiement est celui déjà enregistré sur chaque DPM (lecture seule).
            Le billet de conversion est proposé selon le mode et la devise ; le serveur reste l&apos;autorité.
          </Typography>
          {formError && <Alert severity="error">{formError}</Alert>}
          {rows.map((row, index) => {
            const draft = drafts[row.idDemandePaiement] ?? buildDocumentsDraftFromDpm(row);
            if (!draft) {
              return (
                <Box key={row.idDemandePaiement}>
                  {index > 0 && <Divider sx={{ mb: 2 }} />}
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                    {row.reference}
                  </Typography>
                  <Alert severity="warning">
                    Mode de paiement non renseigné sur cette DPM. Traitez d&apos;abord la charge.
                  </Alert>
                </Box>
              );
            }
            const banque = draft.modePaiementSollicite === 'BANQUE';
            return (
              <Box key={row.idDemandePaiement}>
                {index > 0 && <Divider sx={{ mb: 2 }} />}
                <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                  {row.reference}
                </Typography>
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
                  <TextField
                    size="small"
                    label="Mode"
                    value={draft.modePaiementSollicite}
                    sx={{ minWidth: 140 }}
                    slotProps={{ input: { readOnly: !DOCUMENTS_DIALOG_MODE_EDITABLE } }}
                    disabled
                  />
                  {banque ? (
                    <TextField
                      size="small"
                      label="Instrument"
                      value={instrumentLabel(draft.typeInstrument)}
                      sx={{ minWidth: 200 }}
                      disabled
                    />
                  ) : (
                    <TextField
                      select
                      size="small"
                      label="Instrument"
                      value={draft.typeInstrument}
                      onChange={(e) =>
                        handleInstrumentChange(
                          row.idDemandePaiement,
                          e.target.value as InstrumentDocuments,
                        )
                      }
                      sx={{ minWidth: 200 }}
                      disabled={busy}
                    >
                      {instrumentsForMode('CAISSE').map((opt) => (
                        <MenuItem key={opt.value} value={opt.value}>
                          {opt.label}
                        </MenuItem>
                      ))}
                    </TextField>
                  )}
                  <TextField
                    size="small"
                    label="Billet"
                    value={draft.etablirBillet ? 'À établir' : 'Non requis'}
                    sx={{ minWidth: 160 }}
                    disabled
                    helperText={
                      draft.etablirBillet
                        ? 'Requis (CAISSE + devise ≠ CDF)'
                        : 'Non requis'
                    }
                  />
                </Stack>
                <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
                  Devise sollicitée : {row.deviseSollicitee || '—'} — Document :{' '}
                  {instrumentLabel(draft.typeInstrument)} — Billet :{' '}
                  {draft.etablirBillet ? 'inclus dans le payload' : 'non inclus'}
                </Typography>
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
          Établir
        </Button>
      </DialogActions>
    </Dialog>
  );
}
