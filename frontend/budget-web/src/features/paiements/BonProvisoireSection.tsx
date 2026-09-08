import {
  Alert,
  Button,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useMemo } from 'react';
import { useDocumentViewer, buildBonProvisoirePdfViewerOptions } from '../../components/documentViewer';
import {
  etablirBonProvisoire,
  type BonProvisoire,
  type DemandePaiementDetail,
} from '../../services/apiClient';
import { formatDateFr } from '../taux-change/tauxChangeUtils';
import { DetailFieldGrid } from './DetailFieldGrid';
import { apiErrorMessage, formatMontantDevise } from './paiementUtils';
import type { EtablissementLocalPatch } from './etablissementLocalUpdate';
import type { RunDemandePaiementMutationOptions } from './useDemandePaiementMutationLock';

type Props = {
  demande: DemandePaiementDetail;
  bon: BonProvisoire | null;
  enTraitement: boolean;
  busy: boolean;
  onEtablissementLocal: (patch: EtablissementLocalPatch) => void;
  onError: (message: string) => void;
  onBusy?: (value: boolean) => void;
  embedded?: boolean;
  guardAction?: <T>(
    action: () => Promise<T>,
    options?: RunDemandePaiementMutationOptions,
  ) => Promise<T | undefined>;
};

function ReadOnlyField({ label, value }: { label: string; value: string }) {
  return (
    <TextField label={label} value={value} disabled fullWidth slotProps={{ inputLabel: { shrink: true } }} />
  );
}

export function BonProvisoireSection({
  demande,
  bon,
  enTraitement,
  busy,
  onEtablissementLocal,
  onError,
  onBusy,
  embedded = false,
  guardAction,
}: Props) {
  const { openDocument } = useDocumentViewer();
  const etabli = bon?.statut?.toUpperCase() === 'ETABLI';

  const beneficiaire = useMemo(() => {
    if (bon?.beneficiaireAffichage) return bon.beneficiaireAffichage;
    const principal = demande.beneficiaires?.find((b) => b.estPrincipal) ?? demande.beneficiaires?.[0];
    if (!principal) return '—';
    return principal.raisonSociale?.trim() || principal.nomComplet?.trim() || '—';
  }, [bon?.beneficiaireAffichage, demande.beneficiaires]);

  const openPdf = () => {
    openDocument(buildBonProvisoirePdfViewerOptions(demande.idDemandePaiement));
  };

  const handleEtablir = async () => {
    if (busy) return;

    const execute = async () => {
      const {
        beginEtablissementPerf,
        markEtablissementLocalUpdateStart,
        markEtablissementLocalUpdateEnd,
      } = await import('./etablissementPerf');
      beginEtablissementPerf('BON_PROVISOIRE', demande.idDemandePaiement);
      const bonProvisoire = await etablirBonProvisoire(demande.idDemandePaiement, {
        dateBon: demande.dateEmission?.slice(0, 10) ?? null,
      });
      markEtablissementLocalUpdateStart('BON_PROVISOIRE', demande.idDemandePaiement);
      onEtablissementLocal({ kind: 'BON_PROVISOIRE', bonProvisoire });
      markEtablissementLocalUpdateEnd('BON_PROVISOIRE', demande.idDemandePaiement);
    };

    if (guardAction) {
      try {
        await guardAction(execute, { message: 'Établissement du bon provisoire' });
      } catch (err) {
        onError(apiErrorMessage(err, 'Impossible d’établir le bon provisoire.'));
      }
      return;
    }

    onBusy?.(true);
    try {
      await execute();
    } catch (err) {
      onError(apiErrorMessage(err, 'Impossible d’établir le bon provisoire.'));
    } finally {
      onBusy?.(false);
    }
  };

  const body = (
    <Stack spacing={2} sx={{ maxWidth: embedded ? undefined : 520 }}>
      {!embedded && (
        <>
          <ReadOnlyField label="Demande de paiement N°" value={demande.reference} />
          <ReadOnlyField label="Bénéficiaire" value={beneficiaire} />
          <ReadOnlyField label="Objet" value={demande.objet} />
        </>
      )}

      {etabli && bon ? (
        embedded ? (
          <>
            <DetailFieldGrid
              fields={[{ label: 'Montant en lettres', value: bon.montantEnLettres, fullWidth: true }]}
            />
            <Stack direction="row" spacing={1}>
              <Button variant="outlined" disabled={busy} onClick={openPdf}>
                Ouvrir le PDF
              </Button>
            </Stack>
          </>
        ) : (
          <>
            <ReadOnlyField label="N° bon" value={bon.numeroBon} />
            <ReadOnlyField label="Date" value={formatDateFr(bon.dateBon)} />
            <ReadOnlyField label="Montant FC" value={formatMontantDevise(bon.montantFc, 'CDF')} />
            <ReadOnlyField label="Montant en lettres" value={bon.montantEnLettres} />
            <ReadOnlyField label="Établi par" value={bon.nomUtilisateurEtabli ?? '—'} />
            <Stack direction="row" spacing={1}>
              <Button variant="outlined" disabled={busy} onClick={openPdf}>
                Ouvrir le PDF
              </Button>
            </Stack>
          </>
        )
      ) : (
        enTraitement && (
          <Button variant="contained" disabled={busy} onClick={() => void handleEtablir()}>
            Établir le bon provisoire
          </Button>
        )
      )}
    </Stack>
  );

  if (embedded) {
    return (
      <>
        {!enTraitement && !etabli && (
          <Alert severity="info" sx={{ mb: 1.5 }}>
            Le bon provisoire pourra être établi lorsque la demande sera en traitement DPM.
          </Alert>
        )}
        {body}
      </>
    );
  }

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Typography variant="subtitle2" sx={{ mb: 1 }}>
        BON PROVISOIRE
      </Typography>

      {!enTraitement && !etabli && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Le bon provisoire pourra être établi lorsque la demande sera en traitement DPM.
        </Alert>
      )}

      {body}
    </Paper>
  );
}