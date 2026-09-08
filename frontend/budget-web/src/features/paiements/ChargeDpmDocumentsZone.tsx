import { Stack, Typography } from '@mui/material';
import type {
  BonProvisoire,
  BilletConversion,
  DemandePaiementDetail,
  DemandePaiementDetailComplet,
  MinuteCheque,
  PieceCaisse,
} from '../../services/apiClient';
import { formatDateFr, formatTaux } from '../taux-change/tauxChangeUtils';
import { BilletConversionSection } from './BilletConversionSection';
import { BonProvisoireSection } from './BonProvisoireSection';
import {
  ChargeDpmDocumentCard,
  type ChargeDpmDocumentStatut,
} from './ChargeDpmDocumentCard';
import { MinuteChequeSection } from './MinuteChequeSection';
import { PieceCaisseSection } from './PieceCaisseSection';
import { formatMontantDevise } from './paiementUtils';
import type { EtablissementLocalPatch } from './etablissementLocalUpdate';
import type { RunDemandePaiementMutationOptions } from './useDemandePaiementMutationLock';

type Props = {
  demande: DemandePaiementDetail;
  data: DemandePaiementDetailComplet;
  enTraitement: boolean;
  modePaiement: 'CAISSE' | 'BANQUE';
  instrument: string;
  billetRequired: boolean;
  busy: boolean;
  onEtablissementLocal: (patch: EtablissementLocalPatch) => void;
  onError: (message: string) => void;
  guardAction?: <T>(
    action: () => Promise<T>,
    options?: RunDemandePaiementMutationOptions,
  ) => Promise<T | undefined>;
};

function principalBeneficiaire(demande: DemandePaiementDetail): string {
  const principal = demande.beneficiaires?.find((b) => b.estPrincipal) ?? demande.beneficiaires?.[0];
  if (!principal) return '—';
  return principal.raisonSociale?.trim() || principal.nomComplet?.trim() || '—';
}

function documentStatut(etabli: boolean, enTraitement: boolean): ChargeDpmDocumentStatut {
  if (etabli) return 'ETABLI';
  if (enTraitement) return 'A_ETABLIR';
  return 'EN_ATTENTE';
}

function billetSummary(
  demande: DemandePaiementDetail,
  billet: BilletConversion | null,
  enTraitement: boolean,
): { statut: ChargeDpmDocumentStatut; fields: { label: string; value: string; fullWidth?: boolean }[] } {
  const etabli = billet?.statut?.toUpperCase() === 'ETABLI';
  const beneficiaire = billet?.beneficiaireAffichage ?? principalBeneficiaire(demande);
  if (etabli && billet) {
    return {
      statut: 'ETABLI',
      fields: [
        { label: 'Bénéficiaire', value: beneficiaire },
        {
          label: 'Montant converti',
          value: formatMontantDevise(billet.montantCdf, 'CDF'),
        },
        { label: 'Taux appliqué', value: formatTaux(billet.tauxApplique) },
        { label: 'Date de conversion', value: formatDateFr(billet.dateConversion) },
        { label: 'Établi par', value: billet.nomUtilisateurEtabli ?? '—' },
      ],
    };
  }
  return {
    statut: documentStatut(false, enTraitement),
    fields: [
      { label: 'Montant à convertir', value: formatMontantDevise(demande.montantBrut, demande.devise) },
      { label: 'Devise', value: demande.devise },
      { label: 'Bénéficiaire', value: beneficiaire },
    ],
  };
}

function pieceSummary(
  demande: DemandePaiementDetail,
  piece: PieceCaisse | null,
  enTraitement: boolean,
): { statut: ChargeDpmDocumentStatut; fields: { label: string; value: string; fullWidth?: boolean }[] } {
  const etabli = piece?.statut?.toUpperCase() === 'ETABLI';
  const beneficiaire = piece?.beneficiaireAffichage ?? principalBeneficiaire(demande);
  if (etabli && piece) {
    return {
      statut: 'ETABLI',
      fields: [
        { label: 'N° pièce', value: piece.numeroPiece },
        { label: 'Date', value: formatDateFr(piece.datePiece) },
        { label: 'Montant FC', value: formatMontantDevise(piece.montantFc, 'CDF') },
        { label: 'Établi par', value: piece.nomUtilisateurEtabli ?? '—' },
      ],
    };
  }
  return {
    statut: documentStatut(false, enTraitement),
    fields: [
      { label: 'Bénéficiaire', value: beneficiaire },
      { label: 'Objet', value: demande.objet, fullWidth: true },
    ],
  };
}

function bonSummary(
  demande: DemandePaiementDetail,
  bon: BonProvisoire | null,
  enTraitement: boolean,
): { statut: ChargeDpmDocumentStatut; fields: { label: string; value: string; fullWidth?: boolean }[] } {
  const etabli = bon?.statut?.toUpperCase() === 'ETABLI';
  const beneficiaire = bon?.beneficiaireAffichage ?? principalBeneficiaire(demande);
  if (etabli && bon) {
    return {
      statut: 'ETABLI' as const,
      fields: [
        { label: 'N° bon', value: bon.numeroBon },
        { label: 'Date', value: formatDateFr(bon.dateBon) },
        { label: 'Montant FC', value: formatMontantDevise(bon.montantFc, 'CDF') },
        { label: 'Établi par', value: bon.nomUtilisateurEtabli ?? '—' },
      ],
    };
  }
  return {
    statut: documentStatut(false, enTraitement),
    fields: [
      { label: 'Bénéficiaire', value: beneficiaire },
      { label: 'Objet', value: demande.objet, fullWidth: true },
    ],
  };
}

function minuteSummary(
  demande: DemandePaiementDetail,
  minute: MinuteCheque | null,
  enTraitement: boolean,
): { statut: ChargeDpmDocumentStatut; fields: { label: string; value: string; fullWidth?: boolean }[] } {
  const etabli = minute?.statut?.toUpperCase() === 'ETABLI';
  const beneficiaire = minute?.beneficiaireAffichage ?? principalBeneficiaire(demande);
  if (etabli && minute) {
    return {
      statut: 'ETABLI' as const,
      fields: [
        { label: 'N° O.P.', value: minute.numeroOp },
        { label: 'Date', value: formatDateFr(minute.dateDocument) },
        {
          label: 'Montant',
          value: formatMontantDevise(minute.montantPaiement, minute.devisePaiement),
        },
        { label: 'Établi par', value: minute.nomUtilisateurEtabli ?? '—' },
      ],
    };
  }
  return {
    statut: documentStatut(false, enTraitement),
    fields: [
      { label: 'Bénéficiaire', value: beneficiaire },
      { label: 'Objet', value: demande.objet, fullWidth: true },
    ],
  };
}

/** Zone documents du paiement — cartes dossier encapsulant les sections métier existantes. */
export function ChargeDpmDocumentsZone({
  demande,
  data,
  enTraitement,
  modePaiement,
  instrument,
  billetRequired,
  busy,
  onEtablissementLocal,
  onError,
  guardAction,
}: Props) {
  const billetMeta = billetRequired
    ? billetSummary(demande, data.billetConversion, enTraitement)
    : null;

  const instrumentSection = (() => {
    if (modePaiement === 'CAISSE' && instrument === 'PIECE_CAISSE') {
      const meta = pieceSummary(demande, data.pieceCaisse, enTraitement);
      return (
        <ChargeDpmDocumentCard title="Pièce de caisse" statut={meta.statut} summaryFields={meta.fields}>
          <PieceCaisseSection
            embedded
            demande={demande}
            piece={data.pieceCaisse}
            enTraitement={enTraitement}
            busy={busy}
            guardAction={guardAction}
            onEtablissementLocal={onEtablissementLocal}
            onError={onError}
          />
        </ChargeDpmDocumentCard>
      );
    }
    if (modePaiement === 'CAISSE' && instrument === 'BON_PROVISOIRE') {
      const meta = bonSummary(demande, data.bonProvisoire, enTraitement);
      return (
        <ChargeDpmDocumentCard title="Bon provisoire" statut={meta.statut} summaryFields={meta.fields}>
          <BonProvisoireSection
            embedded
            demande={demande}
            bon={data.bonProvisoire}
            enTraitement={enTraitement}
            busy={busy}
            guardAction={guardAction}
            onEtablissementLocal={onEtablissementLocal}
            onError={onError}
          />
        </ChargeDpmDocumentCard>
      );
    }
    if (modePaiement === 'BANQUE' && instrument === 'MINUTE_CHEQUE') {
      const meta = minuteSummary(demande, data.minuteCheque, enTraitement);
      return (
        <ChargeDpmDocumentCard title="Minute de chèque" statut={meta.statut} summaryFields={meta.fields}>
          <MinuteChequeSection
            embedded
            demande={demande}
            minute={data.minuteCheque}
            enTraitement={enTraitement}
            busy={busy}
            guardAction={guardAction}
            onEtablissementLocal={onEtablissementLocal}
            onError={onError}
          />
        </ChargeDpmDocumentCard>
      );
    }
    return null;
  })();

  if (!billetRequired && !instrumentSection) {
    return (
      <Typography variant="body2" color="text.secondary">
        Aucun document de paiement requis pour la configuration actuelle.
      </Typography>
    );
  }

  return (
    <Stack spacing={2}>
      {billetRequired && billetMeta && (
        <ChargeDpmDocumentCard
          title="Billet de conversion"
          statut={billetMeta.statut}
          summaryFields={billetMeta.fields}
        >
          <BilletConversionSection
            embedded
            demande={demande}
            billet={data.billetConversion}
            enTraitement={enTraitement}
            busy={busy}
            guardAction={guardAction}
            onEtablissementLocal={onEtablissementLocal}
            onError={onError}
          />
        </ChargeDpmDocumentCard>
      )}
      {instrumentSection}
    </Stack>
  );
}
