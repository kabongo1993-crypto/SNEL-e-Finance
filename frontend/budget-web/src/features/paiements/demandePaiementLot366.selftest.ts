/**
 * Lot 3.6.6 — couverture HTTP/workflow frontend, cohérence liste/détail, retours API.
 * npx --yes tsx src/features/paiements/demandePaiementLot366.selftest.ts
 */
import {
  buildWorkflowRoutageItems,
  DemandePaiementRetourType,
  DemandePaiementRoutageAction,
  formatRetourDestinataireLabel,
  formatTransmissionActeurs,
  labelRoutageAction,
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

const routage = (
  partial: Partial<DemandePaiementRoutage> & Pick<DemandePaiementRoutage, 'idRoutage' | 'action'>,
): DemandePaiementRoutage => ({
  statutSource: 'SOUMISE',
  statutCible: 'EN_TRAITEMENT_DPM',
  idUtilisateurSource: 1,
  nomUtilisateurSource: 'Source',
  prenomUtilisateurSource: 'A',
  idUtilisateurCible: 2,
  nomUtilisateurCible: 'Cible',
  prenomUtilisateurCible: 'B',
  dateRoutage: '2026-03-01T10:00:00',
  estActif: true,
  motif: 'Motif test',
  ...partial,
});

// 10.1 — workflow alimenté par routage API
const routages: DemandePaiementRoutage[] = [
  routage({
    idRoutage: 1,
    action: DemandePaiementRoutageAction.Receptionner,
    dateRoutage: '2026-03-01T09:00:00',
    estActif: false,
  }),
  routage({
    idRoutage: 2,
    action: DemandePaiementRoutageAction.Orienter,
    statutSource: 'EN_TRAITEMENT_DPM',
    statutCible: 'EN_CONTROLE_BUDGETAIRE',
    dateRoutage: '2026-03-01T11:00:00',
  }),
];
const workflow = buildWorkflowRoutageItems(routages, []);
assert(workflow.length === 2, 'historique routage affiché');
assert(workflow[0]!.idRoutage === 1 && workflow[1]!.idRoutage === 2, 'ordre chronologique');
assert(workflow[0]!.actionLabel.includes('Réception'), 'libellé action');
assert(workflow[0]!.statutTransition.includes('Soumise'), 'statut conservé');
assert(workflow[0]!.dateRoutage === '2026-03-01T09:00:00', 'date affichée');
assert(
  formatTransmissionActeurs(routages[1]!)?.includes('Vers : B Cible'),
  'source/cible depuis API',
);

// 10.2 — retours N1→X et Y→X depuis API (pas d'heuristique)
const retourN1X: DemandePaiementRetourDestinataire = {
  idRoutage: 30,
  typeRetour: DemandePaiementRetourType.N1VersDemandeur,
  actionRoutage: DemandePaiementRoutageAction.RejetValidationEntite,
  statutSource: 'EN_VALIDATION_N1',
  statutCible: 'A_CORRIGER',
  idUtilisateurDestinataire: 10,
  nomUtilisateurDestinataire: 'Kabongo',
  prenomUtilisateurDestinataire: 'Jean',
  dateRoutage: '2026-03-02T08:00:00',
  motif: null,
};
assert(
  formatRetourDestinataireLabel(retourN1X) === 'Retour au demandeur — Jean Kabongo',
  'N1→X label API',
);

const retourYX: DemandePaiementRetourDestinataire = {
  idRoutage: 31,
  typeRetour: DemandePaiementRetourType.ChargeVersDemandeur,
  actionRoutage: DemandePaiementRoutageAction.RetourDemandeur,
  statutSource: 'EN_TRAITEMENT_DPM',
  statutCible: 'A_CORRIGER',
  idUtilisateurDestinataire: 10,
  nomUtilisateurDestinataire: 'Kabongo',
  prenomUtilisateurDestinataire: 'Jean',
  dateRoutage: '2026-03-02T09:00:00',
  motif: 'Pièce',
};
assert(
  formatRetourDestinataireLabel(retourYX) === 'Retour au demandeur — Jean Kabongo',
  'Y→X label API',
);

// 10.3 — utilisateur inconnu : pas de nom inventé
const retourSansNom: DemandePaiementRetourDestinataire = {
  ...retourN1X,
  idUtilisateurDestinataire: 9999,
  nomUtilisateurDestinataire: null,
  prenomUtilisateurDestinataire: null,
};
assert(
  !formatRetourDestinataireLabel(retourSansNom).includes('9999'),
  'pas de nom inventé depuis id',
);
assert(
  formatRetourDestinataireLabel(retourSansNom) === 'Retour au demandeur',
  'libellé générique sans identité',
);

// 10.4 — assignation pool / à vous / autre
assert(resolveAssignationDisplay({
  idUtilisateurAssigne: null,
  idUtilisateurCourant: 10,
})?.kind === 'pool', 'badge pool');
assert(resolveAssignationDisplay({
  idUtilisateurAssigne: 10,
  idUtilisateurCourant: 10,
})?.kind === 'yours', 'badge à vous');
assert(
  resolveAssignationDisplay({
    idUtilisateurAssigne: 99,
    nomUtilisateurAssigne: 'Martin',
    prenomUtilisateurAssigne: 'Paul',
    idUtilisateurCourant: 10,
  })?.kind === 'assigned',
  'badge assignée autre',
);

// 10.5 — cohérence liste/détail : mêmes règles d'actions
const listView = {
  idUtilisateurAssigne: 101,
  idUtilisateurCourant: 101,
};
const detailView = { ...listView };
assert(
  peutAfficherActionsMetierAssignation(listView) ===
    peutAfficherActionsMetierAssignation(detailView),
  'liste et détail : même visibilité actions assigné',
);
assert(
  peutAfficherActionsMetierAssignation({
    idUtilisateurAssigne: 101,
    idUtilisateurCourant: 102,
  }) === false,
  'autre assigné : actions masquées',
);
assert(peutAfficherActionsMetierAssignation({
  idUtilisateurAssigne: null,
  idUtilisateurCourant: 10,
}), 'pool : actions visibles côté client (backend autoritaire)');

// libellé routage retour REJET N2→N1
assert(
  labelRoutageAction(
    DemandePaiementRoutageAction.RejetValidationEntite,
    'EN_VALIDATION_N2',
    'EN_VALIDATION_N1',
  ).includes('validateur N1'),
  'libellé rejet N2→N1',
);

// workflow avec retour lié
const retourZY: DemandePaiementRetourDestinataire = {
  idRoutage: 40,
  typeRetour: DemandePaiementRetourType.JuniorVersCharge,
  actionRoutage: DemandePaiementRoutageAction.RetourInterEtapes,
  statutSource: 'EN_CONTROLE_BUDGETAIRE',
  statutCible: 'EN_TRAITEMENT_DPM',
  idUtilisateurDestinataire: 101,
  nomUtilisateurDestinataire: 'Martin',
  prenomUtilisateurDestinataire: 'Paul',
  dateRoutage: '2026-03-03T10:00:00',
  motif: null,
};
const wfRetour = buildWorkflowRoutageItems(
  [
    routage({
      idRoutage: 40,
      action: DemandePaiementRoutageAction.RetourInterEtapes,
      statutSource: 'EN_CONTROLE_BUDGETAIRE',
      statutCible: 'EN_TRAITEMENT_DPM',
    }),
  ],
  [retourZY],
);
assert(wfRetour[0]!.retourLabel?.includes('Paul Martin'), 'retour Z→Y depuis API');

console.log('demandePaiementLot366.selftest OK');
