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
import type { TraitementChargeDpmPayload } from './demandePaiementBatchApi';
import { needsBilletConversion } from '../chargeDpmTauxUtils';

export interface DemandePaiementBatchTraitementRowMeta {
  idDemandePaiement: number;
  reference: string;
  deviseSollicitee: string;
  modePaiementSollicite?: string | null;
  typeBudgetSollicite?: string | null;
  itemSollicite?: string | null;
  typeInstrumentPaiement?: string | null;
  devisePaiement?: string | null;
}

export interface DemandePaiementBatchTraitementDialogProps {
  open: boolean;
  busy?: boolean;
  operation: DemandePaiementBatchOpDef | null;
  rows: DemandePaiementBatchTraitementRowMeta[];
  onConfirm: (traitements: Map<number, TraitementChargeDpmPayload>) => void;
  onClose: () => void;
}

interface DraftTraitement {
  modePaiementSollicite: string;
  typeInstrument: string;
  devisePaiement: string;
  typeBudgetSollicite: string;
  itemSollicite: string;
}

function defaultInstrumentForMode(mode: string): string {
  return mode === 'BANQUE' ? 'MINUTE_CHEQUE' : 'PIECE_CAISSE';
}

function defaultDevisePaiement(mode: string, deviseSollicitee: string): string {
  return mode === 'CAISSE' ? 'CDF' : (deviseSollicitee.trim().toUpperCase() || 'USD');
}

function emptyDraft(row: DemandePaiementBatchTraitementRowMeta): DraftTraitement {
  const mode = (row.modePaiementSollicite ?? 'CAISSE').trim().toUpperCase() || 'CAISSE';
  const instrument =
    (row.typeInstrumentPaiement ?? '').trim().toUpperCase() || defaultInstrumentForMode(mode);
  return {
    modePaiementSollicite: mode === 'BANQUE' ? 'BANQUE' : 'CAISSE',
    typeInstrument: instrument,
    devisePaiement:
      (row.devisePaiement ?? '').trim().toUpperCase() ||
      defaultDevisePaiement(mode, row.deviseSollicitee),
    typeBudgetSollicite: (row.typeBudgetSollicite ?? 'DC').trim().toUpperCase() || 'DC',
    itemSollicite: row.itemSollicite?.trim() ?? '',
  };
}

function buildInitialDrafts(rows: DemandePaiementBatchTraitementRowMeta[]): Record<number, DraftTraitement> {
  const next: Record<number, DraftTraitement> = {};
  for (const row of rows) next[row.idDemandePaiement] = emptyDraft(row);
  return next;
}

function instrumentsForMode(mode: string): { value: string; label: string }[] {
  if (mode === 'BANQUE') {
    return [{ value: 'MINUTE_CHEQUE', label: 'Minute de cheque' }];
  }
  return [
    { value: 'PIECE_CAISSE', label: 'Piece de caisse' },
    { value: 'BON_PROVISOIRE', label: 'Bon provisoire' },
  ];
}

export function DemandePaiementBatchTraitementDialog({
  open,
  busy,
  operation,
  rows,
  onConfirm,
  onClose,
}: DemandePaiementBatchTraitementDialogProps) {
  const idsKey = useMemo(() => rows.map((r) => r.idDemandePaiement).join(','), [rows]);
  const [drafts, setDrafts] = useState<Record<number, DraftTraitement>>(() => buildInitialDrafts(rows));
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setDrafts(buildInitialDrafts(rows));
    setFormError(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when dialog opens / selection changes
  }, [open, idsKey]);

  const updateDraft = (id: number, patch: Partial<DraftTraitement>) => {
    setDrafts((prev) => {
      const row = rows.find((r) => r.idDemandePaiement === id);
      const current = prev[id] ?? (row ? emptyDraft(row) : {
        modePaiementSollicite: 'CAISSE',
        typeInstrument: 'PIECE_CAISSE',
        devisePaiement: 'CDF',
        typeBudgetSollicite: 'DC',
        itemSollicite: '',
      });
      const next = { ...current, ...patch };
      if (patch.modePaiementSollicite) {
        const mode = patch.modePaiementSollicite;
        const allowed = instrumentsForMode(mode).map((o) => o.value);
        if (!allowed.includes(next.typeInstrument)) {
          next.typeInstrument = defaultInstrumentForMode(mode);
        }
        next.devisePaiement = defaultDevisePaiement(mode, row?.deviseSollicitee ?? 'USD');
      }
      return { ...prev, [id]: next };
    });
  };

  const applyTemplateToAll = () => {
    const first = rows[0];
    if (!first) return;
    const template = drafts[first.idDemandePaiement] ?? emptyDraft(first);
    const next: Record<number, DraftTraitement> = {};
    for (const row of rows) {
      next[row.idDemandePaiement] = { ...template };
    }
    setDrafts(next);
  };

  const handleSubmit = () => {
    const map = new Map<number, TraitementChargeDpmPayload>();
    for (const row of rows) {
      const draft = drafts[row.idDemandePaiement] ?? emptyDraft(row);
      if (!draft.typeInstrument.trim()) {
        setFormError(`Instrument obligatoire pour ${row.reference}.`);
        return;
      }
      if (!draft.devisePaiement.trim()) {
        setFormError(`Devise de paiement obligatoire pour ${row.reference}.`);
        return;
      }
      if (!draft.typeBudgetSollicite.trim()) {
        setFormError(`Type de budget obligatoire pour ${row.reference}.`);
        return;
      }
      if (
        (draft.typeBudgetSollicite === 'AE' || draft.typeBudgetSollicite === 'BI') &&
        !draft.itemSollicite.trim()
      ) {
        setFormError(`Item obligatoire pour ${row.reference} (${draft.typeBudgetSollicite}).`);
        return;
      }
      map.set(row.idDemandePaiement, {
        typeInstrument: draft.typeInstrument.trim().toUpperCase(),
        devisePaiement: draft.devisePaiement.trim().toUpperCase(),
        tauxPaiement: null,
        idTauxChangePaiement: null,
        modePaiementSollicite: draft.modePaiementSollicite,
        typeBudgetSollicite: draft.typeBudgetSollicite,
        itemSollicite: draft.typeBudgetSollicite === 'DC' ? null : draft.itemSollicite.trim(),
      });
    }
    setFormError(null);
    onConfirm(map);
  };

  const label = operation?.label ?? 'Traiter la charge';

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="md">
      <DialogTitle>{label}</DialogTitle>
      <DialogContent dividers>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
          Definissez le traitement de chaque demande. Le taux de change est resolu cote serveur — aucune
          saisie manuelle n&apos;est possible.
        </Typography>
        <Button size="small" variant="outlined" disabled={busy || rows.length < 2} onClick={applyTemplateToAll}>
          Copier le traitement de la premiere DPM
        </Button>
        {formError && (
          <Alert severity="error" sx={{ mt: 1.5 }}>
            {formError}
          </Alert>
        )}
        <Stack spacing={2} sx={{ mt: 2 }}>
          {rows.map((row) => {
            const draft = drafts[row.idDemandePaiement] ?? emptyDraft(row);
            const billetHint = needsBilletConversion(draft.modePaiementSollicite, row.deviseSollicitee);
            return (
              <Box
                key={row.idDemandePaiement}
                sx={{ p: 1.5, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}
              >
                <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                  {row.reference}
                </Typography>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
                  Devise sollicitee : {row.deviseSollicitee}
                </Typography>
                {billetHint && (
                  <Alert severity="warning" sx={{ mb: 1 }}>
                    Billet de conversion probablement requis (CAISSE + devise differente de CDF). Il doit
                    deja etre etabli avant le traitement.
                  </Alert>
                )}
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} useFlexGap sx={{ flexWrap: 'wrap' }}>
                  <TextField
                    select
                    size="small"
                    label="Mode"
                    value={draft.modePaiementSollicite}
                    disabled={busy}
                    onChange={(e) =>
                      updateDraft(row.idDemandePaiement, { modePaiementSollicite: e.target.value })
                    }
                    sx={{ minWidth: 140 }}
                  >
                    <MenuItem value="CAISSE">Caisse</MenuItem>
                    <MenuItem value="BANQUE">Banque</MenuItem>
                  </TextField>
                  <TextField
                    select
                    size="small"
                    label="Instrument"
                    value={draft.typeInstrument}
                    disabled={busy}
                    onChange={(e) => updateDraft(row.idDemandePaiement, { typeInstrument: e.target.value })}
                    sx={{ minWidth: 180 }}
                  >
                    {instrumentsForMode(draft.modePaiementSollicite).map((opt) => (
                      <MenuItem key={opt.value} value={opt.value}>
                        {opt.label}
                      </MenuItem>
                    ))}
                  </TextField>
                  <TextField
                    size="small"
                    label="Devise paiement"
                    value={draft.devisePaiement}
                    disabled={busy}
                    onChange={(e) => updateDraft(row.idDemandePaiement, { devisePaiement: e.target.value })}
                    sx={{ minWidth: 120 }}
                  />
                  <TextField
                    select
                    size="small"
                    label="Type budget"
                    value={draft.typeBudgetSollicite}
                    disabled={busy}
                    onChange={(e) =>
                      updateDraft(row.idDemandePaiement, { typeBudgetSollicite: e.target.value })
                    }
                    sx={{ minWidth: 120 }}
                  >
                    <MenuItem value="DC">DC</MenuItem>
                    <MenuItem value="AE">AE</MenuItem>
                    <MenuItem value="BI">BI</MenuItem>
                  </TextField>
                  {(draft.typeBudgetSollicite === 'AE' || draft.typeBudgetSollicite === 'BI') && (
                    <TextField
                      size="small"
                      label="Item"
                      value={draft.itemSollicite}
                      disabled={busy}
                      onChange={(e) => updateDraft(row.idDemandePaiement, { itemSollicite: e.target.value })}
                      sx={{ minWidth: 140 }}
                    />
                  )}
                </Stack>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
                  Taux : resolu automatiquement par le serveur (aucune saisie).
                </Typography>
                <Divider sx={{ mt: 1.5 }} />
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
          Traiter
        </Button>
      </DialogActions>
    </Dialog>
  );
}
