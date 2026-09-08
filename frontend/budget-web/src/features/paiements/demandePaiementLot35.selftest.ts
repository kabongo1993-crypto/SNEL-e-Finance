/**
 * Lot 3.5 — libellés retour, assignation, workflow, scope liste.
 * npx --yes tsx src/features/paiements/demandePaiementLot35.selftest.ts
 */
import { buildAssignationView } from './demandePaiementAssignationFromApi.ts';
import {
  peutAfficherActionsMetierAssignation,
  resolveAssignationDisplay,
} from './demandePaiementAssignationUtils.ts';
import { toListApiParams } from './demandePaiementUrlFilters.ts';
import {
  labelRetourAction,
  labelRetourSuccess,
  resolveRetourContextControle,
  resolveRetourContextValidation,
} from './demandePaiementRetourLabels.ts';
import { getWorkflowEtapeLabels } from './demandePaiementWorkflowUtils.ts';
import { labelStatutDpm } from './paiementUtils.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

// 1–3 assignation (préparé pour champs API futurs)
assert(
  resolveAssignationDisplay({
    idUtilisateurAssigne: 10,
    idUtilisateurCourant: 10,
  })?.kind === 'yours',
  'assignée à moi → À vous',
);
assert(
  resolveAssignationDisplay({
    idUtilisateurAssigne: 99,
    nomUtilisateurAssigne: 'Dupont',
    prenomUtilisateurAssigne: 'Jean',
    idUtilisateurCourant: 10,
  })?.kind === 'assigned',
  'assignée à autre',
);
assert(
  resolveAssignationDisplay({
    idUtilisateurAssigne: null,
    idUtilisateurCourant: 10,
  })?.kind === 'pool',
  'pool métier',
);
assert(buildAssignationView({ idUtilisateurAssigne: 10, nomUtilisateurAssigne: null, prenomUtilisateurAssigne: null }, 10)?.idUtilisateurAssigne === 10, 'api view courant');
assert(buildAssignationView({ idUtilisateurAssigne: null, nomUtilisateurAssigne: null, prenomUtilisateurAssigne: null }, 10) != null, 'api pool non null view');
assert(
  resolveAssignationDisplay(buildAssignationView({ idUtilisateurAssigne: null, nomUtilisateurAssigne: null, prenomUtilisateurAssigne: null }, 10)!)?.kind === 'pool',
  'pool via api',
);
assert(
  peutAfficherActionsMetierAssignation({
    idUtilisateurAssigne: 99,
    idUtilisateurCourant: 10,
  }) === false,
  'assignée à autre → pas actions',
);
assert(
  peutAfficherActionsMetierAssignation({
    idUtilisateurAssigne: 10,
    idUtilisateurCourant: 10,
  }),
  'à moi → actions ok',
);

// 4 A_CORRIGER
assert(
  labelStatutDpm('A_CORRIGER').toLowerCase().includes('demandeur'),
  'statut A_CORRIGER = retour demandeur',
);

// 5 N2 → N1
assert(
  resolveRetourContextValidation('EN_VALIDATION_N2') === 'validation_n2',
  'contexte N2',
);
assert(
  labelRetourAction('validation_n2') === 'Retour au validateur N1',
  'libellé N2',
);

// 6 Z → Y1
assert(
  labelRetourAction('controle_junior') === 'Retour au Chargé DP',
  'libellé Z→Y1',
);
assert(
  labelRetourSuccess('controle_junior').includes('Chargé DP'),
  'succès Z→Y1',
);

// 7 V → Z
assert(
  labelRetourAction('controle_viseur') === 'Retour au contrôle budgétaire',
  'libellé V→Z',
);
assert(
  resolveRetourContextControle({ canControler: false, canViser: true, mode: 'viseur' }) ===
    'controle_viseur',
  'contexte viseur',
);

// 8 même UB — pas de logique UB côté assignation (null API)
assert(resolveAssignationDisplay(null) === null, 'sans données assignation');

// 9–10 scope liste
const chargeParams = toListApiParams({
  scope: 'charge-dpm',
  statut: '',
  idExercice: undefined,
  idUB: undefined,
  idDepartement: undefined,
  idCasDossier: undefined,
  idDemandeur: undefined,
  reference: undefined,
  idExerciceUi: '',
  idUBUi: '',
  idDepartementUi: '',
  idCasDossierUi: '',
  idDemandeurUi: '',
  referenceUi: '',
});
assert(chargeParams.scope === 'charge-dpm', 'scope charge-dpm transmis');

const mesParams = toListApiParams({
  scope: 'mes-demandes',
  statut: 'A_CORRIGER',
  dateDebut: '2026-01-01',
  dateFin: '2026-01-31',
  idExerciceUi: '',
  idUBUi: '',
  idCasDossierUi: '',
});
assert(mesParams.scope === 'mes-demandes', 'scope mes-demandes');
assert(mesParams.statut === 'A_CORRIGER', 'statut liste');

assert(
  getWorkflowEtapeLabels('A_CORRIGER').statutLabel.includes('DEMANDEUR'),
  'workflow bloc A_CORRIGER',
);

console.log('demandePaiementLot35.selftest OK');
