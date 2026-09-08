/**
 * Lot 3.6.4 — routage API, destinataires retour, workflow.
 * npx --yes tsx src/features/paiements/demandePaiementLot364.selftest.ts
 */
import {
  buildWorkflowRoutageItems,
  DemandePaiementRetourType,
  DemandePaiementRoutageAction,
  formatRetourDestinataireLabel,
  labelRetourDestinataireHintFromApi,
  findRetourPourStatutACorriger,
} from './demandePaiementRoutageUtils.ts';
import {
  peutAfficherActionsMetierAssignation,
  resolveAssignationDisplay,
} from './demandePaiementAssignationUtils.ts';
import type {
  DemandePaiementRetourDestinataire,
  DemandePaiementRoutage,
} from '../../services/apiClient.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

const sampleRoutage = (
  partial: Partial<DemandePaiementRoutage> & Pick<DemandePaiementRoutage, 'idRoutage' | 'action'>,
): DemandePaiementRoutage => ({
  statutSource: 'SOUMISE',
  statutCible: 'EN_TRAITEMENT_DPM',
  idUtilisateurSource: 1,
  nomUtilisateurSource: 'Charge',
  prenomUtilisateurSource: 'Un',
  idUtilisateurCible: 2,
  nomUtilisateurCible: 'Charge',
  prenomUtilisateurCible: 'Deux',
  dateRoutage: '2026-03-01T10:00:00',
  estActif: false,
  motif: null,
  ...partial,
});

// 1 — workflow alimenté par routage API
const routages: DemandePaiementRoutage[] = [
  sampleRoutage({
    idRoutage: 1,
    action: DemandePaiementRoutageAction.Receptionner,
    statutSource: 'SOUMISE',
    statutCible: 'EN_TRAITEMENT_DPM',
    dateRoutage: '2026-03-01T09:00:00',
  }),
  sampleRoutage({
    idRoutage: 2,
    action: DemandePaiementRoutageAction.Orienter,
    statutSource: 'EN_TRAITEMENT_DPM',
    statutCible: 'EN_CONTROLE_BUDGETAIRE',
    dateRoutage: '2026-03-01T11:00:00',
  }),
];
const items = buildWorkflowRoutageItems(routages, []);
assert(items.length === 2, 'workflow : 2 transmissions');
assert(items[0]!.idRoutage === 1, 'ordre chronologique');

// 2 — routage vide
assert(buildWorkflowRoutageItems([], []).length === 0, 'routage vide');

// 3 — plusieurs transmissions ordre
assert(items[1]!.idRoutage === 2, '2e transmission');

// 4 — N2 → N1
const retourN2: DemandePaiementRetourDestinataire = {
  idRoutage: 10,
  typeRetour: DemandePaiementRetourType.N2VersN1,
  actionRoutage: DemandePaiementRoutageAction.RejetValidationEntite,
  statutSource: 'EN_VALIDATION_N2',
  statutCible: 'EN_VALIDATION_N1',
  idUtilisateurDestinataire: 5,
  nomUtilisateurDestinataire: 'Dupont',
  prenomUtilisateurDestinataire: 'Jean',
  dateRoutage: '2026-03-02T08:00:00',
  motif: 'Correction',
};
assert(
  formatRetourDestinataireLabel(retourN2) === 'Retour au validateur N1 — Jean Dupont',
  'N2→N1 label',
);

// 5 — Z → Y
const retourZY: DemandePaiementRetourDestinataire = {
  idRoutage: 11,
  typeRetour: DemandePaiementRetourType.JuniorVersCharge,
  actionRoutage: DemandePaiementRoutageAction.RetourInterEtapes,
  statutSource: 'EN_CONTROLE_BUDGETAIRE',
  statutCible: 'EN_TRAITEMENT_DPM',
  idUtilisateurDestinataire: 7,
  nomUtilisateurDestinataire: 'Martin',
  prenomUtilisateurDestinataire: 'Paul',
  dateRoutage: '2026-03-02T09:00:00',
  motif: null,
};
assert(
  formatRetourDestinataireLabel(retourZY) === 'Retour au Chargé DP — Paul Martin',
  'Z→Y label',
);

// 6 — V → Z
const retourVZ: DemandePaiementRetourDestinataire = {
  idRoutage: 12,
  typeRetour: DemandePaiementRetourType.VisaVersControle,
  actionRoutage: DemandePaiementRoutageAction.RetourVisa,
  statutSource: 'EN_CONTROLE_BUDGETAIRE',
  statutCible: 'EN_CONTROLE_BUDGETAIRE',
  idUtilisateurDestinataire: 8,
  nomUtilisateurDestinataire: 'Junior',
  prenomUtilisateurDestinataire: 'Un',
  dateRoutage: '2026-03-02T10:00:00',
  motif: null,
};
  assert(
  formatRetourDestinataireLabel(retourVZ) === 'Retour au contrôle budgétaire — Un Junior',
  'V→Z label',
);

// 7 — identité absente
const retourSansNom: DemandePaiementRetourDestinataire = {
  ...retourN2,
  nomUtilisateurDestinataire: null,
  prenomUtilisateurDestinataire: null,
};
assert(
  formatRetourDestinataireLabel(retourSansNom) === 'Retour au validateur N1',
  'sans identité : pas de nom inventé',
);
assert(labelRetourDestinataireHintFromApi(retourSansNom) === null, 'hint sans nom');

// 8 — A_CORRIGER avec destinataire
const retoursACorriger: DemandePaiementRetourDestinataire[] = [
  {
    idRoutage: 20,
    typeRetour: DemandePaiementRetourType.ChargeVersDemandeur,
    actionRoutage: DemandePaiementRoutageAction.RetourDemandeur,
    statutSource: 'EN_TRAITEMENT_DPM',
    statutCible: 'A_CORRIGER',
    idUtilisateurDestinataire: 3,
    nomUtilisateurDestinataire: 'Kabongo',
    prenomUtilisateurDestinataire: 'Jean',
    dateRoutage: '2026-03-03T08:00:00',
    motif: 'Pièce manquante',
  },
];
const pourACorriger = findRetourPourStatutACorriger(retoursACorriger);
assert(pourACorriger?.idRoutage === 20, 'retour A_CORRIGER trouvé');

// 9 — actions charge masquées autre assigné
assert(
  peutAfficherActionsMetierAssignation({
    idUtilisateurAssigne: 99,
    idUtilisateurCourant: 10,
  }) === false,
  'actions masquées autre assigné',
);

// 10 — cohérence badge/actions
assert(
  resolveAssignationDisplay({
    idUtilisateurAssigne: 10,
    nomUtilisateurAssigne: 'Martin',
    prenomUtilisateurAssigne: 'Paul',
    idUtilisateurCourant: 20,
  })?.kind === 'assigned',
  'badge assignée autre',
);
assert(
  peutAfficherActionsMetierAssignation({
    idUtilisateurAssigne: 10,
    idUtilisateurCourant: 10,
  }),
  'actions ok pour assigné courant',
);

// retour lié au routage dans le workflow
const retourItems = buildWorkflowRoutageItems(
  [
    sampleRoutage({
      idRoutage: 11,
      action: DemandePaiementRoutageAction.RetourInterEtapes,
      statutSource: 'EN_CONTROLE_BUDGETAIRE',
      statutCible: 'EN_TRAITEMENT_DPM',
    }),
  ],
  [retourZY],
);
assert(retourItems[0]!.retourLabel?.includes('Paul Martin'), 'workflow retour Z→Y');

console.log('demandePaiementLot364.selftest OK');
