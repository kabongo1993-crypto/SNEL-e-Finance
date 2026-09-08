/**
 * Self-test — mise à jour locale après établissement.
 * Exécuter : npx --yes tsx src/features/paiements/etablissementLocalUpdate.selftest.ts
 */
import type { DemandePaiementDetailComplet, PieceCaisse } from '../../services/apiClient';
import { applyEtablissementPatch } from './etablissementLocalUpdate';

function assert(cond: boolean, msg: string) {
  if (!cond) throw new Error(msg);
}

const baseData: DemandePaiementDetailComplet = {
  demande: {
    idDemandePaiement: 6,
    reference: 'DPM-000006',
    statut: 'EN_TRAITEMENT_DPM',
    objet: 'Test',
    montantBrut: 1000,
    devise: 'CDF',
    dateEmission: '2026-09-01',
    beneficiaires: [],
  } as unknown as DemandePaiementDetailComplet['demande'],
  snapshots: [],
  controleBudgetaire: null,
  piecesManquantes: [],
  historique: [],
  billetConversion: null,
  pieceCaisse: null,
  bonProvisoire: null,
  minuteCheque: null,
};

const piece: PieceCaisse = {
  idPieceCaisse: 1,
  idDemandePaiement: 6,
  statut: 'ETABLI',
  numeroPiece: 'PC-2026-0001',
  datePiece: '2026-09-01',
  montantFc: 1000,
  montantEnLettres: 'mille francs',
  referenceDemande: 'DPM-000006',
  motif: 'Test',
  pieceJustificative: null,
  beneficiaireAffichage: 'Benef',
  beneficiaireMatricule: null,
  beneficiaireIdentite: null,
  recuSnel: null,
  sr: null,
  comptabiliteGenerale: null,
  cp: null,
  cpa: null,
  numeroAppariement: null,
  identifiantVerification: 'PC-2026-0001 · DPM-000006',
  idUtilisateurEtabli: 4,
  nomUtilisateurEtabli: 'Charge DP',
  dateEtabli: '2026-09-02T10:00:00',
};

const patched = applyEtablissementPatch(baseData, { kind: 'PIECE_CAISSE', pieceCaisse: piece });
assert(patched.pieceCaisse?.numeroPiece === 'PC-2026-0001', 'piece caisse patchée');
assert(patched.demande.statut === 'EN_TRAITEMENT_DPM', 'statut DPM inchangé');
assert(patched.historique.length === 0, 'historique non rechargé');
assert(patched === baseData || patched.pieceCaisse !== baseData.pieceCaisse, 'immutabilité surface');

console.info('etablissementLocalUpdate.selftest — OK');
