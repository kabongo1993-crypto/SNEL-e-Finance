import type {
  BilletConversion,
  BonProvisoire,
  DemandePaiementDetailComplet,
  MinuteCheque,
  PieceCaisse,
} from '../../services/apiClient';

/** Patch minimal après POST d'établissement — évite GET /complet. */
export type EtablissementLocalPatch =
  | { kind: 'PIECE_CAISSE'; pieceCaisse: PieceCaisse }
  | { kind: 'BON_PROVISOIRE'; bonProvisoire: BonProvisoire }
  | { kind: 'MINUTE_CHEQUE'; minuteCheque: MinuteCheque }
  | { kind: 'BILLET_CONVERSION'; billetConversion: BilletConversion };

export function applyEtablissementPatch(
  data: DemandePaiementDetailComplet,
  patch: EtablissementLocalPatch,
): DemandePaiementDetailComplet {
  switch (patch.kind) {
    case 'PIECE_CAISSE':
      return { ...data, pieceCaisse: patch.pieceCaisse };
    case 'BON_PROVISOIRE':
      return { ...data, bonProvisoire: patch.bonProvisoire };
    case 'MINUTE_CHEQUE':
      return { ...data, minuteCheque: patch.minuteCheque };
    case 'BILLET_CONVERSION':
      return { ...data, billetConversion: patch.billetConversion };
  }
}
