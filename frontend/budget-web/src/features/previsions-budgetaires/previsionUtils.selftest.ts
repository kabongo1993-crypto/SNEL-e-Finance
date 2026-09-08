/**
 * Tests utilitaires prévision — exécutables via :
 *   npx --yes tsx src/features/previsions-budgetaires/previsionUtils.selftest.ts
 * (vitest n'est pas encore configuré dans budget-web)
 */
import {
  agregatSections,
  contexteComplet,
  cumulRepartitions,
  filtrerGroupesReplies,
  hasRepartitionNonVide,
  montantAnnuelAffiche,
  parseMontantInput,
  repartirMontantSur12Mois,
  resolveContexteDepuisPrevisions,
  setMoisMontant,
} from './previsionUtils';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

function moisEq(
  repartitions: { mois: number; montant: number }[],
  mois: number,
  montant: number,
): boolean {
  return repartitions.some((r) => r.mois === mois && r.montant === montant);
}

// Verrouillage contexte
assert(
  !contexteComplet({
    idExercice: '1',
    idVersion: '',
    idTypeBudget: '1',
    idModePrevision: '1',
    idUB: '1',
    codeType: 'DC',
    actionAE: '',
    idItemBI: '',
  }),
  'grille verrouillée sans version',
);

assert(
  contexteComplet({
    idExercice: '1',
    idVersion: '1',
    idTypeBudget: '1',
    idModePrevision: '1',
    idUB: '1',
    codeType: 'DC',
    actionAE: '',
    idItemBI: '',
  }),
  'DC prêt avec UB',
);

assert(
  !contexteComplet({
    idExercice: '1',
    idVersion: '1',
    idTypeBudget: '1',
    idModePrevision: '1',
    idUB: '1',
    codeType: 'AE',
    actionAE: '',
    idItemBI: '',
  }),
  'AE verrouillé sans action',
);

assert(
  contexteComplet({
    idExercice: '1',
    idVersion: '1',
    idTypeBudget: '1',
    idModePrevision: '1',
    idUB: '1',
    codeType: 'AE',
    actionAE: 'Formations',
    idItemBI: '',
  }),
  'AE prêt avec action',
);

assert(
  !contexteComplet({
    idExercice: '1',
    idVersion: '1',
    idTypeBudget: '1',
    idModePrevision: '1',
    idUB: '1',
    codeType: 'BI',
    actionAE: '',
    idItemBI: '',
  }),
  'BI verrouillé sans item',
);

assert(
  contexteComplet({
    idExercice: '1',
    idVersion: '1',
    idTypeBudget: '1',
    idModePrevision: '1',
    idUB: '1',
    codeType: 'BI',
    actionAE: '',
    idItemBI: '9',
  }),
  'BI prêt avec item',
);

const ctxMensuel = resolveContexteDepuisPrevisions([
  {
    idPrevision: 1,
    idTypeBudget: 1,
    codeType: 'DC',
    idModePrevision: 1,
    codeMode: 'ANNUEL',
    dateCreation: '2026-01-01T00:00:00Z',
  },
  {
    idPrevision: 2,
    idTypeBudget: 1,
    codeType: 'DC',
    idModePrevision: 2,
    codeMode: 'MENSUEL',
    dateCreation: '2026-01-02T00:00:00Z',
    dateModification: '2026-02-01T00:00:00Z',
  },
  {
    idPrevision: 3,
    idTypeBudget: 1,
    codeType: 'DC',
    idModePrevision: 2,
    codeMode: 'MENSUEL',
    dateCreation: '2026-01-03T00:00:00Z',
  },
]);
assert(ctxMensuel?.idModePrevision === 2, 'restaure le mode le plus utilisé (MENSUEL)');
assert(ctxMensuel?.codeMode === 'MENSUEL', 'codeMode MENSUEL');

const ctxAe = resolveContexteDepuisPrevisions([
  {
    idPrevision: 10,
    idTypeBudget: 2,
    codeType: 'AE',
    idModePrevision: 1,
    codeMode: 'ANNUEL',
    libelleItemAE: 'Maintenance réseau',
    dateCreation: '2026-03-01T00:00:00Z',
  },
]);
assert(ctxAe?.libelleItemAE === 'Maintenance réseau', 'restaure action AE');

// Montants
assert(parseMontantInput('-10') === null, 'refuse négatif');
assert(parseMontantInput('1 200,5') === 1200.5, 'parse FR');
assert(cumulRepartitions([{ mois: 1, montant: 100 }, { mois: 2, montant: 50 }]) === 150, 'cumul');

const reps = setMoisMontant([], 1, 8700);
assert(reps.length === 1 && reps[0]!.montant === 8700, 'set mois');

// Agrégat par idGroupeRB (pas parentId) — deux groupes code 02 distincts
const lignes = [
  {
    idRB: null,
    idGroupeRB: 10,
    parentIdRB: null,
    estSection: true,
    montantAnnuel: 0,
    cumulMensuel: 0,
    repartitions: [] as { mois: number; montant: number }[],
  },
  {
    idRB: 2,
    idGroupeRB: 10,
    parentIdRB: 99,
    estSection: false,
    montantAnnuel: 100,
    cumulMensuel: 100,
    repartitions: [{ mois: 1, montant: 100 }],
  },
  {
    idRB: 3,
    idGroupeRB: 10,
    parentIdRB: 99,
    estSection: false,
    montantAnnuel: 50,
    cumulMensuel: 50,
    repartitions: [{ mois: 2, montant: 50 }],
  },
  {
    idRB: null,
    idGroupeRB: 11,
    parentIdRB: null,
    estSection: true,
    montantAnnuel: 0,
    cumulMensuel: 0,
    repartitions: [] as { mois: number; montant: number }[],
  },
  {
    idRB: 4,
    idGroupeRB: 11,
    parentIdRB: 99,
    estSection: false,
    montantAnnuel: 20,
    cumulMensuel: 20,
    repartitions: [{ mois: 1, montant: 20 }],
  },
];
const agg = agregatSections(lignes, false);
assert(agg[0]!.montantAnnuel === 150, 'agrégat groupe 10 annuel');
assert(agg[3]!.montantAnnuel === 20, 'agrégat groupe 11 séparé (même code possible)');

const aggM = agregatSections(lignes, true);
assert(aggM[0]!.cumulMensuel === 150, 'agrégat mensuel cumul');
assert(moisEq(aggM[0]!.repartitions, 1, 100), 'groupe jan');
assert(moisEq(aggM[0]!.repartitions, 2, 50), 'groupe fév');
assert(aggM[0]!.montantAnnuel === 150, 'groupe montant annuel = somme des montants feuilles (100+50)');
assert(aggM[0]!.cumulMensuel === 150, 'groupe cumul = somme mois');

assert(montantAnnuelAffiche(12000, []) === 12000, 'annuel seul');
assert(montantAnnuelAffiche(12000, [{ mois: 1, montant: 5000 }, { mois: 2, montant: 7000 }]) === 12000, 'annuel = sum mois');
assert(montantAnnuelAffiche(999, [{ mois: 1, montant: 100 }]) === 100, 'sum mois prioritaire');

const collapsed = filtrerGroupesReplies(agg, new Set([10]));
assert(collapsed.length === 3, 'groupe 10 replié : header + groupe 11 + RB');
assert(collapsed.every((l) => l.idGroupeRB !== 10 || l.estSection), 'pas de RB du groupe 10');

// Répartition 12 mois — somme exacte
function assertSum12(montant: number, msg: string): void {
  const reps = repartirMontantSur12Mois(montant);
  assert(reps !== null, `${msg}: null`);
  const sum = cumulRepartitions(reps!);
  assert(Math.abs(sum - montant) < 1e-9, `${msg}: sum=${sum} ≠ ${montant}`);
  assert((reps!.length === 0) === (montant === 0), `${msg}: longueur`);
}

assertSum12(12000, '12 000 / 12');
assertSum12(10000, '10 000 / 12');
assertSum12(0, '0');
assertSum12(0.01, '1 centime');
assertSum12(100.07, 'centimes irréguliers');
assert(repartirMontantSur12Mois(-1) === null, 'négatif refusé');
assert(repartirMontantSur12Mois(Number.NaN) === null, 'NaN refusé');

const r10k = repartirMontantSur12Mois(10000)!;
assert(r10k.length === 12, '10k : 12 mois');
const cents = r10k.map((r) => Math.round(r.montant * 100));
assert(cents.reduce((a, b) => a + b, 0) === 1000000, '10k centimes exacts');
assert(cents.filter((c) => c === 83334).length === 4, 'reste sur 4 premiers mois');
assert(cents.filter((c) => c === 83333).length === 8, 'base sur 8 mois');

assert(!hasRepartitionNonVide([]), 'vide');
assert(hasRepartitionNonVide([{ mois: 1, montant: 1 }]), 'non vide');

console.log('previsionUtils.selftest: OK');
