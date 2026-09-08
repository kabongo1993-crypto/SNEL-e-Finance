import {
  Alert,
  Box,
  Button,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useEffect, useMemo, useState } from 'react';
import { SearchableSelect, AmountField } from '../../components';
import {
  fetchActionsExploitation,
  fetchGroupesItemAE,
  fetchItemsBI,
  fetchLigneBudgetaireDisponible,
  fetchRubriquesBudgetaires,
  type GroupeItemAE,
  type ItemBI,
  type LigneBudgetaireDisponible,
  type RubriqueBudgetaire,
  type TypeBudget,
} from '../../services/apiClient';
import { MOIS_LABELS, apiErrorMessage, formatMontantUsd } from './paiementUtils';

export interface ImputationDraft {
  ordre: number;
  idTypeBudget: number;
  codeTypeBudget: string;
  idUB: number;
  idExercice: number;
  idRubriqueBudgetaire: number | null;
  mois: number | null;
  libelleItemAE: string | null;
  idGroupeItemAE: number | null;
  idItemBI: number | null;
  detailBI: string | null;
  idBudgetLigne: number | null;
  montantUsd: number;
  devise: string;
}

interface ImputationBudgetaireFormProps {
  typesBudget: TypeBudget[];
  idUB: number;
  idExercice: number;
  anneeExercice: number;
  initial?: Partial<ImputationDraft>;
  disabled?: boolean;
  onSubmit: (draft: ImputationDraft) => void;
  onCancel: () => void;
}

function codeType(tb: TypeBudget | undefined): string {
  return (tb?.codeType ?? '').trim().toUpperCase();
}

export function ImputationBudgetaireForm({
  typesBudget,
  idUB,
  idExercice,
  anneeExercice,
  initial,
  disabled,
  onSubmit,
  onCancel,
}: ImputationBudgetaireFormProps) {
  const [idTypeBudget, setIdTypeBudget] = useState(initial?.idTypeBudget ?? typesBudget[0]?.idTypeBudget ?? 0);
  const [idRB, setIdRB] = useState<number | ''>(initial?.idRubriqueBudgetaire ?? '');
  const [mois, setMois] = useState<number | ''>(initial?.mois ?? '');
  const [libelleItemAE, setLibelleItemAE] = useState(initial?.libelleItemAE ?? '');
  const [idGroupeAE, setIdGroupeAE] = useState<number | ''>(initial?.idGroupeItemAE ?? '');
  const [idItemBI, setIdItemBI] = useState<number | ''>(initial?.idItemBI ?? '');
  const [detailBI, setDetailBI] = useState(initial?.detailBI ?? '');
  const [montantUsd, setMontantUsd] = useState(String(initial?.montantUsd ?? ''));
  const [rubriques, setRubriques] = useState<RubriqueBudgetaire[]>([]);
  const [actionsAE, setActionsAE] = useState<string[]>([]);
  const [groupesAE, setGroupesAE] = useState<GroupeItemAE[]>([]);
  const [itemsBI, setItemsBI] = useState<ItemBI[]>([]);
  const [ligne, setLigne] = useState<LigneBudgetaireDisponible | null>(null);
  const [ligneMsg, setLigneMsg] = useState<string | null>(null);
  const [ligneLoading, setLigneLoading] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);

  const type = useMemo(
    () => typesBudget.find((t) => t.idTypeBudget === idTypeBudget),
    [typesBudget, idTypeBudget],
  );
  const code = codeType(type);

  useEffect(() => {
    void (async () => {
      try {
        const [rb, gr, bi] = await Promise.all([
          fetchRubriquesBudgetaires(),
          fetchGroupesItemAE(),
          fetchItemsBI(),
        ]);
        setRubriques(rb.filter((r) => r.actif));
        setGroupesAE(gr.filter((g) => g.actif));
        setItemsBI(bi.filter((i) => i.actif));
      } catch {
        /* non bloquant */
      }
    })();
  }, []);

  useEffect(() => {
    if (code !== 'AE' || !idExercice) return;
    void (async () => {
      try {
        const actions = await fetchActionsExploitation({ idExercice });
        setActionsAE(actions);
      } catch {
        setActionsAE([]);
      }
    })();
  }, [code, idExercice]);

  useEffect(() => {
    if (!idTypeBudget || !idUB || !idExercice) {
      setLigne(null);
      return;
    }
    const timer = window.setTimeout(() => {
      void (async () => {
        setLigneLoading(true);
        setLigneMsg(null);
        try {
          const params: Parameters<typeof fetchLigneBudgetaireDisponible>[0] = {
            idExercice,
            idUB,
            idTypeBudget,
          };
          if (code === 'DC') {
            if (!idRB || !mois) {
              setLigne(null);
              setLigneLoading(false);
              return;
            }
            params.idRubriqueBudgetaire = Number(idRB);
            params.mois = Number(mois);
          } else if (code === 'AE') {
            if (!idRB || !libelleItemAE.trim()) {
              setLigne(null);
              setLigneLoading(false);
              return;
            }
            params.idRubriqueBudgetaire = Number(idRB);
            params.libelleItemAE = libelleItemAE.trim();
            if (idGroupeAE) params.idGroupeItemAE = Number(idGroupeAE);
          } else if (code === 'BI') {
            if (!idItemBI || !detailBI.trim()) {
              setLigne(null);
              setLigneLoading(false);
              return;
            }
            params.idItemBI = Number(idItemBI);
            params.detailBI = detailBI.trim();
          }
          const result = await fetchLigneBudgetaireDisponible(params);
          setLigne(result);
          if (!result.previsionExiste) {
            setLigneMsg('Aucune prévision enregistrée pour cette combinaison.');
          } else {
            setLigneMsg(null);
          }
        } catch (err) {
          setLigne(null);
          setLigneMsg(apiErrorMessage(err, 'Impossible de consulter la ligne budgétaire.'));
        } finally {
          setLigneLoading(false);
        }
      })();
    }, 350);
    return () => window.clearTimeout(timer);
  }, [idTypeBudget, idUB, idExercice, code, idRB, mois, libelleItemAE, idGroupeAE, idItemBI, detailBI]);

  const handleSubmit = () => {
    setLocalError(null);
    const montant = Number(String(montantUsd).replace(',', '.'));
    if (!idTypeBudget) {
      setLocalError('Le type de budget est obligatoire.');
      return;
    }
    if (!Number.isFinite(montant) || montant <= 0) {
      setLocalError('Le montant USD doit être strictement positif.');
      return;
    }
    if (code === 'DC' && (!idRB || !mois)) {
      setLocalError('Pour une imputation DC : UB, rubrique et mois sont obligatoires.');
      return;
    }
    if (code === 'AE' && (!idRB || !libelleItemAE.trim())) {
      setLocalError('Pour une imputation AE : UB, rubrique et Item AE sont obligatoires.');
      return;
    }
    if (code === 'BI' && (!idItemBI || !detailBI.trim())) {
      setLocalError('Pour une imputation BI : UB, Item BI et détail sont obligatoires.');
      return;
    }

    onSubmit({
      ordre: initial?.ordre ?? 1,
      idTypeBudget,
      codeTypeBudget: code,
      idUB,
      idExercice,
      idRubriqueBudgetaire: idRB ? Number(idRB) : null,
      mois: mois ? Number(mois) : null,
      libelleItemAE: code === 'AE' ? libelleItemAE.trim() : null,
      idGroupeItemAE: idGroupeAE ? Number(idGroupeAE) : null,
      idItemBI: idItemBI ? Number(idItemBI) : null,
      detailBI: code === 'BI' ? detailBI.trim() : null,
      idBudgetLigne: ligne?.idBudgetLigne ?? null,
      montantUsd: montant,
      devise: 'USD',
    });
  };

  return (
    <Stack spacing={2}>
      <SearchableSelect
        required
        label="Type de budget"
        value={idTypeBudget ? String(idTypeBudget) : ''}
        onChange={(v) => {
          setIdTypeBudget(Number(v));
          setIdRB('');
          setMois('');
          setLibelleItemAE('');
          setIdGroupeAE('');
          setIdItemBI('');
          setDetailBI('');
          setLigne(null);
        }}
        disabled={disabled}
        fullWidth
        options={typesBudget.map((t) => ({
          value: String(t.idTypeBudget),
          label: `${t.codeType} — ${t.libelle}`,
        }))}
        placeholder="Rechercher un type…"
      />

      {(code === 'DC' || code === 'AE') && (
        <SearchableSelect
          required
          label="Rubrique budgétaire"
          value={idRB === '' ? '' : String(idRB)}
          onChange={(v) => setIdRB(v ? Number(v) : '')}
          disabled={disabled}
          fullWidth
          allowEmpty
          options={[
            { value: '', label: 'Sélectionner…' },
            ...rubriques.map((r) => ({
              value: String(r.idRB),
              label: `${r.codeRB} — ${r.libelle}`,
            })),
          ]}
          placeholder="Rechercher une rubrique…"
        />
      )}

      {code === 'DC' && (
        <SearchableSelect
          required
          label="Mois"
          value={mois === '' ? '' : String(mois)}
          onChange={(v) => setMois(v ? Number(v) : '')}
          disabled={disabled}
          fullWidth
          allowEmpty
          options={[
            { value: '', label: 'Sélectionner…' },
            ...MOIS_LABELS.map((m) => ({
              value: String(m.value),
              label: m.label,
            })),
          ]}
          placeholder="Rechercher un mois…"
        />
      )}

      {code === 'AE' && (
        <>
          <TextField
            required
            label="Item AE"
            value={libelleItemAE}
            onChange={(e) => setLibelleItemAE(e.target.value)}
            disabled={disabled}
            fullWidth
            helperText={
              actionsAE.length
                ? 'Saisissez l’Item AE réellement imputé (suggestions disponibles).'
                : 'Identité de l’Item AE réellement imputé.'
            }
            slotProps={{ htmlInput: { list: 'dpm-actions-ae' } }}
          />
          {actionsAE.length > 0 && (
            <datalist id="dpm-actions-ae">
              {actionsAE.map((a) => (
                <option key={a} value={a} />
              ))}
            </datalist>
          )}
          <SearchableSelect
            label="Groupe AE (facultatif)"
            value={idGroupeAE === '' ? '' : String(idGroupeAE)}
            onChange={(v) => setIdGroupeAE(v ? Number(v) : '')}
            disabled={disabled}
            fullWidth
            allowEmpty
            options={[
              { value: '', label: 'Aucun' },
              ...groupesAE.map((g) => ({
                value: String(g.idGroupeItemAE),
                label: g.libelle,
              })),
            ]}
            placeholder="Rechercher un groupe…"
          />
          <TextField label="Année" value={anneeExercice} disabled fullWidth />
        </>
      )}

      {code === 'BI' && (
        <>
          <SearchableSelect
            required
            label="Item BI"
            value={idItemBI === '' ? '' : String(idItemBI)}
            onChange={(v) => setIdItemBI(v ? Number(v) : '')}
            disabled={disabled}
            fullWidth
            allowEmpty
            options={[
              { value: '', label: 'Sélectionner…' },
              ...itemsBI.map((i) => ({
                value: String(i.idItemBI),
                label: `${i.codeItem} — ${i.libelle}`,
              })),
            ]}
            placeholder="Rechercher un item BI…"
          />
          <TextField
            required
            label="Détail BI"
            value={detailBI}
            onChange={(e) => setDetailBI(e.target.value)}
            disabled={disabled}
            fullWidth
          />
          <TextField label="Année" value={anneeExercice} disabled fullWidth />
        </>
      )}

      <AmountField
        required
        label="Montant USD"
        value={montantUsd}
        onChange={setMontantUsd}
        disabled={disabled}
        fullWidth
        currency="USD"
        helperText="Les montants d’imputation sont exprimés en USD."
      />

      <Box
        sx={{
          p: 1.5,
          borderRadius: 1,
          border: '1px solid',
          borderColor: 'divider',
          bgcolor: 'var(--ef-surface-secondary)',
        }}
      >
        <Typography variant="subtitle2" sx={{ mb: 0.5 }}>
          Situation budgétaire (aide à la saisie)
        </Typography>
        {ligneLoading && (
          <Typography variant="body2" color="text.secondary">
            Consultation de la ligne…
          </Typography>
        )}
        {!ligneLoading && ligne && (
          <Stack spacing={0.5}>
            <Typography variant="body2" sx={{ overflowWrap: 'anywhere' }}>
              Ligne : {ligne.idBudgetLigne ?? '—'} · Prévision :{' '}
              {formatMontantUsd(ligne.montantPrevision)}
            </Typography>
            <Typography variant="body2" sx={{ overflowWrap: 'anywhere' }}>
              Budget annuel : {formatMontantUsd(ligne.budgetAnnuel)} · Engagé :{' '}
              {formatMontantUsd(ligne.creditEngageAnnuel)} · Disponible :{' '}
              {formatMontantUsd(ligne.creditDisponibleAnnuel)}
            </Typography>
            {!ligne.previsionExiste && (
              <Alert severity="info" sx={{ mt: 1 }}>
                Prévision : 0 USD — aucune prévision enregistrée pour cette combinaison. Vous pouvez
                poursuivre l’imputation.
              </Alert>
            )}
          </Stack>
        )}
        {!ligneLoading && !ligne && ligneMsg && (
          <Alert severity="info">{ligneMsg} L’imputation reste autorisée.</Alert>
        )}
        {!ligneLoading && !ligne && !ligneMsg && (
          <Typography variant="body2" color="text.secondary">
            Renseignez la clé d’imputation pour afficher le budget applicable.
          </Typography>
        )}
      </Box>

      {localError && <Alert severity="error">{localError}</Alert>}

      <Stack
        direction={{ xs: 'column', md: 'row' }}
        spacing={1}
        sx={{ justifyContent: { md: 'flex-end' } }}
      >
        <Button onClick={onCancel} disabled={disabled}>
          Annuler
        </Button>
        <Button variant="contained" onClick={handleSubmit} disabled={disabled}>
          Enregistrer l’imputation
        </Button>
      </Stack>
    </Stack>
  );
}
