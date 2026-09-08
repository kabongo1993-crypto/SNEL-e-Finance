/**
 * Synchronisation URL ↔ filtres liste DPM.
 * npx --yes tsx src/features/paiements/useDemandePaiementListFilters.selftest.ts
 */
import {
  buildDemandePaiementListSearchParams,
  buildStatutOnlySearchParams,
  parseIsoDateParam,
} from './demandePaiementUrlFilters.ts';
import { normalizeStatutNavValue } from './paiementStatutNavConfig.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

const TODAY = '2026-08-31';

assert(parseIsoDateParam('2026-08-01', TODAY) === '2026-08-01', 'parse date valide');
assert(parseIsoDateParam('bad', TODAY) === TODAY, 'parse date invalide → fallback');
assert(parseIsoDateParam(null, TODAY) === TODAY, 'parse date absente → fallback');

assert(normalizeStatutNavValue('SOUMISE') === 'SOUMISE', 'statut SOUMISE');

const clean = buildDemandePaiementListSearchParams({
  statut: '',
  dateDebut: TODAY,
  dateFin: TODAY,
  today: TODAY,
});
assert(clean.toString() === '', 'sans filtre → URL vide');

const brouillon = buildDemandePaiementListSearchParams({
  statut: 'BROUILLON',
  dateDebut: TODAY,
  dateFin: TODAY,
  today: TODAY,
});
assert(brouillon.get('statut') === 'BROUILLON', 'chip brouillon → statut');
assert(brouillon.get('dateDebut') === TODAY, 'chip brouillon → dateDebut aujourd’hui explicite');
assert(brouillon.get('dateFin') === TODAY, 'chip brouillon → dateFin aujourd’hui explicite');
assert(!brouillon.has('statut[]'), 'pas de tableau statut');

const periode = buildDemandePaiementListSearchParams({
  statut: 'SOUMISE',
  dateDebut: '2026-08-01',
  dateFin: '2026-08-31',
  today: TODAY,
});
assert(periode.get('statut') === 'SOUMISE', 'statut soumise');
assert(periode.get('dateDebut') === '2026-08-01', 'date début conservée');
assert(periode.get('dateFin') === '2026-08-31', 'date fin conservée');

const changeStatut = buildDemandePaiementListSearchParams({
  statut: 'A_CORRIGER',
  dateDebut: '2026-08-01',
  dateFin: '2026-08-31',
  today: TODAY,
});
assert(changeStatut.get('statut') === 'A_CORRIGER', 'changement statut conserve dates');
assert(changeStatut.get('dateDebut') === '2026-08-01', 'dates inchangées après statut');

const chargeToutes = buildStatutOnlySearchParams('');
assert(chargeToutes.toString() === '', 'charge DP toutes → URL vide');

const chargeSoumise = buildStatutOnlySearchParams('SOUMISE');
assert(chargeSoumise.get('statut') === 'SOUMISE', 'charge DP soumises');
assert(!chargeSoumise.has('dateDebut'), 'charge DP sans dates');

const chargeTraitement = buildStatutOnlySearchParams('EN_TRAITEMENT_DPM');
assert(chargeTraitement.get('statut') === 'EN_TRAITEMENT_DPM', 'charge DP traitement');

console.log('useDemandePaiementListFilters.selftest OK');
