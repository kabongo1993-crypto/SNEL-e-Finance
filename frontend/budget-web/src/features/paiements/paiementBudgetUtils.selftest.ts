/**
 * npx --yes tsx src/features/paiements/paiementBudgetUtils.selftest.ts
 */
import type { ControleBudgetaireDto, DemandePaiementListItem, UniteBudgetaire } from '../../services/apiClient';
import {
  buildControleSummary,
  buildTimelineSteps,
  dcControlesOk,
  enrichDemandesWithDepartement,
  filtreFileBudgets,
  groupDemandesByDepartement,
  labelOperationAudit,
} from './paiementBudgetUtils';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

const ub: UniteBudgetaire = {
  idUB: 1,
  codeUB: 'UB01',
  libelle: 'UB Test',
  idDepartement: 10,
  departementCode: 'D10',
  departementLibelle: 'Direction A',
  idStructure: 1,
  structureCode: 'S1',
  structureLibelle: 'Struct',
  structureType: 'DIRECTION',
  actif: true,
  dateCreation: '2026-01-01',
  nombrePrevisions: 0,
};

const demande: DemandePaiementListItem = {
  idDemandePaiement: 1,
  reference: 'DP-001',
  dateEmission: '2026-01-15',
  idExercice: 1,
  anneeExercice: 2026,
  idUB: 1,
  codeUB: 'UB01',
  libelleUB: 'UB Test',
  idDemandeur: 1,
  codeDemandeur: 'DDK/DKC/DG',
  libelleDemandeur: 'Demandeur test',
  idDepartement: 10,
  codeDepartement: 'D10',
  libelleDepartement: 'Direction A',
  idCasDossier: 1,
  codeCasDossier: 'C1',
  libelleCasDossier: 'Cas 1',
  objet: 'Test',
  montantBrut: 100,
  devise: 'USD',
  montantUsd: 100,
  statut: 'SOUMISE',
  dateSoumission: '2026-01-16',
  dateCreation: '2026-01-14',
  idUtilisateurAssigne: null,
  nomUtilisateurAssigne: null,
  prenomUtilisateurAssigne: null,
};

const enriched = enrichDemandesWithDepartement([demande], [ub]);
assert(enriched[0].libelleDepartement === 'Direction A', 'departement enrichi');

const groups = groupDemandesByDepartement(enriched);
assert(groups.length === 1, 'un groupe');
assert(groups[0].nbSoumises === 1, 'soumises');

assert(filtreFileBudgets(enriched[0], 'FILE') === true, 'file soumise');
assert(filtreFileBudgets({ ...enriched[0], statut: 'BROUILLON' }, 'FILE') === false, 'file brouillon');

const controle: ControleBudgetaireDto = {
  estValide: false,
  motifRejet: 'Crédit insuffisant',
  imputations: [
    {
      idImputation: 1,
      ordre: 1,
      codeTypeBudget: 'DC',
      estValide: false,
      motifRejet: 'Mensuel',
      budgetAnnuel: 1000,
      budgetMensuel: 100,
      creditEngageAnnuel: 200,
      creditEngageMensuel: 50,
      engagementEnCours: 80,
      creditDisponibleAnnuel: 720,
      creditDisponibleMensuel: -30,
      montantPrevision: 0,
      ecartPrevisionImputation: -80,
      montantCourantUsd: 80,
      idBudgetLigne: null,
    },
  ],
};

const summary = buildControleSummary(controle, 80);
assert(summary.estValide === false, 'controle invalide');
assert(summary.motifRejet === 'Crédit insuffisant', 'motif');

const dc = controle.imputations[0];
assert(dcControlesOk(dc).mensuelOk === false, 'dc mensuel ko');
assert(dcControlesOk({ ...dc, creditDisponibleMensuel: 10 }).mensuelOk === true, 'dc mensuel ok');

assert(labelOperationAudit('RECEPTIONNER') === 'Réception Chargé DP', 'label audit');

const timeline = buildTimelineSteps(
  {
    idDemandePaiement: 1,
    reference: 'DP-001',
    dateEmission: '2026-01-15',
    lieuEmission: null,
    idExercice: 1,
    anneeExercice: 2026,
    idVersion: null,
    numeroVersion: null,
    idUB: 1,
    codeUB: 'UB01',
    libelleUB: 'UB',
    idDemandeur: 1,
    codeDemandeur: 'DDK/DKC/DG',
    libelleDemandeur: 'Demandeur',
    idDepartement: 10,
    codeDepartement: 'D10',
    libelleDepartement: 'Direction A',
    idCasDossier: 1,
    codeCasDossier: 'C1',
    libelleCasDossier: 'Cas',
    idTypeBudget: 1,
    codeTypeBudget: 'DC',
    typeBudgetSollicite: 'DC',
    itemSollicite: null,
    destinationSolliciteeAffichage: 'DC',
    objet: 'Obj',
    compteSection: null,
    montantBrut: 100,
    devise: 'USD',
    tauxConversion: 1,
    montantUsd: 100,
    idTauxChange: null,
    modePaiementSollicite: 'CAISSE',
    typeInstrumentPaiement: 'PIECE_CAISSE',
    devisePaiement: 'CDF',
    montantPaiement: 229000,
    tauxPaiement: 2290,
    idTauxChangePaiement: null,
    statut: 'EN_CONTROLE_BUDGETAIRE',
    motifRetour: null,
    commentaireRetour: null,
    dateCreation: '2026-01-14',
    dateSoumission: '2026-01-16',
    dateReception: '2026-01-17',
    dateControle: '2026-01-18',
    dateVisa: null,
    dateRetour: null,
    beneficiaires: [],
    imputations: [],
    pieces: [],
    idUtilisateurAssigne: null,
    nomUtilisateurAssigne: null,
    prenomUtilisateurAssigne: null,
  },
  [],
);
assert(timeline.some((s) => s.key === 'reception' && s.done), 'timeline reception');

console.log('paiementBudgetUtils.selftest OK');
