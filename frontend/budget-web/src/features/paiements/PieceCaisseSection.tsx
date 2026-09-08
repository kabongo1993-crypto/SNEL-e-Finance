import {
  Alert,
  Button,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useMemo } from 'react';
import { useDocumentViewer, buildPieceCaissePdfViewerOptions } from '../../components/documentViewer';
import {
  etablirPieceCaisse,
  type DemandePaiementDetail,
  type PieceCaisse,
} from '../../services/apiClient';
import { formatDateFr } from '../taux-change/tauxChangeUtils';
import { DetailFieldGrid } from './DetailFieldGrid';
import { apiErrorMessage, formatMontantDevise } from './paiementUtils';
import type { EtablissementLocalPatch } from './etablissementLocalUpdate';
import type { RunDemandePaiementMutationOptions } from './useDemandePaiementMutationLock';

type Props = {
  demande: DemandePaiementDetail;
  piece: PieceCaisse | null;
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

export function PieceCaisseSection({
  demande,
  piece,
  enTraitement,
  busy,
  onEtablissementLocal,
  onError,
  onBusy,
  embedded = false,
  guardAction,
}: Props) {
  const { openDocument } = useDocumentViewer();
  const etabli = piece?.statut?.toUpperCase() === 'ETABLI';

  const beneficiaire = useMemo(() => {
    if (piece?.beneficiaireAffichage) return piece.beneficiaireAffichage;
    const principal = demande.beneficiaires?.find((b) => b.estPrincipal) ?? demande.beneficiaires?.[0];
    if (!principal) return '—';
    return principal.raisonSociale?.trim() || principal.nomComplet?.trim() || '—';
  }, [piece?.beneficiaireAffichage, demande.beneficiaires]);

  const openPdf = () => {
    openDocument(buildPieceCaissePdfViewerOptions(demande.idDemandePaiement));
  };

  const handleEtablir = async () => {
    if (busy) return;

    const execute = async () => {
      const {
        beginEtablissementPerf,
        markEtablissementLocalUpdateStart,
        markEtablissementLocalUpdateEnd,
      } = await import('./etablissementPerf');
      beginEtablissementPerf('PIECE_CAISSE', demande.idDemandePaiement);
      const pieceCaisse = await etablirPieceCaisse(demande.idDemandePaiement, {
        datePiece: demande.dateEmission?.slice(0, 10) ?? null,
      });
      markEtablissementLocalUpdateStart('PIECE_CAISSE', demande.idDemandePaiement);
      onEtablissementLocal({ kind: 'PIECE_CAISSE', pieceCaisse });
      markEtablissementLocalUpdateEnd('PIECE_CAISSE', demande.idDemandePaiement);
    };

    if (guardAction) {
      try {
        await guardAction(execute, { message: 'Établissement de la pièce de caisse' });
      } catch (err) {
        onError(apiErrorMessage(err, 'Impossible d’établir la pièce de caisse.'));
      }
      return;
    }

    onBusy?.(true);
    try {
      await execute();
    } catch (err) {
      onError(apiErrorMessage(err, 'Impossible d’établir la pièce de caisse.'));
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

      {etabli && piece ? (
        embedded ? (
          <>
            <DetailFieldGrid
              fields={[{ label: 'Montant en lettres', value: piece.montantEnLettres, fullWidth: true }]}
            />
            <Stack direction="row" spacing={1}>
              <Button variant="outlined" disabled={busy} onClick={openPdf}>
                Ouvrir le PDF
              </Button>
            </Stack>
          </>
        ) : (
          <>
            <ReadOnlyField label="N° pièce" value={piece.numeroPiece} />
            <ReadOnlyField label="Date" value={formatDateFr(piece.datePiece)} />
            <ReadOnlyField label="Montant FC" value={formatMontantDevise(piece.montantFc, 'CDF')} />
            <ReadOnlyField label="Montant en lettres" value={piece.montantEnLettres} />
            <ReadOnlyField label="Établi par" value={piece.nomUtilisateurEtabli ?? '—'} />
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
            Établir la pièce de caisse
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
            La pièce de caisse pourra être établie lorsque la demande sera en traitement DPM.
          </Alert>
        )}
        {body}
      </>
    );
  }

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Typography variant="subtitle2" sx={{ mb: 1 }}>
        PIÈCE DE CAISSE
      </Typography>

      {!enTraitement && !etabli && (
        <Alert severity="info" sx={{ mb: 2 }}>
          La pièce de caisse pourra être établie lorsque la demande sera en traitement DPM.
        </Alert>
      )}

      {body}
    </Paper>
  );
}