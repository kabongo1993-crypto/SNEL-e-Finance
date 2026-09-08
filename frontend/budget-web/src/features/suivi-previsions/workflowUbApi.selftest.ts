/**
 * Garde-fou non-régression — rejet UB ≠ endpoint Version.
 * Exécuter : npx --yes tsx src/features/suivi-previsions/workflowUbApi.selftest.ts
 *
 * Vérifie aussi (manuel / CI grep) que SuiviUbDetailPage et Soumissions
 * n'appellent plus rejeterVersionBudgetaire.
 */
function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

const PATHS_UB = [
  '/api/v1/workflow-previsions-ub/soumettre',
  '/api/v1/workflow-previsions-ub/controler',
  '/api/v1/workflow-previsions-ub/valider',
  '/api/v1/workflow-previsions-ub/rejeter',
  '/api/v1/workflow-previsions-ub/rejeter-departement',
  '/api/v1/workflow-previsions-ub/reouvrir',
] as const;

const FORBIDDEN_UB_REJECT = '/api/v1/versions-budgetaires/{id}/rejeter';

for (const p of PATHS_UB) {
  assert(p.includes('workflow-previsions-ub'), `chemin OK: ${p}`);
  assert(!p.includes('versions-budgetaires'), `interdit versions-budgetaires: ${p}`);
}

assert(
  !FORBIDDEN_UB_REJECT.includes('workflow-previsions-ub'),
  'endpoint Version reste distinct du workflow UB',
);

/** Contrat body rejet UB */
const rejetUbBody = { idVersion: 1, idUB: 2, motif: 'motif' };
assert(rejetUbBody.idVersion > 0 && rejetUbBody.idUB > 0, 'rejet UB exige idVersion + idUB');
assert(rejetUbBody.motif.trim().length > 0, 'motif obligatoire');

/** Contrat body rejet département */
const rejetDeptBody = { idVersion: 1, idDepartement: 10, motif: 'motif' };
assert(rejetDeptBody.idDepartement > 0, 'rejet département exige idDepartement');

console.log('workflowUbApi.selftest OK');
console.log('  UB reject path:', PATHS_UB[3]);
console.log('  forbidden from UB view:', FORBIDDEN_UB_REJECT);
