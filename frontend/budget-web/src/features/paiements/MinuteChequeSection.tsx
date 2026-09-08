import {
  Alert,
  Button,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useMemo } from 'react';
import { useDocumentViewer, buildMinuteChequePdfViewerOptions } from '../../components/documentViewer';
import {
  etablirMinuteCheque,
  type DemandePaiementDetail,
  type MinuteCheque,
} from '../../services/apiClient';
import { formatDateFr } from '../taux-change/tauxChangeUtils';
import { DetailFieldGrid } from './DetailFieldGrid';
import { apiErrorMessage, formatMontantDevise } from './paiementUtils';
import type { EtablissementLocalPatch } from './etablissementLocalUpdate';
import type { RunDemandePaiementMutationOptions } from './useDemandePaiementMutationLock';

type Props = {
  demande: DemandePaiementDetail;
  minute: MinuteCheque | null;
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

export function MinuteChequeSection({
  demande,
  minute,
  enTraitement,
  busy,
  onEtablissementLocal,
  onError,
  onBusy,
  embedded = false,
  guardAction,
}: Props) {
  const { openDocument } = useDocumentViewer();
  const etabli = minute?.statut?.toUpperCase() === 'ETABLI';

  const beneficiaire = useMemo(() => {
    if (minute?.beneficiaireAffichage) return minute.beneficiaireAffichage;
    const principal = demande.beneficiaires?.find((b) => b.estPrincipal) ?? demande.beneficiaires?.[0];
    if (!principal) return '—';
    return principal.raisonSociale?.trim() || principal.nomComplet?.trim() || '—';
  }, [minute?.beneficiaireAffichage, demande.beneficiaires]);

  const openPdf = () => {
    openDocument(buildMinuteChequePdfViewerOptions(demande.idDemandePaiement));
  };

  const handleEtablir = async () => {
    if (busy) return;

    const execute = async () => {
      const {
        beginEtablissementPerf,
        markEtablissementLocalUpdateStart,
        markEtablissementLocalUpdateEnd,
      } = await import('./etablissementPerf');
      beginEtablissementPerf('MINUTE_CHEQUE', demande.idDemandePaiement);
      const minuteCheque = await etablirMinuteCheque(demande.idDemandePaiement, {
        dateDocument: demande.dateEmission?.slice(0, 10) ?? null,
      });
      markEtablissementLocalUpdateStart('MINUTE_CHEQUE', demande.idDemandePaiement);
      onEtablissementLocal({ kind: 'MINUTE_CHEQUE', minuteCheque });
      markEtablissementLocalUpdateEnd('MINUTE_CHEQUE', demande.idDemandePaiement);
    };

    if (guardAction) {
      try {
        await guardAction(execute, { message: 'Établissement de la minute chèque' });
      } catch (err) {
        onError(apiErrorMessage(err, 'Impossible d’établir la minute de chèque.'));
      }
      return;
    }

    onBusy?.(true);
    try {
      await execute();
    } catch (err) {
      onError(apiErrorMessage(err, 'Impossible d’établir la minute de chèque.'));
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

      {etabli && minute ? (
        embedded ? (
          <>
            <DetailFieldGrid
              fields={[{ label: 'Montant en lettres', value: minute.montantEnLettres, fullWidth: true }]}
            />
            <Stack direction="row" spacing={1}>
              <Button variant="outlined" disabled={busy} onClick={openPdf}>
                Ouvrir le PDF
              </Button>
            </Stack>
          </>
        ) : (
          <>
            <ReadOnlyField label="N° O.P." value={minute.numeroOp} />
            <ReadOnlyField label="Date" value={formatDateFr(minute.dateDocument)} />
            <ReadOnlyField
              label="Montant"
              value={formatMontantDevise(minute.montantPaiement, minute.devisePaiement)}
            />
            <ReadOnlyField label="Montant en lettres" value={minute.montantEnLettres} />
            <ReadOnlyField label="Établi par" value={minute.nomUtilisateurEtabli ?? '—'} />
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
            Établir la minute de chèque
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
            La minute de chèque pourra être établie lorsque la demande sera en traitement DPM.
          </Alert>
        )}
        {body}
      </>
    );
  }

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Typography variant="subtitle2" sx={{ mb: 1 }}>
        MINUTE DE CHÈQUE
      </Typography>

      {!enTraitement && !etabli && (
        <Alert severity="info" sx={{ mb: 2 }}>
          La minute de chèque pourra être établie lorsque la demande sera en traitement DPM.
        </Alert>
      )}

      {body}
    </Paper>
  );
}