import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  Paper,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import { SearchableSelect } from '../../components';
import type { DemandePaiementDetail } from '../../services/apiClient';
import { efRadius } from '../../theme';
import { formatDateFr, formatTaux } from '../taux-change/tauxChangeUtils';
import type { ChargeDpmTauxState } from './chargeDpmTauxUtils';
import { DetailFieldGrid } from './DetailFieldGrid';

function messageVerrouillageMode(motif: string | null | undefined): string {
  switch (motif?.toUpperCase()) {
    case 'BILLET_CONVERSION':
      return 'Le mode de paiement est verrouillé : le billet de conversion a déjà été établi.';
    case 'PIECE_CAISSE':
      return 'Le mode de paiement est verrouillé : la pièce de caisse a déjà été établie.';
    case 'BON_PROVISOIRE':
      return 'Le mode de paiement est verrouillé : le bon provisoire a déjà été établi.';
    case 'MINUTE_CHEQUE':
      return 'Le mode de paiement est verrouillé : la minute de chèque a déjà été établie.';
    default:
      return 'Le mode de paiement est verrouillé : un document de paiement a déjà été établi.';
  }
}

function TauxApplicableDisplay({
  state,
  deviseSource,
  deviseCible,
  dateReference,
  snapshotTaux,
}: {
  state: ChargeDpmTauxState;
  deviseSource: string;
  deviseCible: string;
  dateReference: string | null;
  snapshotTaux?: number | null;
}) {
  if (snapshotTaux != null && state.status === 'idle') {
    return (
      <Stack spacing={0.5}>
        <Typography variant="body2" color="text.secondary">
          Taux de change applicable (figé)
        </Typography>
        <Typography variant="body1" sx={{ fontWeight: 600 }}>
          {formatTaux(snapshotTaux)} — {deviseSource} → {deviseCible}
        </Typography>
      </Stack>
    );
  }

  if (state.status === 'loading' || state.status === 'idle') {
    return (
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        <CircularProgress size={18} />
        <Typography variant="body2" color="text.secondary">
          Chargement du taux applicable…
        </Typography>
      </Stack>
    );
  }

  if (state.status === 'error') {
    return <Alert severity="error">{state.message}</Alert>;
  }

  if (state.status === 'not_found') {
    return (
      <Alert severity="warning">
        Aucun taux de change applicable n&apos;est configuré pour la paire{' '}
        <strong>
          {state.deviseSource} → {state.deviseCible}
        </strong>{' '}
        à la date de traitement{' '}
        <strong>{formatDateFr(state.dateReference)}</strong>. Le traitement ne peut pas être validé
        tant qu&apos;un taux n&apos;est pas enregistré dans le référentiel.
      </Alert>
    );
  }

  if (state.status === 'identity') {
    return (
      <Stack spacing={0.5}>
        <Typography variant="body2" color="text.secondary">
          Taux de change applicable
        </Typography>
        <Typography variant="body1" sx={{ fontWeight: 600 }}>
          1 — {deviseSource} → {deviseCible} (même devise)
        </Typography>
        {dateReference && (
          <Typography variant="caption" color="text.secondary">
            Date de référence : {formatDateFr(dateReference)} (date de traitement)
          </Typography>
        )}
      </Stack>
    );
  }

  const { applicable } = state;
  return (
    <Stack spacing={0.5}>
      <Typography variant="body2" color="text.secondary">
        Taux de change applicable (référentiel)
      </Typography>
      <Typography variant="body1" sx={{ fontWeight: 600 }}>
        {formatTaux(applicable.taux)} — {applicable.deviseSource} → {applicable.deviseCible}
      </Typography>
      <Typography variant="caption" color="text.secondary">
        Date d&apos;effet du taux : {formatDateFr(applicable.dateEffet)} — référence métier :{' '}
        {dateReference ? formatDateFr(dateReference) : '—'} (date de traitement)
      </Typography>
    </Stack>
  );
}

export type ChargeDpmTraitementPanelProps = {
  demande: DemandePaiementDetail;
  statut: string;
  enTraitement: boolean;
  modeVerrouille: boolean;
  peutModifierSollicitation: boolean;
  isMutating: boolean;
  modePaiement: 'CAISSE' | 'BANQUE';
  typeBudget: 'DC' | 'AE' | 'BI';
  itemSollicite: string;
  instrument: string;
  devisePaiement: string;
  montantAffiche: string;
  tauxState: ChargeDpmTauxState;
  dateReference: string | null;
  instrumentOptions: { value: string; label: string }[];
  canTransmit: boolean;
  billetRequired: boolean;
  billetEtabli: boolean;
  instrumentDocumentEtabli: boolean;
  onModePaiementChange: (mode: 'CAISSE' | 'BANQUE') => void;
  onTypeBudgetChange: (budget: 'DC' | 'AE' | 'BI') => void;
  onItemSolliciteChange: (item: string) => void;
  onItemSolliciteBlur: () => void;
  onInstrumentChange: (instrument: string) => void;
  onDevisePaiementChange: (devise: string) => void;
  onTransmit: () => void;
};

/** Panneau latéral de traitement DPM — présentation uniquement, handlers fournis par le parent. */
export function ChargeDpmTraitementPanel({
  demande,
  statut,
  enTraitement,
  modeVerrouille,
  peutModifierSollicitation,
  isMutating,
  modePaiement,
  typeBudget,
  itemSollicite,
  instrument,
  devisePaiement,
  montantAffiche,
  tauxState,
  dateReference,
  instrumentOptions,
  canTransmit,
  billetRequired,
  billetEtabli,
  instrumentDocumentEtabli,
  onModePaiementChange,
  onTypeBudgetChange,
  onItemSolliciteChange,
  onItemSolliciteBlur,
  onInstrumentChange,
  onDevisePaiementChange,
  onTransmit,
}: ChargeDpmTraitementPanelProps) {
  return (
    <Paper
      variant="outlined"
      sx={{
        p: { xs: 2, md: 2.5 },
        borderRadius: `${efRadius.md}px`,
        bgcolor: 'background.paper',
      }}
    >
      <Stack spacing={2}>
        <Box>
          <Typography variant="overline" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
            Traitement DPM
          </Typography>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
            Paramètres de paiement
          </Typography>
        </Box>

        {!enTraitement && (
          <Alert severity="info">
            {statut === 'SOUMISE'
              ? 'Cliquez d’abord sur Réceptionner pour ouvrir le traitement (instrument, conversion).'
              : statut === 'BROUILLON'
                ? 'Cliquez sur Entrer en traitement pour ouvrir le formulaire Chargé DP (sans passer par Soumise).'
                : 'Le traitement Chargé DP est disponible uniquement au statut En traitement DPM.'}
          </Alert>
        )}
        {enTraitement && !modeVerrouille && (
          <Alert severity="info">
            Le mode de paiement et le type de budget indiqués par le demandeur sont indicatifs. Vous
            pouvez les modifier ci-dessous avant établissement d&apos;un document (billet, pièce de caisse,
            bon provisoire, minute de chèque). Le document à établir correspond toujours au mode actuel.
            Le taux de change est déterminé automatiquement depuis le référentiel selon la date
            de traitement (jour courant).
          </Alert>
        )}
        {enTraitement && modeVerrouille && (
          <Alert severity="warning">
            {messageVerrouillageMode(demande.motifVerrouillageModePaiement)}
          </Alert>
        )}

        <Stack spacing={2}>
          <Stack spacing={1}>
            <Typography variant="body2" color="text.secondary" sx={{ fontWeight: 600 }}>
              Mode de paiement retenu *
            </Typography>
            <ToggleButtonGroup
              exclusive
              value={modePaiement}
              onChange={(_, v: 'CAISSE' | 'BANQUE' | null) => {
                if (!v || !peutModifierSollicitation) return;
                onModePaiementChange(v);
              }}
              disabled={!peutModifierSollicitation}
              size="small"
              fullWidth
            >
              <ToggleButton value="CAISSE">CAISSE</ToggleButton>
              <ToggleButton value="BANQUE">BANQUE</ToggleButton>
            </ToggleButtonGroup>
          </Stack>

          <Stack spacing={1}>
            <Typography variant="body2" color="text.secondary" sx={{ fontWeight: 600 }}>
              Destination budgétaire retenue *
            </Typography>
            <ToggleButtonGroup
              exclusive
              value={typeBudget}
              onChange={(_, v: 'DC' | 'AE' | 'BI' | null) => {
                if (!v || !peutModifierSollicitation) return;
                onTypeBudgetChange(v);
              }}
              disabled={!peutModifierSollicitation}
              size="small"
              fullWidth
              sx={{ flexWrap: 'wrap' }}
            >
              <ToggleButton value="DC">DC</ToggleButton>
              <ToggleButton value="AE">AE</ToggleButton>
              <ToggleButton value="BI">BI / IVT</ToggleButton>
            </ToggleButtonGroup>
            {(typeBudget === 'AE' || typeBudget === 'BI') && (
              <TextField
                required
                label="Item N°"
                value={itemSollicite}
                onChange={(e) => onItemSolliciteChange(e.target.value)}
                onBlur={onItemSolliciteBlur}
                disabled={!peutModifierSollicitation}
                placeholder="Ex. 025"
                slotProps={{ inputLabel: { shrink: true } }}
                fullWidth
                size="small"
              />
            )}
          </Stack>

          <SearchableSelect
            label="Instrument"
            value={instrument}
            disabled={!enTraitement}
            onChange={onInstrumentChange}
            fullWidth
            options={instrumentOptions}
          />

          <Divider />

          <DetailFieldGrid
            fields={[
              { label: 'Devise sollicitée', value: demande.devise },
              { label: 'Montant sollicité', value: String(demande.montantBrut) },
            ]}
          />

          <TextField
            label="Devise de paiement"
            value={devisePaiement}
            onChange={(e) => onDevisePaiementChange(e.target.value.toUpperCase())}
            disabled={modePaiement === 'CAISSE' || !enTraitement}
            fullWidth
            size="small"
          />

          {(enTraitement || demande.tauxPaiement != null) && (
            <Paper variant="outlined" sx={{ p: 1.5, bgcolor: 'action.hover' }}>
              <TauxApplicableDisplay
                state={enTraitement ? tauxState : { status: 'idle' }}
                deviseSource={demande.devise}
                deviseCible={devisePaiement}
                dateReference={dateReference}
                snapshotTaux={!enTraitement ? demande.tauxPaiement : undefined}
              />
            </Paper>
          )}

          <Box>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.25 }}>
              Montant de paiement (prévisualisation API)
            </Typography>
            <Typography variant="h6" sx={{ fontWeight: 750, fontVariantNumeric: 'tabular-nums' }}>
              {montantAffiche}
            </Typography>
            {enTraitement && (
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                Calculé par le backend à partir du taux référentiel applicable.
              </Typography>
            )}
          </Box>

          <Button
            variant="contained"
            size="large"
            fullWidth
            disabled={isMutating || !enTraitement || !canTransmit}
            onClick={onTransmit}
            sx={{ mt: 0.5, py: 1.25, fontWeight: 700 }}
          >
            Transmettre au Gestionnaire Junior
          </Button>
          {billetRequired && enTraitement && !billetEtabli && (
            <Typography variant="caption" color="text.secondary">
              Le billet de conversion doit être établi avant la transmission.
            </Typography>
          )}
          {enTraitement && !instrumentDocumentEtabli && (
            <Typography variant="caption" color="text.secondary">
              Le document instrument ({instrument.replace(/_/g, ' ')}) doit être établi avant la
              transmission.
            </Typography>
          )}
        </Stack>
      </Stack>
    </Paper>
  );
}
