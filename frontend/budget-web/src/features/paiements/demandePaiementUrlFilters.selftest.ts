/**
 * Persistance URL filtres DPM.
 * npx --yes tsx src/features/paiements/demandePaiementUrlFilters.selftest.ts
 */
import {
  buildBudgetSearchParams,
  buildCanonicalSearchParams,
  buildChargeDpmSearchParams,
  buildDemandePaiementListSearchParams,
  buildMesDemandesSearchParams,
  parseBudgetFileParam,
  parseChargeDpmUrlFilters,
  parseIdParam,
  parseIsoDateParam,
  parseMesDemandesUrlFilters,
  removeEmptySearchParams,
  searchParamsEqual,
  toCompteursApiParams,
  toListApiParams,
} from './demandePaiementUrlFilters.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

const TODAY = '2026-08-31';
const ALLOWED = ['SOUMISE', 'BROUILLON'] as const;

// 1. parsing vide
const empty = parseMesDemandesUrlFilters(new URLSearchParams(), { today: TODAY });
assert(empty.statut === '', 'mes-demandes vide : statut');
assert(empty.dateDebut === TODAY, 'mes-demandes vide : dateDebut défaut');
assert(empty.idExercice === undefined, 'mes-demandes vide : pas idExercice');

// 2. parsing IDs
const withIds = parseMesDemandesUrlFilters(
  new URLSearchParams('idExercice=12&idUB=4&idCasDossier=3'),
  { today: TODAY },
);
assert(withIds.idExercice === 12, 'idExercice valide');
assert(withIds.idUB === 4, 'idUB valide');
assert(withIds.idCasDossier === 3, 'idCasDossier valide');

// 3. ID invalide
assert(parseIdParam('abc') === undefined, 'abc invalide');
assert(parseIdParam('NaN') === undefined, 'NaN invalide');
assert(parseIdParam('') === undefined, 'vide invalide');
assert(parseIdParam('-1') === undefined, 'négatif invalide');
const invalidId = parseMesDemandesUrlFilters(new URLSearchParams('idUB=abc'), { today: TODAY });
assert(invalidId.idUB === undefined, 'idUB abc ignoré');
assert(invalidId.idUBUi === '', 'idUBUi vide si invalide');

// 4. dates valides
assert(parseIsoDateParam('2026-08-01', TODAY) === '2026-08-01', 'date valide');

// 5. dates invalides
assert(parseIsoDateParam('bad', TODAY) === TODAY, 'date invalide → fallback');

// 6. statut valide
const soumise = parseMesDemandesUrlFilters(new URLSearchParams('statut=SOUMISE'), {
  today: TODAY,
  allowedStatuts: ALLOWED,
});
assert(soumise.statut === 'SOUMISE', 'statut SOUMISE');

// 7. statut interdit
const interdit = parseMesDemandesUrlFilters(
  new URLSearchParams('statut=EN_TRAITEMENT_DPM'),
  { today: TODAY, allowedStatuts: ALLOWED },
);
assert(interdit.statut === '', 'statut interdit → Toutes');
const canonicalInterdit = buildCanonicalSearchParams(
  'mes-demandes',
  new URLSearchParams('statut=EN_TRAITEMENT_DPM&idExercice=1'),
  { today: TODAY, allowedStatuts: ALLOWED },
);
assert(!canonicalInterdit.has('statut'), 'canonical supprime statut interdit');
assert(canonicalInterdit.get('idExercice') === '1', 'canonical conserve idExercice');

// 8. suppression filtre
const removed = buildMesDemandesSearchParams(
  new URLSearchParams('statut=SOUMISE&idExercice=12&idUB=4'),
  { idUB: '' },
);
assert(removed.get('statut') === 'SOUMISE', 'conserve statut');
assert(removed.get('idExercice') === '12', 'conserve exercice');
assert(!removed.has('idUB'), 'supprime idUB vide');

// 9. combinaison multi-filtres
const multi = buildMesDemandesSearchParams(new URLSearchParams(), {
  statut: 'SOUMISE',
  dateDebut: '2026-08-01',
  dateFin: '2026-08-31',
  idExercice: '1',
  idUB: '2',
  idCasDossier: '3',
  explicitDates: true,
}, { today: TODAY });
assert(multi.get('statut') === 'SOUMISE', 'multi statut');
assert(multi.get('dateDebut') === '2026-08-01', 'multi dateDebut');
assert(multi.get('idCasDossier') === '3', 'multi cas');

// 10. round-trip
const roundTripParams = new URLSearchParams(
  'statut=SOUMISE&dateDebut=2026-08-01&dateFin=2026-08-31&idExercice=1',
);
const parsed = parseMesDemandesUrlFilters(roundTripParams, { today: TODAY, allowedStatuts: ALLOWED });
const rebuilt = buildMesDemandesSearchParams(new URLSearchParams(), {
  statut: parsed.statut,
  dateDebut: parsed.dateDebut,
  dateFin: parsed.dateFin,
  idExercice: parsed.idExerciceUi,
  explicitDates: true,
}, { today: TODAY });
assert(rebuilt.get('statut') === 'SOUMISE', 'round-trip statut');
assert(rebuilt.get('dateDebut') === '2026-08-01', 'round-trip dateDebut');

// 11. Budget file=active
assert(parseBudgetFileParam('active') === 'FILE', 'file active');
assert(parseBudgetFileParam(null) === 'FILE', 'file défaut active');
const budgetActive = parseBudgetFileParam('toutes');
assert(budgetActive === 'TOUTES', 'file toutes');

const budgetParams = buildBudgetSearchParams(new URLSearchParams(), { file: 'TOUTES' });
assert(budgetParams.get('file') === 'toutes', 'URL file=toutes');

const budgetDefault = buildCanonicalSearchParams('budget', new URLSearchParams(), { today: TODAY });
assert(!budgetDefault.has('file'), 'file absent → active implicite');

// 12. Charge DP sans dates
const charge = buildChargeDpmSearchParams(
  new URLSearchParams('dateDebut=2026-08-01&statut=SOUMISE'),
  { statut: 'EN_TRAITEMENT_DPM' },
);
assert(!charge.has('dateDebut'), 'charge DP : pas de dates');
assert(charge.get('statut') === 'EN_TRAITEMENT_DPM', 'charge statut');

const chargeParsed = parseChargeDpmUrlFilters(
  new URLSearchParams(
    'statut=SOUMISE&idExercice=1&idDepartement=3&idUB=2&idDemandeur=8&idCasDossier=4&reference=DP-001',
  ),
  { allowedStatuts: ['SOUMISE', 'EN_TRAITEMENT_DPM'] },
);
assert(chargeParsed.reference === 'DP-001', 'charge reference');
assert(chargeParsed.idDemandeur === 8, 'charge demandeur');

// 13. conservation autres paramètres
const base = new URLSearchParams('statut=SOUMISE&idExercice=12&idUB=4');
const patchUb = buildMesDemandesSearchParams(base, { idUB: '' });
assert(patchUb.get('statut') === 'SOUMISE', 'patch conserve statut');
assert(patchUb.get('idExercice') === '12', 'patch conserve exercice');

// 14. absence statut dans compteurs
const listParams = toListApiParams(
  parseMesDemandesUrlFilters(new URLSearchParams('statut=BROUILLON'), {
    today: TODAY,
    allowedStatuts: ALLOWED,
  }),
);
const compteurParams = toCompteursApiParams(
  parseMesDemandesUrlFilters(new URLSearchParams('statut=BROUILLON'), {
    today: TODAY,
    allowedStatuts: ALLOWED,
  }),
);
assert(listParams.statut === 'BROUILLON', 'liste inclut statut');
assert(!('statut' in compteurParams), 'compteurs sans statut');

// removeEmptySearchParams
const dirty = new URLSearchParams('statut=SOUMISE&idUB=');
const clean = removeEmptySearchParams(dirty);
assert(clean.get('statut') === 'SOUMISE', 'clean garde statut');
assert(!clean.has('idUB'), 'clean supprime idUB vide');

// Étape 8 compat builders
const step8 = buildDemandePaiementListSearchParams({
  statut: 'BROUILLON',
  dateDebut: TODAY,
  dateFin: TODAY,
  today: TODAY,
});
assert(step8.get('statut') === 'BROUILLON', 'step8 brouillon');

assert(
  searchParamsEqual(
    new URLSearchParams('b=2&a=1'),
    new URLSearchParams('a=1&b=2'),
  ),
  'searchParamsEqual ordre',
);

console.log('demandePaiementUrlFilters.selftest OK');
