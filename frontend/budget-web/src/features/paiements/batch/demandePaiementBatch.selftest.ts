/**
 * Batch DPM Phase 1+2+3A+3B+3C+3D — config + sélection + payloads.
 * npx --yes tsx src/features/paiements/batch/demandePaiementBatch.selftest.ts
 */
import {
  DEMANDE_PAIEMENT_BATCH_MAX_ITEMS,
  getBatchOpsForStatut,
  isBatchSelectableStatut,
  operationRequiresDeclaration,
  operationRequiresDocuments,
  operationRequiresOrientation,
  operationRequiresRetour,
  operationRequiresTraitement,
} from './demandePaiementBatchConfig.ts';
import { needsBilletConversion } from '../chargeDpmTauxUtils.ts';
import { buildDocumentsDraftFromDpm, toDocumentsPayload } from './demandePaiementBatchDocuments.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

assert(!isBatchSelectableStatut(''), 'Toutes : pas de selection');
assert(isBatchSelectableStatut('BROUILLON'), 'BROUILLON : selection active');
assert(!isBatchSelectableStatut('SOUMISE'), 'SOUMISE mes-demandes : pas ops');
assert(isBatchSelectableStatut('SOUMISE', 'charge-dpm'), 'SOUMISE charge-dpm : selection');
assert(isBatchSelectableStatut('EN_TRAITEMENT_DPM', 'charge-dpm'), 'EN_TRAITEMENT charge : selection');
assert(!isBatchSelectableStatut('EN_TRAITEMENT_DPM'), 'EN_TRAITEMENT mes-demandes : pas');
assert(!isBatchSelectableStatut('', 'charge-dpm'), 'Toutes charge-dpm : pas selection');

const charge = {
  permissions: ['paiements.lire', 'paiements.ecrire', 'paiements.charge_dpm', 'paiements.reception_budget'],
};
const demandeur = {
  permissions: ['paiements.lire', 'paiements.envoyer_validation', 'paiements.soumettre'],
};
const ecrivain = {
  permissions: ['paiements.lire', 'paiements.ecrire', 'paiements.envoyer_validation'],
};
const n1 = {
  permissions: ['paiements.lire', 'paiements.valider_n1', 'paiements.rejeter_validation_entite'],
};
const n2 = {
  permissions: ['paiements.lire', 'paiements.valider_n2', 'paiements.rejeter_validation_entite'],
};

assert(
  getBatchOpsForStatut('SOUMISE', charge, 'charge-dpm').map((o) => o.operation).join() === 'receptionner',
  'charge SOUMISE receptionner',
);
const enTraitementOps = getBatchOpsForStatut('EN_TRAITEMENT_DPM', charge, 'charge-dpm').map((o) => o.operation);
assert(enTraitementOps.includes('etablir-documents'), 'charge EN_TRAITEMENT etablir-documents');
assert(enTraitementOps.includes('traiter-charge'), 'charge EN_TRAITEMENT traiter-charge');
assert(enTraitementOps.includes('orienter'), 'charge EN_TRAITEMENT orienter');
assert(enTraitementOps[0] === 'etablir-documents', 'etablir avant traiter');
assert(getBatchOpsForStatut('SOUMISE', charge, 'charge-dpm').every((o) => o.operation !== 'orienter'), 'pas orienter sur SOUMISE');
assert(getBatchOpsForStatut('SOUMISE', charge, 'charge-dpm').every((o) => o.operation !== 'traiter-charge'), 'pas traiter sur SOUMISE');
assert(getBatchOpsForStatut('SOUMISE', charge, 'charge-dpm').every((o) => o.operation !== 'etablir-documents'), 'pas etablir sur SOUMISE');
assert(getBatchOpsForStatut('EN_TRAITEMENT_DPM', charge, 'charge-dpm').every((o) => o.operation !== 'receptionner'), 'pas reception sur EN_TRAITEMENT');
assert(getBatchOpsForStatut('', charge, 'charge-dpm').length === 0, 'Toutes charge : aucune op');
assert(getBatchOpsForStatut('EN_TRAITEMENT_DPM', demandeur, 'charge-dpm').length === 0, 'demandeur sans charge');

assert(operationRequiresDeclaration('declarer-validation-physique-n1'), 'N1 physique');
assert(!operationRequiresDeclaration('traiter-charge'), 'traiter sans declaration');
assert(operationRequiresTraitement('traiter-charge'), 'traiter requires traitement');
assert(!operationRequiresTraitement('receptionner'), 'receptionner sans traitement');
assert(operationRequiresDocuments('etablir-documents'), 'etablir requires documents');
assert(!operationRequiresDocuments('traiter-charge'), 'traiter sans documents payload');
assert(!operationRequiresDocuments('receptionner'), 'receptionner sans documents');
assert(operationRequiresOrientation('orienter'), 'orienter requires orientation dialog');
assert(!operationRequiresOrientation('traiter-charge'), 'traiter sans orientation');
assert(!operationRequiresOrientation('etablir-documents'), 'etablir sans orientation');
assert(operationRequiresRetour('rejeter-validation-n1'), 'rejet N1 requires retour');
assert(operationRequiresRetour('rejeter-validation-n2'), 'rejet N2 requires retour');
assert(!operationRequiresRetour('valider-n1'), 'valider sans retour');
assert(!operationRequiresRetour('supprimer'), 'supprimer sans retour');

assert(
  getBatchOpsForStatut('BROUILLON', ecrivain).some((o) => o.operation === 'supprimer'),
  'BROUILLON : supprimer batch',
);
assert(
  !getBatchOpsForStatut('BROUILLON', demandeur).some((o) => o.operation === 'supprimer'),
  'demandeur sans ecrire : pas supprimer',
);
assert(
  getBatchOpsForStatut('EN_VALIDATION_N1', n1).some((o) => o.operation === 'rejeter-validation-n1'),
  'N1 : rejet batch',
);
assert(
  !getBatchOpsForStatut('EN_VALIDATION_N1', n2).some((o) => o.operation === 'rejeter-validation-n1'),
  'N2 sans valider_n1 : pas rejet N1 (aligné unitaire)',
);
assert(
  getBatchOpsForStatut('EN_VALIDATION_N2', n2).some((o) => o.operation === 'rejeter-validation-n2'),
  'N2 : rejet batch',
);
assert(
  !getBatchOpsForStatut('EN_VALIDATION_N2', n1).some((o) => o.operation === 'rejeter-validation-n2'),
  'N1 sans valider_n2 : pas rejet N2 (aligné unitaire)',
);

const orientations = new Map<number, { idUtilisateurCible: number | null }>();
orientations.set(101, { idUtilisateurCible: 25 });
orientations.set(102, { idUtilisateurCible: null });
orientations.set(103, { idUtilisateurCible: 40 });
const orientItems = [101, 102, 103].map((idDemandePaiement) => ({
  idDemandePaiement,
  orientation: orientations.get(idDemandePaiement) ?? { idUtilisateurCible: null },
}));
assert(orientItems[0]!.orientation.idUtilisateurCible === 25, 'orient nominatif A');
assert(orientItems[1]!.orientation.idUtilisateurCible === null, 'orient pool');
assert(orientItems[2]!.orientation.idUtilisateurCible === 40, 'orient nominatif B');
assert(
  !('tauxPaiement' in (orientItems[0]!.orientation as object)),
  'orientation sans taux',
);
assert(
  !('typeInstrument' in (orientItems[0]!.orientation as object)),
  'orientation sans instrument',
);
const traitements = new Map<
  number,
  {
    typeInstrument: string;
    devisePaiement: string | null;
    tauxPaiement: number | null;
    idTauxChangePaiement: number | null;
    modePaiementSollicite: string;
    typeBudgetSollicite: string;
    itemSollicite: string | null;
  }
>();
traitements.set(101, {
  typeInstrument: 'PIECE_CAISSE',
  devisePaiement: 'CDF',
  tauxPaiement: null,
  idTauxChangePaiement: null,
  modePaiementSollicite: 'CAISSE',
  typeBudgetSollicite: 'DC',
  itemSollicite: null,
});
traitements.set(102, {
  typeInstrument: 'MINUTE_CHEQUE',
  devisePaiement: 'USD',
  tauxPaiement: null,
  idTauxChangePaiement: null,
  modePaiementSollicite: 'BANQUE',
  typeBudgetSollicite: 'AE',
  itemSollicite: 'ACTION_001',
});
const items = [101, 102].map((idDemandePaiement) => ({
  idDemandePaiement,
  traitement: traitements.get(idDemandePaiement) ?? null,
}));
assert(items[0]!.traitement?.typeInstrument === 'PIECE_CAISSE', 'payload DPM1');
assert(items[1]!.traitement?.typeInstrument === 'MINUTE_CHEQUE', 'payload DPM2 different');
assert(items[0]!.traitement?.tauxPaiement === null, 'taux non saisi');
assert(items[1]!.traitement?.idTauxChangePaiement === null, 'fk taux non saisie');
assert(items.every((i) => i.traitement != null), 'traitement individuel obligatoire');

const docsLot = [
  {
    idDemandePaiement: 101,
    mode: 'CAISSE',
    devise: 'CDF',
    instrument: 'PIECE_CAISSE',
  },
  {
    idDemandePaiement: 102,
    mode: 'CAISSE',
    devise: 'USD',
    instrument: 'PIECE_CAISSE',
  },
  {
    idDemandePaiement: 103,
    mode: 'CAISSE',
    devise: 'USD',
    instrument: 'BON_PROVISOIRE',
  },
  {
    idDemandePaiement: 104,
    mode: 'BANQUE',
    devise: 'USD',
    instrument: 'MINUTE_CHEQUE',
  },
].map((row) => {
  const draft = buildDocumentsDraftFromDpm({
    idDemandePaiement: row.idDemandePaiement,
    reference: `#${row.idDemandePaiement}`,
    deviseSollicitee: row.devise,
    modePaiementSollicite: row.mode,
    typeInstrumentPaiement: row.instrument,
  })!;
  return {
    idDemandePaiement: row.idDemandePaiement,
    documents: toDocumentsPayload(draft),
  };
});
assert(needsBilletConversion('CAISSE', 'USD'), 'règle billet USD CAISSE inchangée');
assert(docsLot[0]!.documents.billet === null, 'CDF CAISSE : billet non requis');
assert(docsLot[0]!.documents.pieceCaisse != null, 'DPM101 piece');
assert(docsLot[1]!.documents.billet != null, 'USD CAISSE : billet requis');
assert(docsLot[2]!.documents.bonProvisoire != null, 'DPM103 bon');
assert(docsLot[3]!.documents.minuteCheque != null, 'DPM104 minute');
assert(docsLot[3]!.documents.billet === null, 'BANQUE : billet non requis');
assert(docsLot.length === 4, 'lot heterogene 4 DPM');

assert(DEMANDE_PAIEMENT_BATCH_MAX_ITEMS === 100, 'limite 100');

const sampleResult = {
  totalSelectionne: 3,
  reussies: 1,
  ignorees: 1,
  erreurs: 1,
  details: [
    { idDemandePaiement: 1, outcome: 'SUCCESS', codeErreur: null },
    { idDemandePaiement: 2, outcome: 'IGNORED', codeErreur: 'STATUT_MISMATCH' },
    { idDemandePaiement: 3, outcome: 'ERROR', codeErreur: 'BUSINESS_RULE' },
  ],
};
const selectedAfter = new Set([1, 2, 3]);
for (const d of sampleResult.details) {
  if (d.outcome === 'SUCCESS' || d.outcome === 'IGNORED') selectedAfter.delete(d.idDemandePaiement);
}
assert(selectedAfter.size === 1 && selectedAfter.has(3), 'ERROR conserve');
assert(sampleResult.details.some((d) => d.codeErreur === 'BUSINESS_RULE'), 'codeErreur afficheable');

console.log('demandePaiementBatch.selftest OK');
