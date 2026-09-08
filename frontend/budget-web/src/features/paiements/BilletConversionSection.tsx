import {
  Alert,
  Button,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useMemo, useState } from 'react';
import { useDocumentViewer, buildBilletConversionPdfViewerOptions } from '../../components/documentViewer';
import { AmountField } from '../../components';
import {
  etablirBilletConversion,
  type BilletConversion,
  type DemandePaiementDetail,
} from '../../services/apiClient';
import { formatDateFr, formatTaux } from '../taux-change/tauxChangeUtils';
import { DetailFieldGrid } from './DetailFieldGrid';
import { apiErrorMessage, formatMontantDevise } from './paiementUtils';
import type { EtablissementLocalPatch } from './etablissementLocalUpdate';
import type { RunDemandePaiementMutationOptions } from './useDemandePaiementMutationLock';

type Props = {
  demande: DemandePaiementDetail;
  billet: BilletConversion | null;
  enTraitement: boolean;
  busy: boolean;
  onEtablissementLocal: (patch: EtablissementLocalPatch) => void;
  onError: (message: string) => void;
  onBusy?: (value: boolean) => void;
  /** Mode intégré dans une carte document (sans enveloppe Paper ni champs dupliqués). */
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

export function BilletConversionSection({
  demande,
  billet,
  enTraitement,
  busy,
  onEtablissementLocal,
  onError,
  onBusy,
  embedded = false,
  guardAction,
}: Props) {
  const { openDocument } = useDocumentViewer();
  const [demandeCheque, setDemandeCheque] = useState('');
  const [coursBanque, setCoursBanque] = useState('');
  const [soldeDevise, setSoldeDevise] = useState('');

  const etabli = billet?.statut?.toUpperCase() === 'ETABLI';
  const beneficiaire = useMemo(() => {
    if (billet?.beneficiaireAffichage) return billet.beneficiaireAffichage;
    const principal = demande.beneficiaires?.find((b) => b.estPrincipal) ?? demande.beneficiaires?.[0];
    if (!principal) return '—';
    return principal.raisonSociale?.trim() || principal.nomComplet?.trim() || '—';
  }, [billet?.beneficiaireAffichage, demande.beneficiaires]);

  const openPdf = () => {
    openDocument(
      buildBilletConversionPdfViewerOptions(demande.idDemandePaiement, demande.reference),
    );
  };

  const handleEtablir = async () => {
    if (busy) return;

    const execute = async () => {
      const {
        beginEtablissementPerf,
        markEtablissementLocalUpdateStart,
        markEtablissementLocalUpdateEnd,
      } = await import('./etablissementPerf');
      beginEtablissementPerf('BILLET_CONVERSION', demande.idDemandePaiement);
      const solde =
        soldeDevise.trim() === '' ? null : Number.parseFloat(soldeDevise.replace(',', '.'));
      if (soldeDevise.trim() !== '' && Number.isNaN(solde)) {
        onError('Le solde à payer doit être un nombre valide ou laissé vide.');
        return;
      }

      const billetConversion = await etablirBilletConversion(demande.idDemandePaiement, {
        demandeChequeNumero: demandeCheque.trim() || null,
        coursEchangeBanque: coursBanque.trim() || null,
        soldeAPayerDevise: solde,
      });
      markEtablissementLocalUpdateStart('BILLET_CONVERSION', demande.idDemandePaiement);
      onEtablissementLocal({ kind: 'BILLET_CONVERSION', billetConversion });
      markEtablissementLocalUpdateEnd('BILLET_CONVERSION', demande.idDemandePaiement);
    };

    if (guardAction) {
      try {
        await guardAction(execute, { message: 'Établissement du billet de conversion' });
      } catch (err) {
        onError(apiErrorMessage(err, 'Impossible d’établir le billet de conversion.'));
      }
      return;
    }

    onBusy?.(true);
    try {
      await execute();
    } catch (err) {
      onError(apiErrorMessage(err, 'Impossible d’établir le billet de conversion.'));
    } finally {
      onBusy?.(false);
    }
  };

  const body = (
    <Stack spacing={2} sx={{ maxWidth: embedded ? undefined : 520 }}>
      {!embedded && (
        <>
          <ReadOnlyField label="ID" value={String(demande.idDemandePaiement)} />
          <ReadOnlyField label="Demande de paiement N°" value={demande.reference} />
          <ReadOnlyField label="Bénéficiaire" value={beneficiaire} />
          <ReadOnlyField
            label="Montant à payer (total)"
            value={formatMontantDevise(demande.montantBrut, demande.devise)}
          />
          <ReadOnlyField label="Devise" value={demande.devise} />
        </>
      )}

      {etabli && billet ? (
        embedded ? (
          <>
            <DetailFieldGrid
              fields={[
                {
                  label: 'Demande de chèque N°',
                  value: billet.demandeChequeNumero ?? '—',
                },
                {
                  label: "Cours d'échange / banque",
                  value: billet.coursEchangeBanque ?? '—',
                },
                {
                  label: 'Solde à payer (devise)',
                  value:
                    billet.soldeAPayerDevise != null
                      ? formatMontantDevise(billet.soldeAPayerDevise, billet.deviseOrigine)
                      : '—',
                },
                { label: 'Approuvé par', value: billet.nomUtilisateurApprouve ?? '—' },
                { label: 'Visa', value: billet.nomUtilisateurVisa ?? '—' },
              ]}
            />
            <Stack direction="row" spacing={1}>
              <Button variant="outlined" disabled={busy} onClick={openPdf}>
                Ouvrir le PDF
              </Button>
            </Stack>
          </>
        ) : (
          <>
            <ReadOnlyField
              label="Demande de chèque N°"
              value={billet.demandeChequeNumero ?? '—'}
            />
            <ReadOnlyField label="Taux utilisé" value={formatTaux(billet.tauxApplique)} />
            <ReadOnlyField label="Date de conversion" value={formatDateFr(billet.dateConversion)} />
            <ReadOnlyField
              label="Cours d'échange / banque"
              value={billet.coursEchangeBanque ?? '—'}
            />
            <ReadOnlyField
              label="Montant en FC"
              value={formatMontantDevise(billet.montantCdf, 'CDF')}
            />
            <ReadOnlyField
              label="Solde à payer sur la demande (devise)"
              value={
                billet.soldeAPayerDevise != null
                  ? formatMontantDevise(billet.soldeAPayerDevise, billet.deviseOrigine)
                  : '—'
              }
            />
            <ReadOnlyField label="Établi par" value={billet.nomUtilisateurEtabli ?? '—'} />
            <ReadOnlyField label="Approuvé par" value={billet.nomUtilisateurApprouve ?? '—'} />
            <ReadOnlyField label="Visa" value={billet.nomUtilisateurVisa ?? '—'} />

            <Stack direction="row" spacing={1}>
              <Button variant="outlined" disabled={busy} onClick={openPdf}>
                Ouvrir le PDF
              </Button>
            </Stack>
          </>
        )
      ) : (
        <>
          <TextField
            label="Demande de chèque N° (facultatif)"
            value={demandeCheque}
            onChange={(e) => setDemandeCheque(e.target.value)}
            disabled={!enTraitement || busy}
            fullWidth
            size={embedded ? 'small' : 'medium'}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <AmountField
            label="Cours d'échange / banque (facultatif)"
            value={coursBanque}
            onChange={setCoursBanque}
            disabled={!enTraitement || busy}
            fullWidth
            size={embedded ? 'small' : 'medium'}
            currency={null}
            optional
          />
          <AmountField
            label="Solde à payer sur la demande (devise) (facultatif)"
            value={soldeDevise}
            onChange={setSoldeDevise}
            disabled={!enTraitement || busy}
            fullWidth
            size={embedded ? 'small' : 'medium'}
            currency={null}
            optional
          />

          {enTraitement && (
            <Button variant="contained" disabled={busy} onClick={() => void handleEtablir()}>
              Établir le billet de conversion
            </Button>
          )}
        </>
      )}
    </Stack>
  );

  if (embedded) {
    return (
      <>
        {!enTraitement && !etabli && (
          <Alert severity="info" sx={{ mb: 1.5 }}>
            Le billet de conversion pourra être établi lorsque la demande sera en traitement DPM.
          </Alert>
        )}
        {body}
      </>
    );
  }

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Typography variant="subtitle2" sx={{ mb: 1 }}>
        BILLET DE CONVERSION
      </Typography>

      {!enTraitement && !etabli && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Le billet de conversion pourra être établi lorsque la demande sera en traitement DPM.
        </Alert>
      )}

      {body}
    </Paper>
  );
}
